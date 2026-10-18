using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The duel challenge between the two connected players (not in the Python game). C challenges the other player when both are close, inside the arena;
// they answer with Space (accept) or Escape (decline). Messages: CHALLENGE, ACCEPT, DECLINE, CANCEL, DUELEND.
// The host is always Player 1 and the guest Player 2, so each side knows its role (and its automaton) without asking.
// Created by Multiplayer.
public class DuelChallenge : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const float MaxDistance = 3f;   // in tiles
    const string DuelMap = "arena"; // duels only happen in the empty building

    public enum Phase { None, Sent, Received, InDuel }

    public static Phase Current { get; private set; }
    public static bool IsActive => Current != Phase.None;
    // 1 for the host, 2 for the guest; only meaningful during a duel
    public static int Role { get; private set; }

    public static void ResetState()
    {
        Current = Phase.None;
        Role = 0;
    }

    Multiplayer multiplayer;
    NetworkSession session;
    Font regularFont;
    PlayerController player;
    MapLoader mapLoader;

    DuelGame game;
    GameObject canvasObject;
    RectTransform content;
    string shown = "";
    float lockTimer;

    public void Configure(Multiplayer owner, NetworkSession networkSession, Font font)
    {
        multiplayer = owner;
        session = networkSession;
        regularFont = font;

        session.MessageReceived += OnMessage;
        session.Disconnected += OnDisconnected;
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (session != null)
        {
            session.MessageReceived -= OnMessage;
            session.Disconnected -= OnDisconnected;
        }
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Duel Challenge");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 660; // over the toast (650), under the network menu (700)
        canvas.pixelPerfect = true;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        content = panel;
        canvasObject.SetActive(false);
    }

    void Update()
    {
        if (session == null) return;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (mapLoader == null) mapLoader = FindFirstObjectByType<MapLoader>();

        var keyboard = Keyboard.current;
        if (lockTimer > 0f) lockTimer -= Time.unscaledDeltaTime;

        switch (Current)
        {
            case Phase.None:
                if (keyboard != null && keyboard.cKey.wasPressedThisFrame && CanChallenge()) Challenge();
                break;

            case Phase.Sent:
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { session.Send("CANCEL"); End(); }
                break;

            case Phase.Received:
                if (keyboard == null || lockTimer > 0f) break;
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) { session.Send("ACCEPT"); StartDuel(); }
                else if (keyboard.escapeKey.wasPressedThisFrame) { session.Send("DECLINE"); End(); }
                break;

            case Phase.InDuel:
                if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || (game != null && game.Finished && keyboard.spaceKey.wasPressedThisFrame)))
                {
                    session.Send("DUELEND");
                    End();
                }
                break;
        }

        Refresh();
    }

    bool CanChallenge()
    {
        if (session.CurrentState != NetworkSession.State.Connected) return false;
        if (multiplayer.IsOpen || multiplayer.IsBusyWithGame() || player == null || mapLoader == null) return false;
        if (mapLoader.CurrentMapName != DuelMap || multiplayer.RemoteMap != DuelMap) return false;
        return Vector2.Distance(player.transform.position, multiplayer.RemotePosition) <= MaxDistance;
    }

    void Challenge()
    {
        Current = Phase.Sent;
        Hold(true);
        session.Send("CHALLENGE");
    }

    void StartDuel()
    {
        Current = Phase.InDuel;
        Role = session.IsHost ? 1 : 2;
        Hold(true);

        if (game == null) game = gameObject.AddComponent<DuelGame>();
        game.Begin(Role, session, regularFont);
    }

    void End()
    {
        Current = Phase.None;
        Role = 0;
        Hold(false);
        if (game != null) game.Close();
    }

    // The player stands still while a challenge or duel is on
    void Hold(bool on)
    {
        if (player != null) player.blocked = on;
    }

    void OnMessage(string line)
    {
        switch (line.Split('|')[0])
        {
            case "CHALLENGE":
                if (Current == Phase.Sent) { session.Send("ACCEPT"); StartDuel(); } // both challenged at once: that is a yes
                else if (Current == Phase.None && !multiplayer.IsBusyWithGame() && !multiplayer.IsOpen)
                {
                    Current = Phase.Received;
                    lockTimer = 0.4f; // so a Space from walking around does not accept by accident
                    Hold(true);
                }
                else session.Send("DECLINE");
                break;
            case "ACCEPT":
                if (Current == Phase.Sent) StartDuel();
                break;
            case "DECLINE":
                if (Current == Phase.Sent) End();
                break;
            case "CANCEL":
                if (Current == Phase.Received) End();
                break;
            case "DUELEND":
                if (Current == Phase.InDuel) End();
                break;
        }
    }

    void OnDisconnected()
    {
        if (IsActive) End();
    }

    // ---- the prompt ----

    void Refresh()
    {
        string text = Current switch
        {
            Phase.Sent => "Challenge sent. Waiting for an answer...   Escape: cancel",
            Phase.Received => "The other player challenges you to a duel!   Space: accept   Escape: decline",
            Phase.InDuel => "",   // the duel screen draws everything
            _ => "",
        };

        if (text == shown) return;
        shown = text;

        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        canvasObject.SetActive(text != "");
        if (text == "") return;

        var font = new UiFont(regularFont, 30, 38);
        float width = UiKit.Measure(font, text) + 28;
        UiKit.Box(content, (WindowWidth - width) / 2f, 640, width, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(content, text, font, Color.black, Anchor.MidTop, WindowWidth / 2f, 640 + 14);
    }
}
