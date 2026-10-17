using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// A box in the corner with what the game is doing, for finding problems in an exported game where there is no Console.
// Press Tab to show or hide it. It creates itself; nothing has to be added to the scene.
public class DebugOverlay : MonoBehaviour
{
    bool visible;
    PlayerController player;
    MapLoader mapLoader;
    NetworkSession session;
    float frameTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("Debug Overlay");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugOverlay>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) visible = !visible;
        frameTime = Mathf.Lerp(frameTime, Time.unscaledDeltaTime, 0.1f);
    }

    void OnGUI()
    {
        if (!visible) return;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (mapLoader == null) mapLoader = FindFirstObjectByType<MapLoader>();
        if (session == null) session = FindFirstObjectByType<NetworkSession>();

        var keyboard = Keyboard.current;
        var text = new StringBuilder();
        text.AppendLine($"{Application.productName}  {Application.version}   {(frameTime > 0 ? 1f / frameTime : 0):0} fps   {Screen.width}x{Screen.height}");
        text.AppendLine($"keyboard: {(keyboard == null ? "NONE" : keyboard.displayName)}");
        if (keyboard != null)
        {
            text.AppendLine($"arrows pressed: up {keyboard.upArrowKey.isPressed}  down {keyboard.downArrowKey.isPressed}  left {keyboard.leftArrowKey.isPressed}  right {keyboard.rightArrowKey.isPressed}");
            text.AppendLine($"WASD pressed:   W {keyboard.wKey.isPressed}  A {keyboard.aKey.isPressed}  S {keyboard.sKey.isPressed}  D {keyboard.dKey.isPressed}");
        }
        text.AppendLine($"map: {(mapLoader != null ? mapLoader.CurrentMapName : "-")}   title open: {TitleMenu.IsOpen}   multiplayer menu: {Multiplayer.IsMenuOpen}");
        if (player != null)
            text.AppendLine($"player: ({player.transform.position.x:0.0}, {player.transform.position.y:0.0})  blocked: {player.blocked}  direction: {player.direction}  enabled: {player.enabled}");
        text.AppendLine($"time scale: {Time.timeScale}   dialog: {FindFirstObjectByType<DialogSystem>()?.Active}   battle: {FindFirstObjectByType<BattleSystem>()?.Active}");
        text.AppendLine($"colliders: {WorldColliders.Rects.Count}   characters: {FindObjectsByType<Character>(FindObjectsSortMode.None).Length}");
        if (session != null)
            text.AppendLine($"network: {session.CurrentState}  {session.Status}");
        text.AppendLine($"saves: {Application.persistentDataPath}");

        var area = new Rect(10, 10, 760, 200);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + 8, area.y + 4, area.width - 16, area.height - 8), text.ToString());
    }
}
