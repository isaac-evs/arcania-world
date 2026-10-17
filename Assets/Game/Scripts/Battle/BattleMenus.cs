using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The menus of Battle: draw_general, draw_attacks and draw_switch in battle.py, and the cross of a failed catch (TimedSprite).
// They are rebuilt only when something they show changes.
public partial class Battle
{
    // BATTLE_CHOICES['full'] in settings.py
    static readonly (string option, Vector2 pos, string icon)[] Choices =
    {
        ("fight", new Vector2(30, -60), "sword"),
        ("defend", new Vector2(40, -20), "shield"),
        ("switch", new Vector2(40, 20), "arrows"),
        ("catch", new Vector2(30, 60), "hand"),
    };

    static readonly Color MenuWhite = UiKit.Hex("#f4fefa"), DarkWhite = UiKit.Hex("#f0f0f0"), Light = UiKit.Hex("#c8c8c8"),
        MenuRed = UiKit.Hex("#f03131"), MenuBlue = UiKit.Hex("#66d7ee");

    static readonly Dictionary<string, Color> ElementColors = new Dictionary<string, Color>
    {
        { "fire", UiKit.Hex("#f8a060") }, { "water", UiKit.Hex("#50b0d8") }, { "plant", UiKit.Hex("#64a990") },
    };

    const Corners TopCorners = Corners.TopLeft | Corners.TopRight;
    const Corners BottomCorners = Corners.BottomLeft | Corners.BottomRight;

    string menuSignature;

    // the cross shown for a second on a monster that is too healthy to catch
    readonly List<(GameObject cross, Countdown timer)> crosses = new List<(GameObject, Countdown)>();

    // draw_ui: rebuild the menu when what it shows has changed
    void UpdateMenu()
    {
        string signature = MenuSignature();
        if (signature == menuSignature) return;
        menuSignature = signature;

        for (int i = layerMenu.childCount - 1; i >= 0; i--) Object.Destroy(layerMenu.GetChild(i).gameObject);
        if (currentMonster == null || currentMonster.removed) return;

        if (selectionMode == Mode.General) DrawGeneral();
        if (selectionMode == Mode.Attacks) DrawAttacks();
        if (selectionMode == Mode.Switch) DrawSwitch();
    }

    string MenuSignature()
    {
        if (currentMonster == null || currentMonster.removed || selectionMode == Mode.None) return "none";

        string signature = $"{selectionMode}:{indexes[(int)selectionMode]}:{currentMonster.GetHashCode()}";
        if (selectionMode == Mode.Attacks) signature += ":" + string.Join(",", currentMonster.monster.GetAbilities(all: false));
        if (selectionMode == Mode.Switch)
        {
            foreach (var (index, monster) in AvailableMonsters())
                signature += $":{index}/{monster.level}/{(int)monster.health}/{(int)monster.energy}";
        }
        return signature;
    }

    // draw_general: the four choices next to the active monster, the selected one in colour and the rest in grey
    void DrawGeneral()
    {
        for (int i = 0; i < Choices.Length; i++)
        {
            var choice = Choices[i];
            Sprite icon = i == indexes[(int)Mode.General] ? assets.ui[choice.icon + "_highlight"] : assets.Gray(assets.ui[choice.icon]);
            UiKit.Picture(layerMenu, icon, Anchor.Center, currentMonster.rect.xMax + choice.pos.x, currentMonster.rect.center.y + choice.pos.y);
        }
    }

    // draw_attacks
    void DrawAttacks()
    {
        var abilities = currentMonster.monster.GetAbilities(all: false);
        const float width = 150, height = 200;
        const int visibleAttacks = 4;
        const float itemHeight = height / visibleAttacks;
        int selectedIndex = indexes[(int)Mode.Attacks];
        float vOffset = selectedIndex < visibleAttacks ? 0 : -(selectedIndex - visibleAttacks + 1) * itemHeight;

        // bg
        var bg = UiKit.Place(Anchor.MidLeft, currentMonster.rect.xMax + 20, currentMonster.rect.center.y, width, height);
        UiKit.Box(layerMenu, bg.x, bg.y, bg.width, bg.height, MenuWhite, 5);

        for (int i = 0; i < abilities.Count; i++)
        {
            bool selected = i == selectedIndex;

            // text
            Color textColor = Light;
            if (selected)
            {
                string element = MonsterDatabase.Attacks[abilities[i]].element;
                textColor = element != "normal" ? ElementColors[element] : Color.black;
            }

            // rect
            float centerX = bg.center.x, centerY = bg.yMin + itemHeight / 2f + i * itemHeight + vOffset;
            var textBg = UiKit.Place(Anchor.Center, centerX, centerY, width, itemHeight);

            // draw
            if (!Contains(bg, centerX, centerY)) continue;
            if (selected)
            {
                if (Contains(textBg, bg.xMin, bg.yMin)) UiKit.Box(layerMenu, textBg.x, textBg.y, textBg.width, textBg.height, DarkWhite, 5, TopCorners);
                else if (Contains(textBg, bg.center.x, bg.yMax - 1)) UiKit.Box(layerMenu, textBg.x, textBg.y, textBg.width, textBg.height, DarkWhite, 5, BottomCorners);
                else UiKit.Box(layerMenu, textBg.x, textBg.y, textBg.width, textBg.height, DarkWhite);
            }
            UiKit.Label(layerMenu, abilities[i], assets.regular, textColor, Anchor.Center, centerX, centerY);
        }
    }

    // draw_switch
    void DrawSwitch()
    {
        const float width = 300, height = 320;
        const int visibleMonsters = 4;
        const float itemHeight = height / visibleMonsters;
        int selectedIndex = indexes[(int)Mode.Switch];
        float vOffset = selectedIndex < visibleMonsters ? 0 : -(selectedIndex - visibleMonsters + 1) * itemHeight;

        var bg = UiKit.Place(Anchor.MidLeft, currentMonster.rect.xMax + 20, currentMonster.rect.center.y, width, height);
        UiKit.Box(layerMenu, bg.x, bg.y, bg.width, bg.height, MenuWhite, 5);

        // monsters
        var available = AvailableMonsters();
        for (int i = 0; i < available.Count; i++)
        {
            var monster = available[i].monster;
            bool selected = i == selectedIndex;
            float middleY = bg.yMin + itemHeight / 2f + i * itemHeight + vOffset;
            var itemBg = UiKit.Place(Anchor.MidLeft, bg.xMin, middleY, width, itemHeight);

            var icon = assets.icons[monster.name];
            var iconRect = UiKit.Place(Anchor.MidLeft, bg.xMin + 10, middleY, icon.rect.width, icon.rect.height);
            string label = $"{monster.name} ({monster.level})";
            var textRect = UiKit.Place(Anchor.TopLeft, bg.xMin + 90, iconRect.yMin, UiKit.Measure(assets.regular, label), assets.regular.surfaceHeight);

            // selection bg
            if (selected)
            {
                if (Contains(itemBg, bg.xMin, bg.yMin)) UiKit.Box(layerMenu, itemBg.x, itemBg.y, itemBg.width, itemBg.height, DarkWhite, 5, TopCorners);
                else if (Contains(itemBg, bg.center.x, bg.yMax - 1)) UiKit.Box(layerMenu, itemBg.x, itemBg.y, itemBg.width, itemBg.height, DarkWhite, 5, BottomCorners);
                else UiKit.Box(layerMenu, itemBg.x, itemBg.y, itemBg.width, itemBg.height, DarkWhite);
            }

            if (!Contains(bg, itemBg.center.x, itemBg.center.y)) continue;
            UiKit.Picture(layerMenu, icon, Anchor.MidLeft, bg.xMin + 10, middleY);
            UiKit.Label(layerMenu, label, assets.regular, selected ? MenuRed : Color.black, Anchor.TopLeft, textRect.xMin, textRect.yMin);

            float healthY = textRect.yMax + 4;
            UiKit.Bar(layerMenu, textRect.xMin, healthY, 100, 4, monster.health, monster.GetStat("max_health"), MenuRed, Color.black);
            UiKit.Bar(layerMenu, textRect.xMin, healthY + 4 + 2, 80, 4, monster.energy, monster.GetStat("max_energy"), MenuBlue, Color.black);
        }
    }

    // pygame's Rect.collidepoint: left and top edges inside, right and bottom outside
    static bool Contains(Rect rect, float x, float y)
    {
        return x >= rect.xMin && x < rect.xMax && y >= rect.yMin && y < rect.yMax;
    }

    // TimedSprite(monster_sprite.rect.center, ui['cross'], ..., 1000)
    void ShowCross(BattleMonster target)
    {
        int before = layerOverlay.childCount;
        UiKit.Picture(layerOverlay, assets.ui["cross"], Anchor.Center, target.rect.center.x, target.rect.center.y);
        var cross = layerOverlay.GetChild(before).gameObject;

        var timer = new Countdown(1f, () => Object.Destroy(cross));
        timer.Activate();
        crosses.Add((cross, timer));
    }

    void UpdateCrosses(float dt)
    {
        for (int i = crosses.Count - 1; i >= 0; i--)
        {
            crosses[i].timer.Update(dt);
            if (!crosses[i].timer.active) crosses.RemoveAt(i);
        }
    }

    // What is said when you try to catch a monster that belongs to another trainer
    static readonly string[] StolenMessages =
    {
        "Hey! What are you doing?",
        "It's not yours!",
        "Don't be a thief!",
    };

    const float MessageTime = 2f;
    GameObject message;
    Countdown messageTimer;

    // A bubble in the style of the dialogue boxes (white, rounded, black text) at the top of the screen for a couple of seconds
    void ShowMessage(string text)
    {
        if (message != null) Object.Destroy(message);

        var font = new UiFont(assets.regular.font, 30, 38); // the dialogue font and size
        float width = UiKit.Measure(font, text) + 28;
        int before = layerMessage.childCount;
        UiKit.Box(layerMessage, (1280f - width) / 2f, 36, width, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(layerMessage, text, font, Color.black, Anchor.MidTop, 1280f / 2f, 36 + 14);

        // both pieces are removed together
        var holder = new GameObject("Message", typeof(RectTransform));
        var holderRect = (RectTransform)holder.transform;
        holderRect.SetParent(layerMessage, false);
        holderRect.anchorMin = Vector2.zero;
        holderRect.anchorMax = Vector2.one;
        holderRect.offsetMin = holderRect.offsetMax = Vector2.zero;
        while (layerMessage.childCount > before + 1)
            layerMessage.GetChild(before).SetParent(holderRect, false);

        message = holder;
        messageTimer = new Countdown(MessageTime, () => { if (message != null) Object.Destroy(message); message = null; });
        messageTimer.Activate();
    }

    void UpdateMessage(float dt)
    {
        messageTimer?.Update(dt);
    }
}
