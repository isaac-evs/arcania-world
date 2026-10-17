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
    public readonly Transform layerOutline, layerName, layerMonster, layerEffects, layerOverlay, layerMenu, layerMessage;

    // general
    readonly Action<Character, bool> endBattle; // the trainer (null in a wild fight), and whether the player won (false: ran away)
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
        Character character, Action<Character, bool> endBattle, bool nightSky = false)
    {
        this.assets = assets;
        playerData = playerMonsters;
        opponentData = new SortedDictionary<int, Monster>(opponentMonsters);
        this.character = character;
        this.endBattle = endBattle;

        opponentDelay = new Countdown(0.6f, OpponentAttack);

        root = Stretched(panel, "Battle");

        // display_surface.blit(self.bg_surf, (0, 0))
        // (the night backgrounds are bigger than 1280 x 720, so the picture is fitted to the screen)
        var picture = UiKit.Picture(root.transform, background, Anchor.TopLeft, 0, 0);
        picture.rectTransform.sizeDelta = new Vector2(1280, 720);

        // a night battle has shooting stars in the sky, behind the monsters and everything else
        if (nightSky)
        {
            var host = Stretched(root.transform, "Star Shower");
            var shower = host.AddComponent<StarShower>();
            shower.BuildInto(host.transform);
            shower.HorizonY = 400f;
            shower.Intensity = 1f;
        }

        layerOutline = Stretched(root.transform, "Outline").transform;
        layerName = Stretched(root.transform, "Name").transform;
        layerMonster = Stretched(root.transform, "Monster").transform;
        layerEffects = Stretched(root.transform, "Effects").transform;
        layerOverlay = Stretched(root.transform, "Overlay").transform;
        layerMenu = Stretched(root.transform, "Menu").transform;
        layerMessage = Stretched(root.transform, "Message").transform;

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

    bool hadOpponents; // false only if something went wrong and the opponent never had a monster: that is not a win

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

        hadOpponents = opponentSprites.Count > 0;
        if (!hadOpponents) Debug.LogError("A battle started with no opponent monsters.");
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

    // Safety net: if the monsters are paused but nobody is taking a turn, the fight is frozen. After a second they are resumed.
    float stuckTime;

    void UnfreezeIfStuck(float dt)
    {
        bool anyPaused = AllSprites().Exists(sprite => sprite.monster.paused);
        if (anyPaused && !TurnInProgress())
        {
            stuckTime += dt;
            if (stuckTime > 1f)
            {
                UpdateAllMonsters(false);
                stuckTime = 0f;
            }
        }
        else
        {
            stuckTime = 0f;
        }
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
        UpdateMessage(dt);
        UpdateAttackSprites(dt);
        CheckActive();
        UnfreezeIfStuck(dt);

        // drawing
        UpdateOutlines();
        UpdateMenu();
    }

    // input
    void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || Multiplayer.IsMenuOpen || selectionMode == Mode.None || currentMonster == null) return;

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
            // set when this press already sent us back to the four choices: it must not also choose one of them
            bool backedOut = false;

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
                    else if (character != null)
                    {
                        // a trainer's monsters belong to the trainer: they cannot be caught (the menu stays open, like a failed catch)
                        ShowMessage(StolenMessages[UnityEngine.Random.Range(0, StolenMessages.Length)]);
                        ShowCross(target);
                        BackToMenu();
                        backedOut = true;
                    }
                    else
                    {
                        // catching: only monsters below 90% health can be caught (and not one that is already leaving the field)
                        if (!target.Dying && target.monster.health > 0 && target.monster.health < target.monster.GetStat("max_health") * 0.9f)
                        {
                            // the same monster can only join the party once
                            if (!playerData.Contains(target.monster)) playerData.Add(target.monster);
                            target.DelayedKill(null);
                            UpdateAllMonsters(false);

                            // The catch is the action of this turn. (In the Python game the menu stayed open on the target, so pressing
                            // Space again caught the same monster again and put it twice in the party.)
                            currentMonster = null;
                            selectionMode = Mode.None;
                        }
                        else
                        {
                            ShowCross(target);
                            ShowMessage("It is too healthy to catch!");
                            BackToMenu();
                            backedOut = true;
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
                    attackListIndex = Mathf.Min(indexes[(int)Mode.Attacks], abilities.Count - 1);
                    selectedAttack = abilities[attackListIndex];
                    selectionSide = MonsterDatabase.Attacks[selectedAttack].target;
                }
            }

            if (selectionMode == Mode.General && !backedOut)
            {
                int choice = indexes[(int)Mode.General];
                if (choice == 0)
                {
                    if (currentMonster.monster.GetAbilities(all: false).Count == 0) ShowMessage("Not enough energy!");
                    else selectionMode = Mode.Attacks;
                }

                if (choice == 1) // defend
                {
                    currentMonster.monster.defending = true;
                    UpdateAllMonsters(false);
                    currentMonster = null;
                    selectionMode = Mode.None;
                    indexes[(int)Mode.General] = 0;
                }

                if (choice == 2)
                {
                    if (AvailableMonsters().Count == 0) ShowMessage("No other monsters to switch to!");
                    else selectionMode = Mode.Switch;
                }

                if (choice == 3) // catch
                {
                    selectionMode = Mode.Target;
                    selectionSide = "opponent";
                }

                if (choice == 4) TryEscape();
            }

            for (int i = 0; i < indexes.Length; i++) indexes[i] = 0;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (selectionMode == Mode.Attacks || selectionMode == Mode.Switch || selectionMode == Mode.Target)
                BackToMenu();
        }
    }

    int attackListIndex; // where the cursor was in the attack list, to put it back when going back to it

    // Escape goes back one step: from choosing a target for an attack, to the attack list; from anything else, to the four choices.
    // The turn is not spent. The attack that was being aimed is forgotten: in the Python game it was kept, so choosing Catch
    // afterwards fired that attack instead.
    void BackToMenu()
    {
        bool fromAttackTarget = selectionMode == Mode.Target && selectedAttack != null;
        selectedAttack = null;

        if (fromAttackTarget)
        {
            selectionMode = Mode.Attacks;
            indexes[(int)Mode.Attacks] = attackListIndex;
        }
        else
        {
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
        // one turn at a time: while a menu is open, an opponent is about to attack or an attack is playing, the next monster waits
        if (TurnInProgress()) return;

        foreach (var sprite in AllSprites())
        {
            if (sprite.monster.initiative < 100) continue;

            // A monster that has fainted or is about to leave the field never gets a turn. (In the Python game it could: its
            // attack then never finished, nobody was resumed, and the fight froze.)
            if (sprite.removed || sprite.Dying || sprite.monster.health <= 0) continue;

            sprite.monster.defending = false;
            UpdateAllMonsters(true);
            sprite.monster.initiative = 0;
            sprite.SetHighlight(true);
            currentMonster = sprite;
            if (sprite.IsPlayer) selectionMode = Mode.General;
            else opponentDelay.Activate();
            break; // if another monster is ready too, it goes next
        }
    }

    bool TurnInProgress()
    {
        return selectionMode != Mode.None || opponentDelay.active || AllSprites().Exists(sprite => sprite.IsAttacking);
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
                float xpAmount = sprite.monster.level * 100f * Monster.XpGainMultiplier / playerSprites.Count;
                foreach (var playerSprite in playerSprites) playerSprite.monster.UpdateXp(xpAmount);
            }

            sprite.DelayedKill(next);
        }
    }

    // Not in the Python game: running away works 40% of the time. When it fails the turn is lost, and the turn bar, which
    // was emptied when this turn began, starts filling again from zero.
    const float EscapeChance = 0.4f;

    void TryEscape()
    {
        if (UnityEngine.Random.value < EscapeChance)
        {
            ShowMessage("Got away safely!");
            battleOver = true;
            foreach (var monster in playerData) monster.initiative = 0;
            endBattle(character, false);
            return;
        }

        ShowMessage("Couldn't get away!");
        currentMonster = null;
        selectionMode = Mode.None;
        UpdateAllMonsters(false);
    }

    // OpponentAttack: the opponent picks a random ability and a random target
    void OpponentAttack()
    {
        // if the monster cannot attack after all, its turn is simply over: everybody must be resumed or the fight freezes
        if (currentMonster == null || currentMonster.removed || currentMonster.Dying)
        {
            UpdateAllMonsters(false);
            return;
        }

        var abilities = currentMonster.monster.GetAbilities();
        string ability = abilities.Count > 0 ? abilities[UnityEngine.Random.Range(0, abilities.Count)] : null;
        var targets = ability != null && MonsterDatabase.Attacks[ability].target == "player" ? opponentSprites : playerSprites;
        if (ability == null || targets.Count == 0)
        {
            UpdateAllMonsters(false);
            return;
        }

        currentMonster.ActivateAttack(targets[UnityEngine.Random.Range(0, targets.Count)], ability);
    }

    void CheckEndBattle()
    {
        // opponents have been defeated
        if (opponentSprites.Count == 0 && !battleOver)
        {
            battleOver = true;
            endBattle(character, hadOpponents); // with nobody to fight it is not a victory: nothing is won and nobody is defeated
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
