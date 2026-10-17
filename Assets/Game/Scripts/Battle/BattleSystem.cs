using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// How a fight starts and ends, ported from main.py: end_dialog (trainer fights), check_monster / monster_encounter (wild fights),
// the fade to the battle screen and end_battle. One per scene: add it to the Game object.
public class BattleSystem : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;

    [SerializeField] Font regularFont;           // fonts['regular']: PixeloidSans, 18
    [SerializeField] Font smallFont;             // fonts['small']: PixeloidSans, 14
    [SerializeField] Texture2D[] monsterSheets;  // graphics/monsters
    [SerializeField] Texture2D[] backgrounds;    // graphics/backgrounds: forest, ice, sand

    PlayerController player;
    DialogSystem dialogSystem;
    ScreenTint tint;
    GameObject canvasObject;
    RectTransform panel;
    Battle.Assets assets;
    readonly Dictionary<string, Sprite> backgroundSprites = new Dictionary<string, Sprite>();

    Battle pending;      // transition_target: built straight away, shown when the screen is black
    Battle battle;       // self.battle
    bool ending;

    // encounter_timer = Timer(2000, func = self.monster_encounter)
    float encounterDuration = 2f;
    float encounterElapsed;
    bool encounterActive;

    // True from the moment the battle screen is shown (self.battle in main.py)
    public bool Active => battle != null;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();

        var idleFrames = new Dictionary<string, Sprite[]>();
        foreach (var sheet in monsterSheets) idleFrames[sheet.name] = UiKit.CutRow(sheet, 4, 2, 0);

        foreach (var texture in backgrounds) backgroundSprites[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));

        assets = new Battle.Assets
        {
            idleFrames = idleFrames,
            regular = new UiFont(regularFont, 18, 23),
            small = new UiFont(smallFont, 14, 18),
        };

        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Battle");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // over the world, under the monster index (500) and the fade (1000)

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // black behind the 1280x720 picture, for windows that are wider than 16:9
        var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(canvasObject.transform, false);
        black.color = Color.black;
        black.raycastTarget = false;
        var blackRect = black.rectTransform;
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;

        panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(canvasObject.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        panel.anchoredPosition = Vector2.zero;

        canvasObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // self.encounter_timer.update()
        if (encounterActive)
        {
            encounterElapsed += dt;
            if (encounterElapsed >= encounterDuration)
            {
                MonsterEncounter();
                encounterActive = false;
            }
        }

        CheckMonster();

        if (battle != null)
        {
            battle.Update(dt);

            // TODO: temporary exit while there are no real actions. The real battle ends when one side has no monsters left.
            if (!ending && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) EndBattle(battle.character);
        }
    }

    // check_monster: standing in tall grass while walking starts the encounter timer
    void CheckMonster()
    {
        if (battle != null || pending != null || player.direction == Vector2.zero) return;
        if (!encounterActive && CollidingPatches().Count > 0)
        {
            encounterActive = true;
            encounterElapsed = 0f;
        }
    }

    List<WorldColliders.MonsterPatch> CollidingPatches()
    {
        var patches = new List<WorldColliders.MonsterPatch>();
        var hitbox = player.HitboxRect;
        foreach (var patch in WorldColliders.MonsterPatches)
        {
            if (WorldColliders.Overlap(hitbox, patch.rect)) patches.Add(patch);
        }
        return patches;
    }

    // monster_encounter: a wild fight against the monsters of the first patch under the player
    void MonsterEncounter()
    {
        var patches = CollidingPatches();
        if (patches.Count == 0 || player.direction == Vector2.zero) return;

        var patch = patches[0];
        encounterDuration = Random.Range(800, 2501) / 1000f;
        player.Block();
        // TODO: audio['overworld'].stop(), audio['battle'].play(-1)

        var opponents = new SortedDictionary<int, Monster>();
        for (int i = 0; i < patch.monsters.Length; i++)
            opponents[i] = new Monster(patch.monsters[i], patch.level + Random.Range(-3, 4));

        Begin(opponents, patch.biome, null);
    }

    // end_dialog, the part for a trainer that is not defeated yet
    public void StartTrainerBattle(Character trainer)
    {
        // TODO: audio['overworld'].stop(), audio['battle'].play(-1)
        Begin(trainer.monsters, trainer.data.biome, trainer);
    }

    void Begin(SortedDictionary<int, Monster> opponents, string biome, Character trainer)
    {
        if (!backgroundSprites.TryGetValue(biome, out var background))
        {
            Debug.LogError($"No battle background named '{biome}'");
            player.Unblock();
            return;
        }

        // the battle is built straight away (like Battle(...) in main.py) but only shown once the screen is black
        var battleAssets = new Battle.Assets { background = background, idleFrames = assets.idleFrames, regular = assets.regular, small = assets.small };
        var created = new Battle(panel, battleAssets, PlayerParty.Monsters, opponents, trainer);
        created.root.name = $"Battle ({biome})";
        created.root.SetActive(false);
        pending = created;

        if (tint == null) tint = FindFirstObjectByType<ScreenTint>();
        tint.Begin(() =>
        {
            battle = created;
            pending = null;
            created.root.SetActive(true);
            canvasObject.SetActive(true);
        });
    }

    // end_battle: back to the map; a defeated trainer talks to you again
    void EndBattle(Character trainer)
    {
        ending = true;
        // TODO: audio['battle'].stop()
        if (tint == null) tint = FindFirstObjectByType<ScreenTint>();
        var finished = battle;
        tint.Begin(() =>
        {
            finished.Destroy();
            battle = null;
            ending = false;
            canvasObject.SetActive(false);
        });

        if (trainer != null)
        {
            trainer.data.defeated = true;
            if (dialogSystem == null) dialogSystem = FindFirstObjectByType<DialogSystem>();
            dialogSystem.CreateDialog(trainer);
        }
        else
        {
            player.Unblock();
            // TODO: check_evolution()
        }
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the fonts and pictures
    void Reset()
    {
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
        smallFont = regularFont;
        monsterSheets = LoadFolder("Assets/Game/graphics/monsters");
        backgrounds = LoadFolder("Assets/Game/graphics/backgrounds");
    }

    static Texture2D[] LoadFolder(string folder)
    {
        var textures = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            textures.Add(OverworldAnimations.LoadPixelArt(AssetDatabase.GUIDToAssetPath(guid)));
        return textures.ToArray();
    }
#endif
}
