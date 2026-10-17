using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of MonsterIndex in monster_index.py, opened with Return like Game.input in main.py.
// One per scene: add it to the Game object. The pygame drawing code is ported function by function,
// with the same 1280x720 pixel coordinates (see UiKit).
public class MonsterIndex : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f; // settings.py
    const float AnimationSpeed = 6f;

    [SerializeField] Font regularFont;   // fonts['regular']: PixeloidSans, 18
    [SerializeField] Font boldFont;      // fonts['bold']: dogicapixelbold, 20
    [SerializeField] Texture2D[] monsterSheets;   // graphics/monsters: 4 x 2 frames each (idle row, attack row)
    [SerializeField] Texture2D[] monsterIcons;    // graphics/icons
    [SerializeField] Texture2D[] uiIcons;         // graphics/ui: health, energy, attack, defense, speed, recovery

    // COLORS in settings.py
    static readonly Color White = UiKit.Hex("#f4fefa"), Dark = UiKit.Hex("#2b292c"), Light = UiKit.Hex("#c8c8c8"),
        Gray = UiKit.Hex("#3a373b"), Gold = UiKit.Hex("#ffd700"), LightGray = UiKit.Hex("#4b484d"),
        Red = UiKit.Hex("#f03131"), Blue = UiKit.Hex("#66d7ee"), Black = Color.black;

    static readonly Dictionary<string, Color> ElementColors = new Dictionary<string, Color>
    {
        { "fire", UiKit.Hex("#f8a060") }, { "water", UiKit.Hex("#50b0d8") }, { "plant", UiKit.Hex("#64a990") }, { "normal", Color.white },
    };

    UiFont regular, bold;
    Dictionary<string, Sprite[]> idleFrames = new Dictionary<string, Sprite[]>();
    Dictionary<string, Sprite> iconSprites = new Dictionary<string, Sprite>();
    Dictionary<string, Sprite> uiSprites = new Dictionary<string, Sprite>();

    PlayerController player;
    DialogSystem dialogSystem;
    BattleSystem battleSystem;
    GameObject canvasObject;
    RectTransform content;       // rebuilt whenever something changes
    Image monsterImage;          // the animated monster, updated every frame
    Sprite[] currentFrames;

    // dimensions
    Rect mainRect;
    float listWidth, itemHeight;
    const int VisibleItems = 6;

    // state
    readonly Dictionary<string, float> maxStats = new Dictionary<string, float>();
    int index;
    int? selectedIndex;
    float frameIndex;
    bool open;
    bool dirty;

    public bool IsOpen => open;

    List<Monster> Monsters => PlayerParty.Monsters;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        dialogSystem = FindFirstObjectByType<DialogSystem>();

        regular = new UiFont(regularFont, 18, 23);
        bold = new UiFont(boldFont, 20, 18);

        foreach (var texture in monsterSheets) idleFrames[texture.name] = CutIdleFrames(texture);
        foreach (var texture in monsterIcons) iconSprites[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));
        foreach (var texture in uiIcons) uiSprites[texture.name] = UiKit.SpriteFrom(texture, new Rect(0, 0, texture.width, texture.height));

        // dimensions
        mainRect = UiKit.Place(Anchor.Center, WindowWidth / 2f, WindowHeight / 2f, WindowWidth * 0.6f, WindowHeight * 0.8f);
        listWidth = mainRect.width * 0.3f;
        itemHeight = mainRect.height / VisibleItems;

        // max values: the best of every stat over all monsters
        foreach (var data in MonsterDatabase.Monsters.Values)
        {
            Max("health", data.maxHealth); Max("energy", data.maxEnergy); Max("attack", data.attack);
            Max("defense", data.defense); Max("speed", data.speed); Max("recovery", data.recovery);
        }

        BuildCanvas();
    }

    void Max(string stat, float value)
    {
        if (!maxStats.TryGetValue(stat, out var current) || value > current) maxStats[stat] = value;
    }

    // monster_importer(4, 2, ...): the first row of the sheet is the idle animation
    static Sprite[] CutIdleFrames(Texture2D sheet)
    {
        float cellWidth = sheet.width / 4f, cellHeight = sheet.height / 2f;
        var frames = new Sprite[4];
        for (int col = 0; col < 4; col++)
            frames[col] = UiKit.SpriteFrom(sheet, new Rect(col * cellWidth, sheet.height - cellHeight, cellWidth, cellHeight));
        return frames;
    }

    void BuildCanvas()
    {
        canvasObject = new GameObject("Monster Index");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // under the screen fade (1000)

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // the world camera keeps its height, so the UI does too

        // tint_surf: the whole screen, black, alpha 200
        var tint = new GameObject("Tint", typeof(RectTransform)).AddComponent<Image>();
        tint.transform.SetParent(canvasObject.transform, false);
        tint.color = new Color(0, 0, 0, 200f / 255f);
        tint.raycastTarget = false;
        var tintRect = tint.rectTransform;
        tintRect.anchorMin = Vector2.zero;
        tintRect.anchorMax = Vector2.one;
        tintRect.offsetMin = tintRect.offsetMax = Vector2.zero;

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
        if (keyboard == null) return;

        // Game.input: Return opens and closes the index (not during a dialog or a battle)
        if (battleSystem == null) battleSystem = FindFirstObjectByType<BattleSystem>();
        if (keyboard.enterKey.wasPressedThisFrame && (dialogSystem == null || !dialogSystem.Active) && (battleSystem == null || !battleSystem.Active))
        {
            open = !open;
            player.blocked = !player.blocked; // exactly like main.py: it flips the flag, it does not stop the walk animation
            canvasObject.SetActive(open);
            dirty = true;
        }

        if (!open) return;

        // MonsterIndex.input
        if (keyboard.upArrowKey.wasPressedThisFrame) { index -= 1; dirty = true; }
        if (keyboard.downArrowKey.wasPressedThisFrame) { index += 1; dirty = true; }
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (selectedIndex != null)
            {
                // swap the selected monster with the one under the cursor
                var selected = Monsters[selectedIndex.Value];
                var current = Monsters[index];
                Monsters[index] = selected;
                Monsters[selectedIndex.Value] = current;
                selectedIndex = null;
            }
            else
            {
                selectedIndex = index;
            }
            dirty = true;
        }

        index = ((index % Monsters.Count) + Monsters.Count) % Monsters.Count;

        if (dirty)
        {
            dirty = false;
            Rebuild();
        }

        // monster animation
        frameIndex += AnimationSpeed * Time.deltaTime;
        if (monsterImage != null) monsterImage.sprite = currentFrames[(int)frameIndex % currentFrames.Length];
    }

    void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        monsterImage = null;

        DisplayList();
        DisplayMain();
    }

    // display_list
    void DisplayList()
    {
        UiKit.Box(content, mainRect.x, mainRect.y, listWidth, mainRect.height, Gray, 12, Corners.Left);

        float vOffset = index < VisibleItems ? 0 : -(index - VisibleItems + 1) * itemHeight;
        for (int i = 0; i < Monsters.Count; i++)
        {
            var monster = Monsters[i];

            // colors
            Color bgColor = index != i ? Gray : Light;
            Color textColor = selectedIndex != i ? White : Gold;

            float top = mainRect.y + i * itemHeight + vOffset;
            var itemRect = new Rect(mainRect.x, top, listWidth, itemHeight);

            // only items that touch the list area are drawn (colliderect: touching edges do not count)
            if (!(itemRect.xMax > mainRect.xMin && itemRect.xMin < mainRect.xMax && itemRect.yMax > mainRect.yMin && itemRect.yMin < mainRect.yMax)) continue;

            // check corners
            if (Contains(itemRect, mainRect.xMin, mainRect.yMin))
                UiKit.Box(content, itemRect.x, itemRect.y, itemRect.width, itemRect.height, bgColor, 12, Corners.TopLeft);
            else if (Contains(itemRect, mainRect.xMin + 1, mainRect.yMax - 1))
                UiKit.Box(content, itemRect.x, itemRect.y, itemRect.width, itemRect.height, bgColor, 12, Corners.BottomLeft);
            else
                UiKit.Box(content, itemRect.x, itemRect.y, itemRect.width, itemRect.height, bgColor);

            float middle = itemRect.y + itemRect.height / 2f;
            UiKit.Label(content, monster.name, regular, textColor, Anchor.MidLeft, itemRect.x + 90, middle);
            UiKit.Picture(content, iconSprites[monster.name], Anchor.Center, itemRect.x + 45, middle);
        }

        // lines
        for (int i = 1; i < Mathf.Min(VisibleItems, Monsters.Count); i++)
            UiKit.Box(content, mainRect.x, mainRect.y + itemHeight * i, listWidth, 1, LightGray);

        // shadow
        UiKit.Box(content, mainRect.x + listWidth - 4, mainRect.y, 4, mainRect.height, new Color(0, 0, 0, 100f / 255f));
    }

    // pygame's Rect.collidepoint: left and top edges inside, right and bottom outside
    static bool Contains(Rect rect, float x, float y)
    {
        return x >= rect.xMin && x < rect.xMax && y >= rect.yMin && y < rect.yMax;
    }

    // display_main
    void DisplayMain()
    {
        // data
        var monster = Monsters[index];

        // main bg
        var rect = new Rect(mainRect.x + listWidth, mainRect.y, mainRect.width - listWidth, mainRect.height);
        UiKit.Box(content, rect.x, rect.y, rect.width, rect.height, Dark, 12, Corners.Right);

        // monster display
        var topRect = new Rect(rect.x, rect.y, rect.width, rect.height * 0.4f);
        UiKit.Box(content, topRect.x, topRect.y, topRect.width, topRect.height, ElementColors[monster.element], 12, Corners.TopRight);

        // monster animation (the frame itself is switched every frame in Update)
        currentFrames = idleFrames[monster.name];
        monsterImage = UiKit.Picture(content, currentFrames[(int)frameIndex % currentFrames.Length], Anchor.Center, topRect.center.x, topRect.center.y);

        // name
        UiKit.Label(content, monster.name, bold, White, Anchor.TopLeft, topRect.xMin + 10, topRect.yMin + 10);

        // level
        var levelRect = UiKit.Label(content, $"Lvl: {monster.level}", regular, White, Anchor.BottomLeft, topRect.xMin + 10, topRect.yMax - 16);
        UiKit.Bar(content, levelRect.xMin, levelRect.yMax, 100, 4, monster.xp, monster.levelUp, White, Dark);

        // element
        UiKit.Label(content, monster.element, regular, White, Anchor.BottomRight, topRect.xMax - 10, topRect.yMax - 10);

        // health and energy
        float barWidth = rect.width * 0.45f, barHeight = 30f;
        float barTop = topRect.yMax + rect.width * 0.03f;
        float leftSide = rect.xMin + rect.width / 4f, rightSide = rect.xMin + rect.width * 3f / 4f;

        var healthBar = UiKit.Place(Anchor.MidTop, leftSide, barTop, barWidth, barHeight);
        UiKit.Bar(content, healthBar.x, healthBar.y, healthBar.width, healthBar.height, monster.health, monster.GetStat("max_health"), Red, Black, 2);
        UiKit.Label(content, $"HP: {(int)monster.health}/{(int)monster.GetStat("max_health")}", regular, White, Anchor.MidLeft, healthBar.xMin + 10, healthBar.center.y);

        var energyBar = UiKit.Place(Anchor.MidTop, rightSide, barTop, barWidth, barHeight);
        UiKit.Bar(content, energyBar.x, energyBar.y, energyBar.width, energyBar.height, monster.energy, monster.GetStat("max_energy"), Blue, Black, 2);
        UiKit.Label(content, $"EP: {(int)monster.energy}/{(int)monster.GetStat("max_energy")}", regular, White, Anchor.MidLeft, energyBar.xMin + 10, energyBar.center.y);

        // info
        float leftEdge = healthBar.xMin, rightEdge = energyBar.xMin;
        float infoHeight = rect.yMax - healthBar.yMax;

        // stats: FRect(...).inflate(0, -60).move(0, 15)
        var statsRect = new Rect(leftEdge, healthBar.yMax + 30 + 15, healthBar.width, infoHeight - 60);
        UiKit.Label(content, "Stats", regular, White, Anchor.BottomLeft, statsRect.xMin, statsRect.yMin);

        var monsterStats = monster.GetStats();
        float statHeight = statsRect.height / monsterStats.Count;

        for (int i = 0; i < monsterStats.Count; i++)
        {
            string stat = monsterStats[i].Key;
            float value = monsterStats[i].Value;
            var singleStatRect = new Rect(statsRect.xMin, statsRect.yMin + i * statHeight, statsRect.width, statHeight);

            // icon
            var icon = uiSprites[stat];
            var iconRect = UiKit.Place(Anchor.MidLeft, singleStatRect.xMin + 5, singleStatRect.center.y, icon.rect.width, icon.rect.height);
            UiKit.Picture(content, icon, Anchor.MidLeft, singleStatRect.xMin + 5, singleStatRect.center.y);

            // text
            var textRect = UiKit.Label(content, stat, regular, White, Anchor.TopLeft, iconRect.xMin + 30, iconRect.yMin - 10);

            // bar
            UiKit.Bar(content, textRect.xMin, textRect.yMax + 2, singleStatRect.width - (textRect.xMin - singleStatRect.xMin), 4,
                value, maxStats[stat] * monster.level, White, Black);
        }

        // abilities
        var abilityRect = new Rect(rightEdge, statsRect.yMin, statsRect.width, statsRect.height);
        UiKit.Label(content, "Ability", regular, White, Anchor.BottomLeft, abilityRect.xMin, abilityRect.yMin);

        var abilities = monster.GetAbilities();
        for (int i = 0; i < abilities.Count; i++)
        {
            string element = MonsterDatabase.Attacks[abilities[i]].element;
            float x = abilityRect.xMin + i % 2 * abilityRect.width / 2f;
            float y = 20 + abilityRect.yMin + (i / 2) * (regular.surfaceHeight + 20);

            // rect.inflate(10, 10) behind the text, radius 4
            float textWidth = UiKit.Measure(regular, abilities[i]);
            UiKit.Box(content, x - 5, y - 5, textWidth + 10, regular.surfaceHeight + 10, ElementColors[element], 4);
            UiKit.Label(content, abilities[i], regular, Black, Anchor.TopLeft, x, y);
        }
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills in the fonts and pictures
    void Reset()
    {
        regularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
        boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/dogicapixelbold.otf");
        monsterSheets = LoadFolder("Assets/Game/graphics/monsters");
        monsterIcons = LoadFolder("Assets/Game/graphics/icons");
        uiIcons = LoadFolder("Assets/Game/graphics/ui");
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
