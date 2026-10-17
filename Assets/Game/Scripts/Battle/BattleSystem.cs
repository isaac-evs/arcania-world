using System.Collections.Generic;
using UnityEngine;
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
    [SerializeField] Texture2D[] monsterSheets;  // graphics/monsters (readable: outlines and silhouettes are made from them)
    [SerializeField] Texture2D[] monsterIcons;   // graphics/icons
    [SerializeField] Texture2D[] uiIcons;        // graphics/ui (readable: the unselected choices are drawn in grey)
    [SerializeField] Texture2D[] attackSheets;   // graphics/attacks: 4 frames each
    [SerializeField] Texture2D[] backgrounds;    // graphics/backgrounds: forest, ice, sand

    PlayerController player;
    DialogSystem dialogSystem;
    EvolutionSystem evolutionSystem;
    ScreenTint tint;
    GameObject canvasObject;
    RectTransform panel;
    Battle.Assets assets;
    readonly Dictionary<string, Sprite> backgroundSprites = new Dictionary<string, Sprite>();

    Battle pending;      // transition_target: built straight away, shown when the screen is black
    Battle battle;       // self.battle

    // encounter_timer = Timer(2000, func = self.monster_encounter)
    float encounterDuration = 2f;
    float encounterElapsed;
    bool encounterActive;

    // True from the moment the battle screen is shown (self.battle in main.py)
    public bool Active => battle != null;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();

        foreach (var texture in backgrounds) backgroundSprites[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));

        assets = new Battle.Assets
        {
            regular = new UiFont(regularFont, 18, 23),
            small = new UiFont(smallFont, 14, 18),
            sheets = new Dictionary<string, Texture2D>(),
            ui = new Dictionary<string, Sprite>(),
            icons = new Dictionary<string, Sprite>(),
            attacks = new Dictionary<string, Sprite[]>(),
        };
        foreach (var sheet in attackSheets) assets.attacks[sheet.name] = UiKit.CutRow(sheet, 4, 1, 0);
        foreach (var sheet in monsterSheets) assets.sheets[sheet.name] = sheet;
        foreach (var texture in uiIcons) assets.ui[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));
        foreach (var texture in monsterIcons) assets.icons[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));

        BuildCanvas();
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Battle");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // over the world, under the monster index (500) and the fade (1000)
        canvas.pixelPerfect = true; // snap the interface to whole screen pixels, so text and edges stay sharp

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

        if (battle != null) battle.Update(dt);
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
        AudioManager.PlayBattleMusic("battle_wild");

        // the monsters are not the ones written on the patch (always the same two): they are rolled from the biome's own pool
        var opponents = WildEncounters.Roll(patch.biome, patch.level);

        Begin(opponents, patch.biome, null);
    }

    // end_dialog, the part for a trainer that is not defeated yet
    public void StartTrainerBattle(Character trainer)
    {
        AudioManager.PlayBattleMusic(trainer.IsGymBoss ? "battle_gym_boss" : "battle_trainer");
        Begin(trainer.monsters, trainer.data.biome, trainer);
    }

    void Begin(SortedDictionary<int, Monster> opponents, string biome, Character trainer)
    {
        // a fight in the open at night happens on the night version of the background
        string backgroundName = DayNightCycle.NightOutdoors && backgroundSprites.ContainsKey(biome + "_night") ? biome + "_night" : biome;
        if (!backgroundSprites.TryGetValue(backgroundName, out var background))
        {
            Debug.LogError($"No battle background named '{biome}'");
            player.Unblock();
            return;
        }

        // the battle is built straight away (like Battle(...) in main.py) but only shown once the screen is black
        var created = new Battle(panel, assets, background, PlayerParty.Monsters, opponents, trainer, EndBattle, nightSky: backgroundName.EndsWith("_night"));
        created.root.name = $"Battle ({backgroundName})";
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
        // the fight music stops and the victory music of this kind of fight plays
        AudioManager.PlayVictory(trainer == null ? "wild_defeated" : trainer.IsGymBoss ? "gym_boss_defeated" : "trainer_defeated");
        if (tint == null) tint = FindFirstObjectByType<ScreenTint>();
        var finished = battle;
        tint.Begin(() =>
        {
            finished.Destroy();
            battle = null;
            canvasObject.SetActive(false);
        });

        if (trainer != null)
        {
            trainer.data.defeated = true;
            MapLoader.AutoSaveNow();
            if (dialogSystem == null) dialogSystem = FindFirstObjectByType<DialogSystem>();
            dialogSystem.CreateDialog(trainer);
        }
        else
        {
            // elif not self.evolution: self.player.unblock(); self.check_evolution()
            if (evolutionSystem == null) evolutionSystem = FindFirstObjectByType<EvolutionSystem>();
            if (evolutionSystem == null || !evolutionSystem.Active)
            {
                player.Unblock();
                if (evolutionSystem != null) evolutionSystem.CheckEvolution();
                else AudioManager.StartMapMusic();
            }
            MapLoader.AutoSaveNow();
        }
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the fonts and pictures
    void Reset()
    {
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
        smallFont = regularFont;
        monsterSheets = LoadFolder("Assets/Game/graphics/monsters", readable: true);
        monsterIcons = LoadFolder("Assets/Game/graphics/icons", readable: false);
        uiIcons = LoadFolder("Assets/Game/graphics/ui", readable: true);
        attackSheets = LoadFolder("Assets/Game/graphics/attacks", readable: false);
        backgrounds = LoadBackgrounds("Assets/Game/graphics/backgrounds");
    }

    // The day backgrounds are pixel art (sharp); the night ones are big pictures that get shrunk to the screen, so they are smoothed
    static Texture2D[] LoadBackgrounds(string folder)
    {
        var textures = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            textures.Add(System.IO.Path.GetFileNameWithoutExtension(path).EndsWith("_night") ? LoadSmooth(path) : OverworldAnimations.LoadPixelArt(path));
        }
        return textures.ToArray();
    }

    static Texture2D LoadSmooth(string path)
    {
        // Sprite type keeps Unity from rescaling the odd 2730-pixel width
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Bilinear))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D[] LoadFolder(string folder, bool readable)
    {
        var textures = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            textures.Add(OverworldAnimations.LoadPixelArt(AssetDatabase.GUIDToAssetPath(guid), readable));
        return textures.ToArray();
    }
#endif
}
