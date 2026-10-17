using UnityEngine;

// With "Reload Domain" turned off in Enter Play Mode Options (it makes Play mode start faster), static variables keep their values
// from the previous run: the party stayed evolved and the defeated trainers stayed defeated. This puts everything back to the
// starting state every time Play is pressed, the way restarting the Python program does.
public static class GameStateReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        PlayerParty.ResetState();
        TrainerDatabase.ResetState();
        WorldColliders.ResetState();
        UiKit.ClearCache();
        TitleMenu.ResetState();
        Multiplayer.ResetState();
        SaveSystem.ResetState();
    }

    // An exported game opens in a normal 1280 x 720 window (the size the interface is drawn for) that can be moved next to another one,
    // instead of covering the whole screen. (In the editor the Game view decides.)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SetUpWindow()
    {
#if !UNITY_EDITOR
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        Application.runInBackground = true; // a second copy next to it must keep running while this one is in front
#endif
    }
}
