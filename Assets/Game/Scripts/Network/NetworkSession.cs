using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// The connection between two copies of the game over a network, with plain TCP. One player hosts (waits on a port) and the
// other joins (types the host's address). Messages are lines of text; the receiving happens on a background thread and is handed
// to the main thread in Update, so the rest of the game never has to think about threads.
public class NetworkSession : MonoBehaviour
{
    public enum State { Offline, Hosting, Connecting, Connected }

    public const int Port = 7777;
    const float ConnectTimeout = 6f;

    public State CurrentState { get; private set; }
    public string Status { get; private set; } = "";
    public string HostAddress { get; private set; } = "";   // the address the other player has to type, while hosting
    public bool IsHost { get; private set; }                // true for the player who hosted, false for the one who joined

    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> MessageReceived;

    TcpListener listener;
    TcpClient client;
    NetworkStream stream;

    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    readonly object sendLock = new object();
    volatile TcpClient pending;      // a connection made on a background thread, picked up in Update
    volatile string failure;         // an error from a background thread, shown in Update
    volatile bool dropped;
    int generation;                  // a reader thread that belongs to an old connection must not report anything
    float connectTimer;

    // Waits for another player to join
    public void Host()
    {
        Disconnect();
        try
        {
            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
        }
        catch (Exception exception)
        {
            Fail($"Could not host: {exception.Message}");
            return;
        }

        IsHost = true;
        HostAddress = LocalAddress();
        Debug.Log($"[Network] Hosting on port {Port}; this computer's addresses: {string.Join(", ", AllAddresses())}");
        CurrentState = State.Hosting;
        Status = "Waiting for a player...";
        listener.BeginAcceptTcpClient(OnAccept, listener);
    }

    // Joins a host. The address is an IP address or a name, optionally followed by :port
    public void Join(string address)
    {
        Disconnect();

        string host = address.Trim();
        int port = Port;
        int colon = host.LastIndexOf(':');
        if (colon > 0 && int.TryParse(host.Substring(colon + 1), out int parsed))
        {
            port = parsed;
            host = host.Substring(0, colon);
        }
        if (host.Length == 0)
        {
            Fail("Type the address of the host");
            return;
        }

        IsHost = false;
        Debug.Log($"[Network] Connecting to {host}:{port}");
        CurrentState = State.Connecting;
        Status = "Connecting...";
        connectTimer = 0f;
        client = new TcpClient();
        try
        {
            client.BeginConnect(host, port, OnConnect, client);
        }
        catch (Exception exception)
        {
            Fail($"Could not connect: {exception.Message}");
        }
    }

    // The other copy is gone, or the session is cancelled
    public void Disconnect()
    {
        generation++;
        CurrentState = State.Offline;
        HostAddress = "";

        try { listener?.Stop(); } catch { }
        try { client?.Close(); } catch { }
        try { pending?.Close(); } catch { }
        listener = null;
        client = null;
        stream = null;
        pending = null;
        failure = null;
        dropped = false;
        while (inbox.TryDequeue(out _)) { }
    }

    // Sends one line of text to the other player
    public void Send(string line)
    {
        if (CurrentState != State.Connected || stream == null) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(line + "\n");
            lock (sendLock) stream.Write(data, 0, data.Length);
        }
        catch
        {
            dropped = true;
        }
    }

    void Update()
    {
        // a player arrived (hosting) or the host answered (joining)
        if (pending != null)
        {
            var connection = pending;
            pending = null;
            Attach(connection);
        }

        if (failure != null)
        {
            string message = failure;
            failure = null;
            Fail(message);
        }

        if (CurrentState == State.Connecting)
        {
            connectTimer += Time.unscaledDeltaTime;
            if (connectTimer > ConnectTimeout) Fail("No answer from that address");
        }

        while (inbox.TryDequeue(out string line)) MessageReceived?.Invoke(line);

        if (dropped)
        {
            Disconnect();
            Status = "The other player left";
            Disconnected?.Invoke();
        }
    }

    void Attach(TcpClient connection)
    {
        // only one other player: nobody else can join
        try { listener?.Stop(); } catch { }
        listener = null;

        Debug.Log("[Network] Connected to the other player");
        client = connection;
        client.NoDelay = true; // small messages, sent often: send them right away
        stream = client.GetStream();
        CurrentState = State.Connected;
        Status = "Connected";
        HostAddress = "";

        int mine = generation;
        var thread = new Thread(() => ReadLoop(stream, mine)) { IsBackground = true };
        thread.Start();

        Connected?.Invoke();
    }

    // On a background thread: every line that arrives goes in the inbox
    void ReadLoop(NetworkStream source, int owner)
    {
        try
        {
            var reader = new StreamReader(source, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null) inbox.Enqueue(line);
        }
        catch
        {
            // the connection closed
        }

        if (owner == generation) dropped = true;
    }

    void OnAccept(IAsyncResult result)
    {
        try
        {
            pending = ((TcpListener)result.AsyncState).EndAcceptTcpClient(result);
        }
        catch
        {
            // the listener was stopped
        }
    }

    void OnConnect(IAsyncResult result)
    {
        var attempt = (TcpClient)result.AsyncState;
        try
        {
            attempt.EndConnect(result);
            if (attempt == client) pending = attempt;
        }
        catch (Exception exception)
        {
            Debug.Log($"[Network] Connection failed: {exception}");
            if (attempt == client) failure = $"Could not connect: {exception.Message}";
        }
    }

    void Fail(string message)
    {
        Disconnect();
        Status = message;
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // Every IPv4 address of this computer (several when there are a VPN, a bridge or more than one network card)
    public static System.Collections.Generic.List<string> AllAddresses()
    {
        var list = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) list.Add(address.ToString());
            }
        }
        catch
        {
            // no host name: the list stays as it is
        }
        return list;
    }

    // The address of this computer on the local network, which is what the other player types
    static string LocalAddress()
    {
        try
        {
            // connecting a UDP socket sends nothing: it only makes the system choose the network card that would be used
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            }
        }
        catch
        {
            foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) return address.ToString();
            }
            return "127.0.0.1";
        }
    }
}
