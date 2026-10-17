using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Port of the setup part of Battle in battle.py (and the battle sprites in sprites.py).
// For now it only places the monsters and their name, level and stats boxes; there are no actions yet.
// Everything is drawn with pygame coordinates in the 1280x720 panel (see UiKit).
public class Battle
{
    // BATTLE_POSITIONS in settings.py, in the order of list(...values()): top, center, bottom
    static readonly Vector2[] LeftPositions = { new Vector2(360, 260), new Vector2(190, 400), new Vector2(410, 520) };
    static readonly Vector2[] RightPositions = { new Vector2(900, 260), new Vector2(1110, 390), new Vector2(900, 550) };

    const float AnimationSpeed = 6f; // ANIMATION_SPEED in settings.py

    static readonly Color White = UiKit.Hex("#f4fefa"), Gray = UiKit.Hex("#3a373b"), Red = UiKit.Hex("#f03131"),
        Blue = UiKit.Hex("#66d7ee"), Black = Color.black;

    // MonsterSprite
    public class MonsterSprite
    {
        public Monster monster;
        public int index, posIndex;
        public string entity;
        public Rect rect;
        public Image image;
        public Sprite[] frames;
        public float frameIndex;
    }

    // everything the screen needs from the game: the pictures, the fonts and the monster names to frames
    public class Assets
    {
        public Sprite background;
        public Dictionary<string, Sprite[]> idleFrames;
        public UiFont regular, small;
    }

    public readonly GameObject root;
    public readonly Character character;   // the trainer, null in a wild fight
    public readonly List<MonsterSprite> playerSprites = new List<MonsterSprite>();
    public readonly List<MonsterSprite> opponentSprites = new List<MonsterSprite>();

    // monster_data: what is left to send into the fight
    readonly List<Monster> playerData;
    readonly SortedDictionary<int, Monster> opponentData;
    readonly Assets assets;

    // BATTLE_LAYERS: outline, name, monster, effects, overlay (drawn in this order)
    readonly Transform layerOutline, layerName, layerMonster, layerEffects, layerOverlay;

    public Battle(Transform panel, Assets assets, List<Monster> playerMonsters, SortedDictionary<int, Monster> opponentMonsters, Character character)
    {
        this.assets = assets;
        playerData = playerMonsters;
        opponentData = opponentMonsters;
        this.character = character;

        root = Stretched(panel, "Battle");

        // display_surface.blit(self.bg_surf, (0, 0))
        UiKit.Picture(root.transform, assets.background, Anchor.TopLeft, 0, 0);

        layerOutline = Stretched(root.transform, "Outline").transform;
        layerName = Stretched(root.transform, "Name").transform;
        layerMonster = Stretched(root.transform, "Monster").transform;
        layerEffects = Stretched(root.transform, "Effects").transform;
        layerOverlay = Stretched(root.transform, "Overlay").transform;

        Setup();
    }

    static GameObject Stretched(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect.gameObject;
    }

    // setup
    void Setup()
    {
        // the first three monsters of each side go to the field
        for (int index = 0; index < playerData.Count && index <= 2; index++)
            CreateMonster(playerData[index], index, index, "player");

        foreach (var pair in new List<KeyValuePair<int, Monster>>(opponentData))
        {
            if (pair.Key <= 2) CreateMonster(pair.Value, pair.Key, pair.Key, "opponent");
        }

        // remove opponent monster data: the ones on the field are no longer waiting
        for (int i = 0; i < opponentSprites.Count; i++)
            opponentData.Remove(i);
    }

    void CreateMonster(Monster monster, int index, int posIndex, string entity)
    {
        monster.paused = false;
        bool player = entity == "player";

        var sprite = new MonsterSprite
        {
            monster = monster, index = index, posIndex = posIndex, entity = entity,
            frames = assets.idleFrames[monster.name],
        };

        // the player's monsters face right: their frames are mirrored
        Vector2 pos = player ? LeftPositions[posIndex] : RightPositions[posIndex];
        sprite.image = UiKit.Picture(layerMonster, sprite.frames[0], Anchor.Center, pos.x, pos.y, flipX: player);
        sprite.rect = UiKit.Place(Anchor.Center, pos.x, pos.y, sprite.frames[0].rect.width, sprite.frames[0].rect.height);
        (player ? playerSprites : opponentSprites).Add(sprite);

        // ui
        Vector2 namePos = player ? new Vector2(sprite.rect.xMin + 16, sprite.rect.center.y - 70)
                                 : new Vector2(sprite.rect.xMax - 40, sprite.rect.center.y - 70);
        var nameRect = NameSprite(namePos, sprite);
        Vector2 levelPos = player ? new Vector2(nameRect.xMin, nameRect.yMax) : new Vector2(nameRect.xMax, nameRect.yMax);
        LevelSprite(player, levelPos, sprite);
        StatsSprite(new Vector2(sprite.rect.center.x, sprite.rect.yMax + 20), sprite);
    }

    // MonsterNameSprite: white box with the name, midtop at pos
    Rect NameSprite(Vector2 pos, MonsterSprite sprite)
    {
        const int padding = 10;
        float width = UiKit.Measure(assets.regular, sprite.monster.name) + padding * 2;
        float height = assets.regular.surfaceHeight + padding * 2;
        var rect = UiKit.Place(Anchor.MidTop, pos.x, pos.y, width, height);

        UiKit.Box(layerName, rect.x, rect.y, rect.width, rect.height, White);
        UiKit.Label(layerName, sprite.monster.name, assets.regular, Black, Anchor.TopLeft, rect.x + padding, rect.y + padding);
        return rect;
    }

    // MonsterLevelSprite: 60 x 26 box with "Lvl n" and the xp line along the bottom
    void LevelSprite(bool player, Vector2 pos, MonsterSprite sprite)
    {
        var rect = UiKit.Place(player ? Anchor.TopLeft : Anchor.TopRight, pos.x, pos.y, 60, 26);

        UiKit.Box(layerName, rect.x, rect.y, rect.width, rect.height, White);
        UiKit.Label(layerName, $"Lvl {sprite.monster.level}", assets.small, Black, Anchor.Center, rect.x + rect.width / 2f, rect.y + rect.height / 2f);
        UiKit.Bar(layerName, rect.x, rect.y + rect.height - 2, rect.width, 2, sprite.monster.xp, sprite.monster.levelUp, Black, White, 0);
    }

    // MonsterStatsSprite: 150 x 48 box with health, energy and the initiative line, midbottom at pos
    void StatsSprite(Vector2 pos, MonsterSprite sprite)
    {
        var rect = UiKit.Place(Anchor.MidBottom, pos.x, pos.y, 150, 48);
        UiKit.Box(layerOverlay, rect.x, rect.y, rect.width, rect.height, White);

        var info = sprite.monster.GetInfo();
        Color[] colors = { Red, Blue, Gray };
        for (int i = 0; i < info.Length; i++)
        {
            var (value, maxValue) = info[i];
            if (i < 2) // health and energy
            {
                var textRect = UiKit.Label(layerOverlay, $"{(int)value}/{Python(maxValue)}", assets.small, Black, Anchor.TopLeft,
                    rect.x + rect.width * 0.05f, rect.y + i * rect.height / 2f);
                UiKit.Bar(layerOverlay, textRect.xMin, textRect.yMax - 2, rect.width * 0.9f, 4, value, maxValue, colors[i], Black, 2);
            }
            else // initiative
            {
                UiKit.Bar(layerOverlay, rect.x, rect.y + rect.height - 2, rect.width, 2, value, maxValue, colors[i], White, 0);
            }
        }
    }

    // f'{max_value}' prints whole numbers without decimals
    static string Python(float value)
    {
        return value == Mathf.Floor(value) ? ((int)value).ToString() : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    // MonsterSprite.animate
    public void Update(float dt)
    {
        foreach (var sprite in playerSprites) Animate(sprite, dt);
        foreach (var sprite in opponentSprites) Animate(sprite, dt);
    }

    static void Animate(MonsterSprite sprite, float dt)
    {
        sprite.frameIndex += AnimationSpeed * dt;
        sprite.image.sprite = sprite.frames[(int)(sprite.frameIndex % sprite.frames.Length)];
    }

    public void Destroy()
    {
        Object.Destroy(root);
    }
}
