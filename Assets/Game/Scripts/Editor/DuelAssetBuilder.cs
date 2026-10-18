using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

// Menu Arcania > Build Duel Animators. Makes everything the duel's Animators need, from the monster sheets:
//   - one PNG per frame of Cindrill, Pluma and Jacana (graphics/duel/frames), imported as sprites,
//   - one AnimationClip per state of each automaton (Resources/Duel/Clips): sprite frames plus move / scale / tilt / colour curves,
//   - one Animator Controller per automaton (Resources/Duel/Striker, Guardian, Referee) whose states are the automaton's states
//     and whose transitions are the pairs of delta: the tool calls the Step function of DuelAutomata for every state and symbol.
// Run it again after changing the automata or the animations: everything is rebuilt.
public static class DuelAssetBuilder
{
    const string FramesFolder = "Assets/Game/graphics/duel/frames";
    const string ResourcesFolder = "Assets/Game/Resources/Duel";
    const string ClipsFolder = ResourcesFolder + "/Clips";
    const int FrameSize = 192;   // the monster sheets are 4 x 2 frames of 192 x 192 (row 0 idle, row 1 attack)

    // one animation curve value per property, so that every clip writes all of them (a state never inherits the pose of the last one)
    class Pose
    {
        public float x, y, scaleX = 1, scaleY = 1, tilt;
        public Color color = Color.white;
    }

    [MenuItem("Arcania/Build Duel Animators")]
    public static void Build()
    {
        Directory.CreateDirectory(FramesFolder);
        Directory.CreateDirectory(ClipsFolder);

        var cindrill = CutFrames("Cindrill");
        var pluma = CutFrames("Pluma");
        var jacana = CutFrames("Jacana");

        BuildStriker(cindrill);
        BuildGuardian(pluma);
        BuildReferee(jacana);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Duel animators built in " + ResourcesFolder);
    }

    // ---------- frames ----------

    // Returns the 8 sprites of a sheet: [0..3] idle, [4..7] attack
    static Sprite[] CutFrames(string monster)
    {
        string sheetPath = $"Assets/Game/graphics/monsters/{monster}.png";
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sheet.LoadImage(File.ReadAllBytes(sheetPath));   // reads the file itself, so the import settings do not matter

        var paths = new List<string>();
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                // textures count rows from the bottom: the idle row is the top one
                var pixels = sheet.GetPixels(column * FrameSize, sheet.height - (row + 1) * FrameSize, FrameSize, FrameSize);
                var frame = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
                frame.SetPixels(pixels);
                string path = $"{FramesFolder}/{monster}_{(row == 0 ? "idle" : "attack")}_{column}.png";
                File.WriteAllBytes(path, frame.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frame);
                paths.Add(path);
            }
        }
        UnityEngine.Object.DestroyImmediate(sheet);

        AssetDatabase.Refresh();
        var sprites = new Sprite[paths.Count];
        for (int i = 0; i < paths.Count; i++)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
        }
        return sprites;
    }

    static Sprite[] Idle(Sprite[] all) => new[] { all[0], all[1], all[2], all[3] };
    static Sprite[] Attack(Sprite[] all) => new[] { all[4], all[5], all[6], all[7] };

    // ---------- the three automata ----------

    static void BuildStriker(Sprite[] f)
    {
        var clips = new Dictionary<int, AnimationClip>();
        string o = "Striker";
        // positive x moves away from the opponent: the lunge is negative
        clips[(int)StrikerState.Idle]      = Clip(o, "Idle", Idle(f), 6, true, 0.8f, t => new Pose { y = Wave(t, 0.8f) * 6 });
        clips[(int)StrikerState.Attacking] = Clip(o, "Attacking", Attack(f), 8, false, 0.6f, t => new Pose { x = -Hump(t, 0.6f) * 150, scaleX = 1 + Hump(t, 0.6f) * 0.1f, scaleY = 1 + Hump(t, 0.6f) * 0.1f });
        clips[(int)StrikerState.Charging]  = Clip(o, "Charging", Idle(f), 14, true, 0.5f, t => new Pose { scaleX = 1 + Hump(t, 0.5f) * 0.18f, scaleY = 1 + Hump(t, 0.5f) * 0.18f, color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.2f), 0.4f + 0.5f * Hump(t, 0.5f)) });
        clips[(int)StrikerState.Defending] = Clip(o, "Defending", Idle(f), 4, true, 0.8f, t => new Pose { scaleX = 1.08f, scaleY = 0.88f, y = -18, color = new Color(0.6f, 0.8f, 1f) });
        clips[(int)StrikerState.Hurt]      = Clip(o, "Hurt", new[] { f[4] }, 1, false, 0.5f, t => new Pose { x = Mathf.Sin(t * 60f) * 14 * (1 - t / 0.5f), color = new Color(1f, 0.35f, 0.35f) });
        clips[(int)StrikerState.Victory]   = Clip(o, "Victory", Idle(f), 8, true, 0.6f, t => new Pose { y = Hump(t, 0.6f) * 40, scaleX = 1.1f, scaleY = 1.1f, color = new Color(1f, 0.95f, 0.6f) });
        clips[(int)StrikerState.Defeated]  = Clip(o, "Defeated", new[] { f[0] }, 1, false, 0.8f, t => new Pose { y = -Mathf.Min(1, t / 0.8f) * 60, tilt = Mathf.Min(1, t / 0.8f) * 90, color = new Color(0.45f, 0.45f, 0.45f, 0.7f) });

        MakeController<StrikerState, StrikerSymbol>(o, clips, StrikerAutomaton.Step);
    }

    static void BuildGuardian(Sprite[] f)
    {
        var clips = new Dictionary<int, AnimationClip>();
        string o = "Guardian";
        clips[(int)GuardianState.Idle]      = Clip(o, "Idle", Idle(f), 5, true, 0.8f, t => new Pose { y = Wave(t, 0.8f) * 4 });
        clips[(int)GuardianState.Attacking] = Clip(o, "Attacking", Attack(f), 8, false, 0.6f, t => new Pose { x = -Hump(t, 0.6f) * 120, tilt = Hump(t, 0.6f) * 10 });
        clips[(int)GuardianState.Defending] = Clip(o, "Defending", Idle(f), 4, true, 0.8f, t => new Pose { scaleX = 1.05f, scaleY = 0.9f, y = -14, color = new Color(0.55f, 0.75f, 1f) });
        clips[(int)GuardianState.Healing]   = Clip(o, "Healing", Idle(f), 6, true, 1.0f, t => new Pose { y = Hump(t, 1.0f) * 24, color = Color.Lerp(Color.white, new Color(0.5f, 1f, 0.6f), 0.4f + 0.5f * Hump(t, 1.0f)) });
        clips[(int)GuardianState.Hurt]      = Clip(o, "Hurt", new[] { f[4] }, 1, false, 0.5f, t => new Pose { x = Mathf.Sin(t * 60f) * 14 * (1 - t / 0.5f), color = new Color(1f, 0.35f, 0.35f) });
        clips[(int)GuardianState.Victory]   = Clip(o, "Victory", Idle(f), 8, true, 0.6f, t => new Pose { y = Hump(t, 0.6f) * 40, scaleX = 1.1f, scaleY = 1.1f, color = new Color(1f, 0.95f, 0.6f) });
        clips[(int)GuardianState.Defeated]  = Clip(o, "Defeated", new[] { f[0] }, 1, false, 0.8f, t => new Pose { y = -Mathf.Min(1, t / 0.8f) * 60, tilt = -Mathf.Min(1, t / 0.8f) * 90, color = new Color(0.45f, 0.45f, 0.45f, 0.7f) });

        MakeController<GuardianState, GuardianSymbol>(o, clips, GuardianAutomaton.Step);
    }

    static void BuildReferee(Sprite[] f)
    {
        var clips = new Dictionary<int, AnimationClip>();
        string o = "Referee";
        clips[(int)RefereeState.Waiting]  = Clip(o, "Waiting", Idle(f), 5, true, 0.8f, t => new Pose { y = Wave(t, 0.8f) * 5 });
        clips[(int)RefereeState.Watching] = Clip(o, "Watching", Idle(f), 8, true, 0.6f, t => new Pose { tilt = Wave(t, 0.6f) * 8, color = new Color(1f, 1f, 0.8f) });
        clips[(int)RefereeState.Judging]  = Clip(o, "Judging", Attack(f), 10, true, 0.5f, t => new Pose { scaleX = 1 + Hump(t, 0.5f) * 0.12f, scaleY = 1 + Hump(t, 0.5f) * 0.12f, color = new Color(1f, 0.85f, 0.5f) });
        clips[(int)RefereeState.Ruling]   = Clip(o, "Ruling", new[] { f[4], f[5] }, 3, true, 0.7f, t => new Pose { y = Hump(t, 0.7f) * 30, scaleX = 1.2f, scaleY = 1.2f, color = Color.white });
        clips[(int)RefereeState.Finished] = Clip(o, "Finished", Idle(f), 4, true, 1.2f, t => new Pose { y = Wave(t, 1.2f) * 10, tilt = Wave(t, 1.2f) * 12, color = new Color(1f, 0.85f, 0.3f) });

        MakeController<RefereeState, RefereeSymbol>(o, clips, RefereeAutomaton.Step);
    }

    // ---------- curves ----------

    static float Wave(float t, float length) => Mathf.Sin(t / length * Mathf.PI * 2f);          // -1..1, one cycle per clip
    static float Hump(float t, float length) => Mathf.Sin(Mathf.Clamp01(t / length) * Mathf.PI);  // 0..1..0 over the clip

    // A clip made of sprite frames (at the given frames per second) and a pose sampled over its length
    static AnimationClip Clip(string owner, string state, Sprite[] sprites, float fps, bool loop, float length, Func<float, Pose> pose)
    {
        var clip = new AnimationClip { frameRate = 60, name = $"{owner}_{state}" };

        // the pictures: each one is shown for 1 / fps seconds; a looping clip ends one frame later so that it repeats cleanly
        var keys = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < sprites.Length; i++) keys.Add(new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] });
        if (loop)
        {
            length = Mathf.Max(length, sprites.Length / fps);
            keys.Add(new ObjectReferenceKeyframe { time = length, value = sprites[0] });
        }
        else length = Mathf.Max(length, (sprites.Length - 1) / fps);
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys.ToArray());

        // the pose, sampled every 1/30 s
        int samples = Mathf.Max(2, Mathf.CeilToInt(length * 30f) + 1);
        var curves = new Dictionary<string, (Type type, AnimationCurve curve)>
        {
            { "m_AnchoredPosition.x", (typeof(RectTransform), new AnimationCurve()) },
            { "m_AnchoredPosition.y", (typeof(RectTransform), new AnimationCurve()) },
            { "m_LocalScale.x", (typeof(RectTransform), new AnimationCurve()) },
            { "m_LocalScale.y", (typeof(RectTransform), new AnimationCurve()) },
            { "localEulerAnglesRaw.z", (typeof(RectTransform), new AnimationCurve()) },
            { "m_Color.r", (typeof(Image), new AnimationCurve()) },
            { "m_Color.g", (typeof(Image), new AnimationCurve()) },
            { "m_Color.b", (typeof(Image), new AnimationCurve()) },
            { "m_Color.a", (typeof(Image), new AnimationCurve()) },
        };
        for (int i = 0; i < samples; i++)
        {
            float t = Mathf.Min(length, i * length / (samples - 1));
            var p = pose(t);
            curves["m_AnchoredPosition.x"].curve.AddKey(t, p.x);
            curves["m_AnchoredPosition.y"].curve.AddKey(t, p.y);
            curves["m_LocalScale.x"].curve.AddKey(t, p.scaleX);
            curves["m_LocalScale.y"].curve.AddKey(t, p.scaleY);
            curves["localEulerAnglesRaw.z"].curve.AddKey(t, p.tilt);
            curves["m_Color.r"].curve.AddKey(t, p.color.r);
            curves["m_Color.g"].curve.AddKey(t, p.color.g);
            curves["m_Color.b"].curve.AddKey(t, p.color.b);
            curves["m_Color.a"].curve.AddKey(t, p.color.a);
        }
        foreach (var entry in curves)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", entry.Value.type, entry.Key), entry.Value.curve);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = $"{ClipsFolder}/{clip.name}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ---------- controllers ----------

    // The Animator Controller of an automaton: one state per value of the state enum, one transition for every pair of delta that
    // changes state, taken when the integer parameter "State" becomes the number of the new state
    static void MakeController<TState, TSymbol>(string name, Dictionary<int, AnimationClip> clips, Func<TState, TSymbol, TState> step)
        where TState : struct, Enum where TSymbol : struct, Enum
    {
        string path = $"{ResourcesFolder}/{name}.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);
        var machine = controller.layers[0].stateMachine;

        var states = new Dictionary<int, AnimatorState>();
        int column = 0;
        foreach (TState state in Enum.GetValues(typeof(TState)))
        {
            int number = Convert.ToInt32(state);
            var animatorState = machine.AddState(state.ToString(), new Vector3(260 * (column % 4), 120 * (column / 4), 0));
            animatorState.motion = clips[number];
            animatorState.writeDefaultValues = true;
            states[number] = animatorState;
            column++;
        }
        machine.defaultState = states[0];   // q0

        var added = new HashSet<(int, int)>();
        foreach (TState from in Enum.GetValues(typeof(TState)))
        {
            foreach (TSymbol symbol in Enum.GetValues(typeof(TSymbol)))
            {
                var to = step(from, symbol);
                int a = Convert.ToInt32(from), b = Convert.ToInt32(to);
                if (a == b || !added.Add((a, b))) continue;

                var transition = states[a].AddTransition(states[b]);
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, b, "State");
            }
        }
        EditorUtility.SetDirty(controller);
    }
}
