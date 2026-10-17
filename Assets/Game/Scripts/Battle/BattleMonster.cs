using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A monster on the battlefield: MonsterSprite, MonsterOutlineSprite, MonsterNameSprite, MonsterLevelSprite and MonsterStatsSprite
// from sprites.py, kept together because they live and die together.
public class BattleMonster
{
    const float AnimationSpeed = 6f; // ANIMATION_SPEED in settings.py

    static readonly Color White = UiKit.Hex("#f4fefa"), Gray = UiKit.Hex("#3a373b"), Red = UiKit.Hex("#f03131"),
        Blue = UiKit.Hex("#66d7ee"), Black = Color.black;

    public struct NextMonster
    {
        public Monster monster;
        public int index, posIndex;
        public string entity;
    }

    readonly Battle battle;
    public readonly Monster monster;
    public readonly int index, posIndex;
    public readonly string entity;
    public bool IsPlayer => entity == "player";

    public Rect rect;
    public bool removed;

    // data
    string state = "idle";
    float frameIndex;
    int adjustedFrameIndex;
    bool highlight;
    BattleMonster targetSprite;
    string currentAttack;
    NextMonster? nextMonsterData;
    readonly MonsterFrames frames;

    // timers
    readonly Countdown removeHighlight, kill;

    // what is drawn
    Image image, outline;
    readonly List<GameObject> parts = new List<GameObject>();
    Text levelText, healthText, energyText;
    Image xpFill, healthFill, energyFill, initiativeFill;

    const float StatsWidth = 150f, StatsBarWidth = 135f, LevelWidth = 60f;

    public BattleMonster(Battle battle, Monster monster, int index, int posIndex, string entity)
    {
        this.battle = battle;
        this.monster = monster;
        this.index = index;
        this.posIndex = posIndex;
        this.entity = entity;

        frames = battle.assets.GetFrames(monster.name);
        removeHighlight = new Countdown(0.3f, () => SetHighlight(false));
        kill = new Countdown(0.6f, Destroy);

        // the player's monsters face right: their frames are mirrored
        Vector2 pos = IsPlayer ? Battle.LeftPositions[posIndex] : Battle.RightPositions[posIndex];
        rect = UiKit.Place(Anchor.Center, pos.x, pos.y, frames.idle[0].rect.width, frames.idle[0].rect.height);

        image = Track(battle.layerMonster, () => UiKit.Picture(battle.layerMonster, frames.idle[0], Anchor.Center, pos.x, pos.y, flipX: IsPlayer));
        outline = Track(battle.layerOutline, () => UiKit.Picture(battle.layerOutline, frames.idleOutline[0], Anchor.Center, pos.x, pos.y, flipX: IsPlayer));
        outline.gameObject.SetActive(false);

        // ui
        Vector2 namePos = IsPlayer ? new Vector2(rect.xMin + 16, rect.center.y - 70) : new Vector2(rect.xMax - 40, rect.center.y - 70);
        Rect nameRect = default;
        Track<object>(battle.layerName, () => { nameRect = NameSprite(namePos); return null; });
        Vector2 levelPos = IsPlayer ? new Vector2(nameRect.xMin, nameRect.yMax) : new Vector2(nameRect.xMax, nameRect.yMax);
        Track<object>(battle.layerName, () => { LevelSprite(levelPos); return null; });
        Track<object>(battle.layerOverlay, () => { StatsSprite(new Vector2(rect.center.x, rect.yMax + 20)); return null; });
    }

    // runs build and remembers every object it added to the layer, so they can all be removed with the monster
    T Track<T>(Transform layer, System.Func<T> build) where T : class
    {
        int before = layer.childCount;
        T result = build();
        for (int i = before; i < layer.childCount; i++) parts.Add(layer.GetChild(i).gameObject);
        return result;
    }

    // MonsterNameSprite: white box with the name, midtop at pos
    Rect NameSprite(Vector2 pos)
    {
        const int padding = 10;
        float width = UiKit.Measure(battle.assets.regular, monster.name) + padding * 2;
        float height = battle.assets.regular.surfaceHeight + padding * 2;
        var box = UiKit.Place(Anchor.MidTop, pos.x, pos.y, width, height);

        UiKit.Box(battle.layerName, box.x, box.y, box.width, box.height, White);
        UiKit.Label(battle.layerName, monster.name, battle.assets.regular, Black, Anchor.TopLeft, box.x + padding, box.y + padding);
        return box;
    }

    // MonsterLevelSprite: 60 x 26 box with "Lvl n" and the xp line along the bottom
    void LevelSprite(Vector2 pos)
    {
        var box = UiKit.Place(IsPlayer ? Anchor.TopLeft : Anchor.TopRight, pos.x, pos.y, LevelWidth, 26);

        UiKit.Box(battle.layerName, box.x, box.y, box.width, box.height, White);
        UiKit.Label(battle.layerName, $"Lvl {monster.level}", battle.assets.small, Black, Anchor.Center, box.x + box.width / 2f, box.y + box.height / 2f, out levelText);
        xpFill = UiKit.Bar(battle.layerName, box.x, box.y + box.height - 2, box.width, 2, monster.xp, monster.levelUp, Black, White, 0);
    }

    // MonsterStatsSprite: 150 x 48 box with health, energy and the initiative line, midbottom at pos
    void StatsSprite(Vector2 pos)
    {
        var box = UiKit.Place(Anchor.MidBottom, pos.x, pos.y, StatsWidth, 48);
        UiKit.Box(battle.layerOverlay, box.x, box.y, box.width, box.height, White);

        var info = monster.GetInfo();
        for (int i = 0; i < 3; i++)
        {
            var (value, maxValue) = info[i];
            if (i < 2) // health and energy
            {
                var textRect = UiKit.Label(battle.layerOverlay, $"{(int)value}/{Python(maxValue)}", battle.assets.small, Black, Anchor.TopLeft,
                    box.x + box.width * 0.05f, box.y + i * box.height / 2f, out var text);
                var fill = UiKit.Bar(battle.layerOverlay, textRect.xMin, textRect.yMax - 2, StatsBarWidth, 4, value, maxValue, i == 0 ? Red : Blue, Black, 2);
                if (i == 0) { healthText = text; healthFill = fill; }
                else { energyText = text; energyFill = fill; }
            }
            else // initiative
            {
                initiativeFill = UiKit.Bar(battle.layerOverlay, box.x, box.y + box.height - 2, box.width, 2, value, maxValue, Gray, White, 0);
            }
        }
    }

    // f'{max_value}' prints whole numbers without decimals
    static string Python(float value)
    {
        return value == Mathf.Floor(value) ? ((int)value).ToString() : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    // The sprites of Group.update: timers, animation, then the monster itself (initiative)
    public void Update(float dt)
    {
        removeHighlight.Update(dt);
        kill.Update(dt);
        if (removed) return;

        Animate(dt);
        monster.Update(dt);
        RefreshUi();
    }

    void Animate(float dt)
    {
        frameIndex += AnimationSpeed * dt;
        if (state == "attack" && frameIndex >= frames.attack.Length)
        {
            battle.ApplyAttack(targetSprite, currentAttack, monster.GetBaseDamage(currentAttack));
            state = "idle";
            if (removed) return;
        }

        var current = frames.Frames(state);
        adjustedFrameIndex = (int)(frameIndex % current.Length);
        image.sprite = highlight ? frames.White(state)[adjustedFrameIndex] : current[adjustedFrameIndex];
        outline.sprite = frames.Outlines(state)[adjustedFrameIndex];
    }

    // the name, level and stats boxes show the monster's current values
    void RefreshUi()
    {
        levelText.text = $"Lvl {monster.level}";
        SetWidth(xpFill, UiKit.BarProgress(LevelWidth, monster.xp, monster.levelUp));

        var info = monster.GetInfo();
        healthText.text = $"{(int)info[0].Item1}/{Python(info[0].Item2)}";
        SetWidth(healthFill, UiKit.BarProgress(StatsBarWidth, info[0].Item1, info[0].Item2));
        energyText.text = $"{(int)info[1].Item1}/{Python(info[1].Item2)}";
        SetWidth(energyFill, UiKit.BarProgress(StatsBarWidth, info[1].Item1, info[1].Item2));
        SetWidth(initiativeFill, UiKit.BarProgress(StatsWidth, info[2].Item1, info[2].Item2));
    }

    static void SetWidth(Image fill, float width)
    {
        var size = fill.rectTransform.sizeDelta;
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Floor(width), size.y);
    }

    public void SetHighlight(bool value)
    {
        highlight = value;
        if (value) removeHighlight.Activate();
    }

    public void SetOutline(bool visible)
    {
        if (!removed) outline.gameObject.SetActive(visible);
    }

    public void ActivateAttack(BattleMonster target, string attack)
    {
        state = "attack";
        frameIndex = 0;
        targetSprite = target;
        currentAttack = attack;
        monster.ReduceEnergy(attack);
    }

    public void DelayedKill(NextMonster? next)
    {
        if (!kill.active)
        {
            nextMonsterData = next;
            kill.Activate();
        }
    }

    void Destroy()
    {
        if (nextMonsterData.HasValue)
        {
            var next = nextMonsterData.Value;
            battle.CreateMonster(next.monster, next.index, next.posIndex, next.entity);
        }
        Remove();
    }

    // sprite.kill(): the monster and everything drawn for it disappear
    public void Remove()
    {
        if (removed) return;
        removed = true;
        battle.RemoveSprite(this);
        foreach (var part in parts)
        {
            if (part != null) Object.Destroy(part);
        }
    }
}
