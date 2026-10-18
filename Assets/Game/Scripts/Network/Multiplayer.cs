using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Playing with another person on a local network: each player runs the game on their own computer. During the game + opens a small
// menu to host a game or join one by typing the host's address. Once connected, each player sees the other walking around in their
// own world. One per scene: add it to the Game object. The + key opens and closes the menu.
public class Multiplayer : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const float SendInterval = 0.08f;       // positions are sent about 12 times a second
    const float HeartbeatInterval = 1f;     // ... and at least once a second even when standing still

    [SerializeField] Texture2D remoteSheet;   // graphics/characters/player2.png: how the other player looks
    [SerializeField] Texture2D shadowTexture;
    [SerializeField] Font regularFont;

    // the colours of the menu buttons
    static readonly Color Fill = UiKit.Hex("#fefefc"), Shadow = UiKit.Hex("#b3c9d9"), Purple = UiKit.Hex("#2e1b53");

    enum Screen { Closed, Main, Hosting, Address, Connecting, Message }

    struct Entry
    {
        public string label;
        public Action action;
    }

    NetworkSession session;
    PlayerController player;
    MapLoader mapLoader;
    DialogSystem dialogSystem;
    BattleSystem battleSystem;
    EvolutionSystem evolutionSystem;
    MonsterIndex monsterIndex;

    // the menu
    GameObject canvasObject;
    RectTransform content;
    Screen screen = Screen.Closed;
    readonly List<Entry> entries = new List<Entry>();
    int index;
    string address = "127.0.0.1";
    string message = "";
    bool textSubscribed;
    bool toggleRequested;     // the + key was typed
    float deleteHeld;         // how long the delete key has been held, for repeating
    NetworkSession.State shownState;

    // the other player
    RemotePlayer remote;
    string remoteMap = "";
    float sendTimer, heartbeatTimer;
    Vector2 lastSentPosition;
    string lastSentMap = "";

    // a line at the top that says what happened (connected, left...)
    GameObject toast;
    float toastTimer;

    public bool IsOpen => screen != Screen.Closed;

    // The other player's map and position, from their last message (used by the duel challenge to see if they are close)
    public string RemoteMap => remoteMap;
    public Vector2 RemotePosition { get; private set; }

    // True while the menu is open, or a duel challenge is on: everything else in the game ignores the keyboard, so Enter, Space,
    // Escape and the arrows only do things there
    static bool menuOpen;
    public static bool IsMenuOpen => menuOpen || DuelChallenge.IsActive;

    public static void ResetState()
    {
        menuOpen = false;
    }

    // True when the game is busy with something that owns the keys (a dialogue, a fight, an evolution, the monster index, the title)
    public bool IsBusyWithGame()
    {
        return TitleMenu.IsOpen
            || (dialogSystem != null && dialogSystem.Active)
            || (battleSystem != null && battleSystem.Active)
            || (evolutionSystem != null && evolutionSystem.Active)
            || (monsterIndex != null && monsterIndex.IsOpen);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        mapLoader = FindFirstObjectByType<MapLoader>();
        dialogSystem = FindFirstObjectByType<DialogSystem>();
        battleSystem = FindFirstObjectByType<BattleSystem>();
        evolutionSystem = FindFirstObjectByType<EvolutionSystem>();
        monsterIndex = FindFirstObjectByType<MonsterIndex>();

        // Two copies on one computer cannot both be in front: the one behind must keep running or the connection would freeze
        Application.runInBackground = true;

        session = gameObject.AddComponent<NetworkSession>();
        session.Connected += OnConnected;
        session.Disconnected += OnDisconnected;
        session.MessageReceived += OnMessage;

        // the duel challenge between the two players
        var challenge = gameObject.AddComponent<DuelChallenge>();
        challenge.Configure(this, session, regularFont);

        address = PlayerPrefs.GetString("arcania.lastAddress", address);
        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Multiplayer");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700; // over the game's menus, under the title screen (800) and the screen fade (1000)
        canvas.pixelPerfect = true;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // the game dims behind the menu
        var dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>();
        dim.transform.SetParent(canvasObject.transform, false);
        dim.color = new Color(0, 0, 0, 150f / 255f);
        dim.raycastTarget = false;
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
        dim.gameObject.name = "Dim";

        // a 1280x720 panel in the middle of the screen: everything below uses pygame coordinates inside it
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(panel, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (!textSubscribed)
            {
                keyboard.onTextInput += OnText;
                textSubscribed = true;
            }

            if (toggleRequested)
            {
                toggleRequested = false;
                Toggle();
            }
            else if (IsOpen)
            {
                HandleKeys(keyboard);
            }
        }

        // the menu follows the connection: a player joining or the connection failing changes what it shows
        if (IsOpen && session.CurrentState != shownState) OnStateChanged();

        if (toast != null)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f) { Destroy(toast); toast = null; }
        }

        SendPosition();
        UpdateRemote();
    }

    // ---- the menu ----

    // + opens and closes it, except when the game is busy with something else
    void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        bool busy = IsBusyWithGame() || DuelChallenge.IsActive;
        if (busy) return;

        player.Block();
        canvasObject.SetActive(true);
        menuOpen = true;
        Go(Screen.Main);
    }

    void Close()
    {
        screen = Screen.Closed;
        menuOpen = false;
        canvasObject.SetActive(false);
        player.Unblock();
    }

    void Go(Screen next)
    {
        screen = next;
        index = 0;
        shownState = session.CurrentState;
        Rebuild();
    }

    // What the screen offers
    void BuildEntries(out string[] lines)
    {
        entries.Clear();
        lines = new string[0];

        switch (screen)
        {
            case Screen.Main:
                if (session.CurrentState == NetworkSession.State.Connected)
                {
                    lines = new[] { "Connected" };
                    Add("Disconnect", () => { session.Disconnect(); Message("Disconnected"); });
                }
                else
                {
                    Add("Host a game", () => { session.Host(); Go(Screen.Hosting); });
                    Add("Join a game", () => Go(Screen.Address));
                }
                Add("Close", Close);
                break;

            case Screen.Hosting:
                lines = HostingLines();
                Add("Cancel", () => { session.Disconnect(); Go(Screen.Main); });
                break;

            case Screen.Address:
                lines = new[] { "Address of the host:", address + ((int)(Time.unscaledTime * 2f) % 2 == 0 ? "_" : " ") };
                Add("Connect", ConnectToHost);
                Add("Back", () => Go(Screen.Main));
                break;

            case Screen.Connecting:
                lines = new[] { "Connecting..." };
                Add("Cancel", () => { session.Disconnect(); Go(Screen.Main); });
                break;

            case Screen.Message:
                lines = new[] { message };
                Add("OK", () => Go(Screen.Main));
                break;
        }
    }

    // The address to type, and the other addresses this computer has in case the first one is not on the same network
    string[] HostingLines()
    {
        var lines = new List<string> { "Waiting for a player...", "Your address:", $"{session.HostAddress}:{NetworkSession.Port}" };
        var others = NetworkSession.AllAddresses();
        others.Remove(session.HostAddress);
        if (others.Count > 0) lines.Add("Or: " + string.Join("  ", others));
        return lines.ToArray();
    }

    void Add(string label, Action action)
    {
        entries.Add(new Entry { label = label, action = action });
    }

    void Message(string text)
    {
        message = text;
        Go(Screen.Message);
    }

    void ConnectToHost()
    {
        PlayerPrefs.SetString("arcania.lastAddress", address);
        session.Join(address);
        Go(Screen.Connecting);
    }

    void HandleKeys(Keyboard keyboard)
    {
        // the address screen is a text field: the keys are typed, and only Enter, Escape and the arrows act
        if (keyboard.downArrowKey.wasPressedThisFrame) { index = (index + 1) % entries.Count; Rebuild(); }
        if (keyboard.upArrowKey.wasPressedThisFrame) { index = (index - 1 + entries.Count) % entries.Count; Rebuild(); }

        bool confirm = keyboard.enterKey.wasPressedThisFrame || (screen != Screen.Address && keyboard.spaceKey.wasPressedThisFrame);
        if (confirm) entries[index].action();
        else if (keyboard.escapeKey.wasPressedThisFrame) GoBack();

        // the address field: the delete key (backspace on other keyboards) erases, and keeps erasing while it is held
        if (screen == Screen.Address)
        {
            bool delete = keyboard.backspaceKey.isPressed || keyboard.deleteKey.isPressed;
            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
            {
                Erase();
                deleteHeld = 0f;
            }
            else if (delete)
            {
                deleteHeld += Time.unscaledDeltaTime;
                if (deleteHeld > 0.4f) { Erase(); deleteHeld = 0.35f; } // after a short wait, about every 0.05 s
            }
            else
            {
                deleteHeld = 0f;
            }

            Rebuild(); // the blinking cursor
        }
    }

    void GoBack()
    {
        if (screen == Screen.Main) Close();
        else if (screen == Screen.Hosting || screen == Screen.Connecting) { session.Disconnect(); Go(Screen.Main); }
        else Go(Screen.Main);
    }

    // Typed characters: + opens the menu (it is the typed character, so it works on every keyboard layout and on the numpad),
    // and on the address screen they are the address
    void OnText(char character)
    {
        if (character == '+')
        {
            if (screen != Screen.Address) toggleRequested = true;
            return;
        }

        if (screen != Screen.Address) return;

        // (erasing is done with the delete key itself in HandleKeys: the typed character it produces differs between systems)
        if (address.Length < 40 && (char.IsLetterOrDigit(character) || character == '.' || character == ':' || character == '-')) address += character;
    }

    void Erase()
    {
        if (address.Length > 0) address = address.Substring(0, address.Length - 1);
    }

    // The connection changed while the menu was open
    void OnStateChanged()
    {
        shownState = session.CurrentState;

        if (session.CurrentState == NetworkSession.State.Connected) Message("Connected!");
        else if (session.CurrentState == NetworkSession.State.Offline && (screen == Screen.Connecting || screen == Screen.Hosting)) Message(session.Status);
        else Rebuild();
    }

    void Rebuild()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        BuildEntries(out string[] lines);
        index = Mathf.Clamp(index, 0, Mathf.Max(0, entries.Count - 1));
        var font = new UiFont(regularFont, 30, 38);

        // the text lines, each in a white bubble like the dialogue boxes
        float y = 150;
        foreach (var line in lines)
        {
            float width = UiKit.Measure(font, line) + 28;
            UiKit.Box(content, (WindowWidth - width) / 2f, y, width, font.surfaceHeight + 28, Color.white, 4);
            UiKit.Label(content, line, font, Color.black, Anchor.MidTop, WindowWidth / 2f, y + 14);
            y += font.surfaceHeight + 28 + 12;
        }

        // the buttons, in the style of the title menu
        const float buttonWidth = 420, buttonHeight = 60, gap = 80;
        float top = Mathf.Max(y + 30, 360);
        for (int i = 0; i < entries.Count; i++)
        {
            float x = (WindowWidth - buttonWidth) / 2f, buttonY = top + i * gap;
            UiKit.Box(content, x + 8, buttonY + 8, buttonWidth, buttonHeight, Shadow);
            UiKit.Box(content, x, buttonY, buttonWidth, buttonHeight, Fill);
            UiKit.Label(content, entries[i].label, font, Purple, Anchor.Center, x + buttonWidth / 2f, buttonY + buttonHeight / 2f);
            if (i == index) PixelArrow(x - 64, buttonY + buttonHeight / 2f);
        }
    }

    // A right-pointing triangle made of 6 px steps, with the same fill and shadow as the buttons
    void PixelArrow(float x, float centerY)
    {
        const float step = 6;
        float[] widths = { 6, 12, 18, 24, 18, 12, 6 };
        float top = centerY - widths.Length * step / 2f;

        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass == 0 ? 5 : 0;
            Color color = pass == 0 ? Shadow : Fill;
            for (int row = 0; row < widths.Length; row++)
                UiKit.Box(content, x + offset, top + row * step + offset, widths[row], step, color);
        }
    }

    // ---- the connection ----

    void OnConnected()
    {
        lastSentMap = ""; // send everything again to the new player
        Toast("Player connected");
    }

    void OnDisconnected()
    {
        if (remote != null) { Destroy(remote.gameObject); remote = null; }
        Toast("The other player left");
    }

    // Tells the other player where this one is: POS|map|x|y|facing|moving
    void SendPosition()
    {
        if (session.CurrentState != NetworkSession.State.Connected || mapLoader == null || string.IsNullOrEmpty(mapLoader.CurrentMapName)) return;

        sendTimer -= Time.unscaledDeltaTime;
        heartbeatTimer -= Time.unscaledDeltaTime;
        if (sendTimer > 0f) return;
        sendTimer = SendInterval;

        Vector2 position = player.transform.position;
        string map = mapLoader.CurrentMapName;
        bool changed = map != lastSentMap || (position - lastSentPosition).sqrMagnitude > 1e-6f;
        if (!changed && heartbeatTimer > 0f) return;

        heartbeatTimer = HeartbeatInterval;
        bool moving = (position - lastSentPosition).sqrMagnitude > 1e-6f;
        lastSentPosition = position;
        lastSentMap = map;

        session.Send(string.Format(CultureInfo.InvariantCulture, "POS|{0}|{1:0.###}|{2:0.###}|{3}|{4}",
            map, position.x, position.y, player.FacingDirection, moving ? 1 : 0));
    }

    void OnMessage(string line)
    {
        var parts = line.Split('|');
        if (parts[0] != "POS" || parts.Length < 6) return;

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !Enum.TryParse(parts[4], true, out Direction facing)) return;

        remoteMap = parts[1];
        RemotePosition = new Vector2(x, y);
        if (remote == null) CreateRemote();
        remote?.SetState(new Vector2(x, y), facing);
    }

    void CreateRemote()
    {
        if (remoteSheet == null || shadowTexture == null)
        {
            Debug.LogError("The Multiplayer component has no picture for the other player: right-click its header and choose Reset.");
            return;
        }

        // inactive until the textures are in place, so Entity.Awake can use them
        var go = new GameObject("Other player");
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        go.AddComponent<SpriteRenderer>();
        remote = go.AddComponent<RemotePlayer>();
        remote.Setup(remoteSheet, shadowTexture, Direction.Down);
        go.SetActive(true);
    }

    // The other player only shows in the map they are in
    void UpdateRemote()
    {
        if (remote == null || mapLoader == null) return;
        remote.gameObject.SetActive(remoteMap == mapLoader.CurrentMapName && !TitleMenu.IsOpen);
    }

    // ---- a line of text at the top of the screen ----

    public void Notice(string text) => Toast(text);

    void Toast(string text)
    {
        if (toast != null) Destroy(toast);

        toast = new GameObject("Toast", typeof(RectTransform));
        var canvas = toast.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;
        canvas.pixelPerfect = true;
        var scaler = toast.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        toast.transform.SetParent(transform, false);

        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(toast.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        var font = new UiFont(regularFont, 30, 38);
        float width = UiKit.Measure(font, text) + 28;
        UiKit.Box(panel, (WindowWidth - width) / 2f, 20, width, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(panel, text, font, Color.black, Anchor.MidTop, WindowWidth / 2f, 20 + 14);
        toastTimer = 3f;
    }

    void OnDestroy()
    {
        menuOpen = false;
        if (textSubscribed && Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the picture of the other player and the font
    void Reset()
    {
        remoteSheet = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/characters/player2.png");
        shadowTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/other/shadow.png");
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
