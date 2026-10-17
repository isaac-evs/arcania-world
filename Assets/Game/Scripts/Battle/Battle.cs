using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Port of Battle in battle.py: the field, the turn system (initiative), the menus' input, deaths and the end of the fight.
// Everything is drawn with pygame coordinates in the 1280x720 panel (see UiKit); the menus are in BattleMenus.cs.
public partial class Battle
{
    // BATTLE_POSITIONS in settings.py, in the order of list(...values()): top, center, bottom
    public static readonly Vector2[] LeftPositions = { new Vector2(360, 260), new Vector2(190, 400), new Vector2(410, 520) };
    public static readonly Vector2[] RightPositions = { new Vector2(900, 260), new Vector2(1110, 390), new Vector2(900, 550) };

    public enum Mode { None, General, Attacks, Switch, Target }

    // everything the screen needs from the game: the pictures and the fonts
    public class Assets
    {
        public UiFont regular, small;
        public Dictionary<string, Texture2D> sheets;   // monster sheets by monster name
        public Dictionary<string, Sprite> ui;          // graphics/ui by name
        public Dictionary<string, Sprite> icons;       // graphics/icons by monster name
        public Dictionary<string, Sprite[]> attacks;   // graphics/attacks by animation name: 4 frames each

        readonly Dictionary<string, MonsterFrames> frames = new Dictionary<string, MonsterFrames>();
        readonly Dictionary<Sprite, Sprite> grayscale = new Dictionary<Sprite, Sprite>();

        // cut (and derived) the first time a monster is needed
        public MonsterFrames GetFrames(string monsterName)
        {
            if (!frames.TryGetValue(monsterName, out var result))
                frames[monsterName] = result = MonsterFrames.Build(sheets[monsterName]);
            return result;
        }

        public Sprite Gray(Sprite sprite)
        {
            if (!grayscale.TryGetValue(sprite, out var result))
                grayscale[sprite] = result = MonsterFrames.Grayscale(sprite);
            return result;
        }
    }

    public readonly GameObject root;
    public readonly Assets assets;
    public readonly Character character;   // the trainer, null in a wild fight

    // BATTLE_LAYERS: outline, name, monster, effects, overlay (drawn in this order), then the menus on top
    public readonly Transform layerOutline, layerName, layerMonster, layerEffects, layerOverlay, layerMenu;

    // general
    readonly Action<Character> endBattle;
    bool battleOver;

    // monster_data
    readonly List<Monster> playerData;                    // the player's whole party
    readonly SortedDictionary<int, Monster> opponentData; // the opponent's monsters that are still waiting

    // groups
    public readonly List<BattleMonster> playerSprites = new List<BattleMonster>();
    public readonly List<BattleMonster> opponentSprites = new List<BattleMonster>();

    // control
    BattleMonster currentMonster;
    Mode selectionMode = Mode.None;
    string selectedAttack;
    string selectionSide = "player";
    readonly int[] indexes = new int[5];  // indexed by Mode (general, attacks, switch, target)

    // timers
    readonly Countdown opponentDelay;

    public Battle(Transform panel, Assets assets, Sprite background, List<Monster> playerMonsters, SortedDictionary<int, Monster> opponentMonsters,
        Character character, Action<Character> endBattle)
    {
        this.assets = assets;
        playerData = playerMonsters;
        opponentData = opponentMonsters;
        this.character = character;
        this.endBattle = endBattle;

        opponentDelay = new Countdown(0.6f, OpponentAttack);

        root = Stretched(panel, "Battle");

        // display_surface.blit(self.bg_surf, (0, 0))
        UiKit.Picture(root.transform, background, Anchor.TopLeft, 0, 0);

        layerOutline = Stretched(root.transform, "Outline").transform;
        layerName = Stretched(root.transform, "Name").transform;
        layerMonster = Stretched(root.transform, "Monster").transform;
        layerEffects = Stretched(root.transform, "Effects").transform;
        layerOverlay = Stretched(root.transform, "Overlay").transform;
        layerMenu = Stretched(root.transform, "Menu").transform;

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

    public void CreateMonster(Monster monster, int index, int posIndex, string entity)
    {
        monster.paused = false;
        var sprite = new BattleMonster(this, monster, index, posIndex, entity);
        (entity == "player" ? playerSprites : opponentSprites).Add(sprite);
    }

    public void RemoveSprite(BattleMonster sprite)
    {
        playerSprites.Remove(sprite);
        opponentSprites.Remove(sprite);
    }

    List<BattleMonster> AllSprites()
    {
        var all = new List<BattleMonster>(playerSprites);
        all.AddRange(opponentSprites);
        return all;
    }

    // update
    public void Update(float dt)
    {
        CheckEndBattle();

        // updates
        HandleInput();
        opponentDelay.Update(dt);
        foreach (var sprite in AllSprites()) sprite.Update(dt); // battle_sprites.update(dt)
        UpdateCrosses(dt);
        UpdateAttackSprites(dt);
        CheckActive();

        // drawing
        UpdateOutlines();
        UpdateMenu();
    }

    // input
    void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || selectionMode == Mode.None || currentMonster == null) return;

        int limiter = 0;
        switch (selectionMode)
        {
            case Mode.General: limiter = Choices.Length; break;
            case Mode.Attacks: limiter = currentMonster.monster.GetAbilities(all: false).Count; break;
            case Mode.Switch: limiter = AvailableMonsters().Count; break;
            case Mode.Target: limiter = selectionSide == "opponent" ? opponentSprites.Count : playerSprites.Count; break;
        }

        // (the Python game divides by zero here when there is nothing to choose from)
        if (limiter > 0)
        {
            int mode = (int)selectionMode;
            if (keyboard.downArrowKey.wasPressedThisFrame) indexes[mode] = (indexes[mode] + 1) % limiter;
            if (keyboard.upArrowKey.wasPressedThisFrame) indexes[mode] = ((indexes[mode] - 1) % limiter + limiter) % limiter;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (selectionMode == Mode.Switch)
            {
                var available = AvailableMonsters();
                if (available.Count > 0)
                {
                    var (index, newMonster) = available[Mathf.Min(indexes[(int)Mode.Switch], available.Count - 1)];
                    currentMonster.Remove();
                    CreateMonster(newMonster, index, currentMonster.posIndex, "player");
                    selectionMode = Mode.None;
                    UpdateAllMonsters(false);
                }
            }

            if (selectionMode == Mode.Target)
            {
                var target = TargetSprite(selectionSide);
                if (target != null)
                {
                    if (selectedAttack != null)
                    {
                        currentMonster.ActivateAttack(target, selectedAttack);
                        selectedAttack = null;
                        currentMonster = null;
                        selectionMode = Mode.None;
                    }
                    else
                    {
                        // catching: only monsters below 90% health can be caught
                        if (target.monster.health < target.monster.GetStat("max_health") * 0.9f)
                        {
                            playerData.Add(target.monster);
                            target.DelayedKill(null);
                            UpdateAllMonsters(false);
                        }
                        else
                        {
                            ShowCross(target);
                        }
                    }
                }
            }

            if (selectionMode == Mode.Attacks)
            {
                var abilities = currentMonster.monster.GetAbilities(all: false);
                if (abilities.Count > 0)
                {
                    selectionMode = Mode.Target;
                    selectedAttack = abilities[Mathf.Min(indexes[(int)Mode.Attacks], abilities.Count - 1)];
                    selectionSide = MonsterDatabase.Attacks[selectedAttack].target;
                }
            }

            if (selectionMode == Mode.General)
            {
                int choice = indexes[(int)Mode.General];
                if (choice == 0) selectionMode = Mode.Attacks;

                if (choice == 1) // defend
                {
                    currentMonster.monster.defending = true;
                    UpdateAllMonsters(false);
                    currentMonster = null;
                    selectionMode = Mode.None;
                    indexes[(int)Mode.General] = 0;
                }

                if (choice == 2) selectionMode = Mode.Switch;

                if (choice == 3) // catch
                {
                    selectionMode = Mode.Target;
                    selectionSide = "opponent";
                }
            }

            for (int i = 0; i < indexes.Length; i++) indexes[i] = 0;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (selectionMode == Mode.Attacks || selectionMode == Mode.Switch || selectionMode == Mode.Target)
                selectionMode = Mode.General;
        }
    }

    // {sprite.pos_index: sprite for sprite in group}, then the one the target index points at
    BattleMonster TargetSprite(string side)
    {
        var group = side == "opponent" ? opponentSprites : playerSprites;
        var byPosition = new List<BattleMonster>();
        var positions = new List<int>();
        foreach (var sprite in group)
        {
            int at = positions.IndexOf(sprite.posIndex);
            if (at >= 0) { byPosition[at] = sprite; }
            else { positions.Add(sprite.posIndex); byPosition.Add(sprite); }
        }
        if (byPosition.Count == 0) return null;
        return byPosition[Mathf.Min(indexes[(int)Mode.Target], byPosition.Count - 1)];
    }

    // The party members that are not on the field and still have health
    List<(int index, Monster monster)> AvailableMonsters()
    {
        var available = new List<(int, Monster)>();
        for (int i = 0; i < playerData.Count; i++)
        {
            var monster = playerData[i];
            bool active = playerSprites.Exists(sprite => sprite.index == i && sprite.monster == monster);
            if (!active && monster.health > 0) available.Add((i, monster));
        }
        return available;
    }

    // battle system
    void CheckActive()
    {
        foreach (var sprite in AllSprites())
        {
            if (sprite.monster.initiative < 100) continue;

            sprite.monster.defending = false;
            UpdateAllMonsters(true);
            sprite.monster.initiative = 0;
            sprite.SetHighlight(true);
            currentMonster = sprite;
            if (sprite.IsPlayer) selectionMode = Mode.General;
            else opponentDelay.Activate();
        }
    }

    void UpdateAllMonsters(bool pause)
    {
        foreach (var sprite in AllSprites()) sprite.monster.paused = pause;
    }

    // apply_attack: called when a monster's attack animation ends
    public void ApplyAttack(BattleMonster target, string attack, float amount)
    {
        var data = MonsterDatabase.Attacks[attack];
        CreateAttackSprite(target.rect.center, assets.attacks[data.animation]);
        AudioManager.Play(data.animation);

        // get correct attack damage amount (defense, element)
        string attackElement = data.element;
        string targetElement = target.monster.element;

        // double attack
        if (attackElement == "fire" && targetElement == "plant" ||
            attackElement == "water" && targetElement == "fire" ||
            attackElement == "plant" && targetElement == "water")
            amount *= 2;

        // halve attack
        if (attackElement == "fire" && targetElement == "water" ||
            attackElement == "water" && targetElement == "plant" ||
            attackElement == "plant" && targetElement == "fire")
            amount *= 0.5f;

        float targetDefense = 1 - target.monster.GetStat("defense") / 2000f;
        if (target.monster.defending) targetDefense -= 0.2f;
        targetDefense = Mathf.Max(0, Mathf.Min(1, targetDefense));

        // update the monster health
        target.monster.health -= amount * targetDefense;
        CheckDeath();

        // resume
        UpdateAllMonsters(false);
    }

    void CheckDeath()
    {
        var sprites = new List<BattleMonster>(opponentSprites);
        sprites.AddRange(playerSprites);

        foreach (var sprite in sprites)
        {
            if (sprite.monster.health > 0) continue;

            BattleMonster.NextMonster? next = null;
            if (sprite.IsPlayer)
            {
                // the first party member that is alive and not on the field takes over
                var available = AvailableMonsters();
                if (available.Count > 0)
                    next = new BattleMonster.NextMonster { monster = available[0].monster, index = available[0].index, posIndex = sprite.posIndex, entity = "player" };
            }
            else
            {
                if (opponentData.Count > 0)
                {
                    int first = int.MaxValue;
                    foreach (var key in opponentData.Keys) first = Mathf.Min(first, key);
                    next = new BattleMonster.NextMonster { monster = opponentData[first], index = sprite.index, posIndex = sprite.posIndex, entity = "opponent" };
                    opponentData.Remove(first);
                }

                // xp
                float xpAmount = sprite.monster.level * 100f / playerSprites.Count;
                foreach (var playerSprite in playerSprites) playerSprite.monster.UpdateXp(xpAmount);
            }

            sprite.DelayedKill(next);
        }
    }

    // OpponentAttack: the opponent picks a random ability and a random target
    void OpponentAttack()
    {
        if (currentMonster == null) return;
        var abilities = currentMonster.monster.GetAbilities();
        if (abilities.Count == 0) return;

        string ability = abilities[UnityEngine.Random.Range(0, abilities.Count)];
        var targets = MonsterDatabase.Attacks[ability].target == "player" ? opponentSprites : playerSprites;
        if (targets.Count == 0) return;
        currentMonster.ActivateAttack(targets[UnityEngine.Random.Range(0, targets.Count)], ability);
    }

    void CheckEndBattle()
    {
        // opponents have been defeated
        if (opponentSprites.Count == 0 && !battleOver)
        {
            battleOver = true;
            endBattle(character);
            foreach (var monster in playerData) monster.initiative = 0;
        }

        // player has been defeated
        if (playerSprites.Count == 0)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // BattleSprites.draw: which monsters get the white outline
    void UpdateOutlines()
    {
        BattleMonster targeted = selectionMode == Mode.Target ? TargetSprite(selectionSide) : null;

        foreach (var sprite in AllSprites())
        {
            bool current = sprite == currentMonster && !(selectionMode == Mode.Target && selectionSide == "player");
            bool target = sprite == targeted && sprite.entity == selectionSide && selectionMode == Mode.Target;
            sprite.SetOutline(current || target);
        }
    }

    public void Destroy()
    {
        UnityEngine.Object.Destroy(root);
    }
}
