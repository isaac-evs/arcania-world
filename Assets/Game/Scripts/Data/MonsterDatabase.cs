using System.Collections.Generic;

// Port of MONSTER_DATA and ATTACK_DATA in game_data.py (generated from the Python file, same order)
public struct AttackData
{
    public string target;     // 'opponent' or 'player'
    public float amount;      // damage multiplier (negative heals)
    public int cost;          // energy
    public string element;
    public string animation;
}

public struct Ability
{
    public int level;         // level the monster learns it
    public string attack;
    public Ability(int level, string attack) { this.level = level; this.attack = attack; }
}

public class MonsterData
{
    public string element;
    public float maxHealth, maxEnergy, attack, defense, recovery, speed; // per level
    public Ability[] abilities;
    public string evolveInto;  // null when it does not evolve
    public int evolveLevel;
}

public static class MonsterDatabase
{
    // The ladder: scratch is the basic attack; the first attack of each type (ember, bubble, leaf) is a little stronger;
    // the next ones (burn, fire, splash, ice, vine) hit harder but cost more; explosion and annihilate are rare and expensive.
    // Healing costs energy too, so it cannot be spammed. (The Python numbers had heal at 600 energy and annihilate at 30.)
    public static readonly Dictionary<string, AttackData> Attacks = new Dictionary<string, AttackData>
    {
        { "burn", new AttackData { target = "opponent", amount = 1.9f, cost = 25, element = "fire", animation = "fire" } },
        { "heal", new AttackData { target = "player", amount = -1.2f, cost = 100, element = "plant", animation = "green" } },
        { "battlecry", new AttackData { target = "player", amount = -1.4f, cost = 40, element = "normal", animation = "green" } },
        { "ember", new AttackData { target = "opponent", amount = 1.5f, cost = 20, element = "fire", animation = "fire" } },
        { "bubble", new AttackData { target = "opponent", amount = 1.5f, cost = 20, element = "water", animation = "splash" } },
        { "leaf", new AttackData { target = "opponent", amount = 1.5f, cost = 20, element = "plant", animation = "green" } },
        { "vine", new AttackData { target = "opponent", amount = 1.9f, cost = 25, element = "plant", animation = "scratch" } },
        { "scratch", new AttackData { target = "opponent", amount = 1.2f, cost = 20, element = "normal", animation = "scratch" } },
        { "splash", new AttackData { target = "opponent", amount = 1.9f, cost = 25, element = "water", animation = "splash" } },
        { "fire", new AttackData { target = "opponent", amount = 1.9f, cost = 25, element = "fire", animation = "fire" } },
        { "explosion", new AttackData { target = "opponent", amount = 3f, cost = 90, element = "fire", animation = "explosion" } },
        { "annihilate", new AttackData { target = "opponent", amount = 3.5f, cost = 110, element = "fire", animation = "explosion" } },
        { "ice", new AttackData { target = "opponent", amount = 1.9f, cost = 25, element = "water", animation = "ice" } },
    };

    public static readonly Dictionary<string, MonsterData> Monsters = new Dictionary<string, MonsterData>
    {
        { "Plumette", new MonsterData { element = "plant", maxHealth = 15, maxEnergy = 17, attack = 4, defense = 8, recovery = 1, speed = 1, evolveInto = "Ivieron", evolveLevel = 15, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "leaf"), new Ability(10, "vine") } } },
        { "Ivieron", new MonsterData { element = "plant", maxHealth = 18, maxEnergy = 20, attack = 5, defense = 10, recovery = 1.2f, speed = 1.2f, evolveInto = "Pluma", evolveLevel = 32, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "leaf"), new Ability(10, "vine"), new Ability(25, "heal") } } },
        { "Pluma", new MonsterData { element = "plant", maxHealth = 23, maxEnergy = 26, attack = 6, defense = 12, recovery = 1.8f, speed = 1.8f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "leaf"), new Ability(10, "vine"), new Ability(25, "heal"), new Ability(35, "battlecry") } } },
        { "Sparchu", new MonsterData { element = "fire", maxHealth = 15, maxEnergy = 7, attack = 3, defense = 8, recovery = 1.1f, speed = 1, evolveInto = "Cindrill", evolveLevel = 15, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "fire"), new Ability(15, "battlecry"), new Ability(26, "explosion") } } },
        { "Cindrill", new MonsterData { element = "fire", maxHealth = 18, maxEnergy = 10, attack = 3.5f, defense = 10, recovery = 1.2f, speed = 1.1f, evolveInto = "Charmadillo", evolveLevel = 33, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "fire"), new Ability(15, "battlecry"), new Ability(26, "explosion") } } },
        { "Charmadillo", new MonsterData { element = "fire", maxHealth = 29, maxEnergy = 12, attack = 4, defense = 17, recovery = 1.35f, speed = 1.1f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "fire"), new Ability(15, "battlecry"), new Ability(26, "explosion"), new Ability(45, "annihilate") } } },
        { "Finsta", new MonsterData { element = "water", maxHealth = 13, maxEnergy = 17, attack = 2, defense = 8, recovery = 1.5f, speed = 1.8f, evolveInto = "Gulfin", evolveLevel = 34, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "bubble"), new Ability(10, "splash"), new Ability(20, "ice"), new Ability(25, "battlecry") } } },
        { "Gulfin", new MonsterData { element = "water", maxHealth = 18, maxEnergy = 20, attack = 3, defense = 10, recovery = 1.8f, speed = 2, evolveInto = "Finiette", evolveLevel = 45, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "bubble"), new Ability(10, "splash"), new Ability(20, "ice"), new Ability(25, "battlecry") } } },
        { "Finiette", new MonsterData { element = "water", maxHealth = 27, maxEnergy = 23, attack = 4, defense = 17, recovery = 2, speed = 2.5f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "bubble"), new Ability(10, "splash"), new Ability(20, "ice"), new Ability(25, "battlecry") } } },
        { "Atrox", new MonsterData { element = "fire", maxHealth = 18, maxEnergy = 20, attack = 3, defense = 10, recovery = 1.3f, speed = 1.9f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "ember"), new Ability(30, "fire") } } },
        { "Pouch", new MonsterData { element = "plant", maxHealth = 23, maxEnergy = 25, attack = 4, defense = 12, recovery = 1, speed = 1.5f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "leaf"), new Ability(15, "vine"), new Ability(25, "heal") } } },
        { "Draem", new MonsterData { element = "plant", maxHealth = 23, maxEnergy = 25, attack = 4, defense = 12, recovery = 1.2f, speed = 1.4f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "heal"), new Ability(10, "leaf"), new Ability(20, "vine"), new Ability(25, "battlecry") } } },
        { "Larvea", new MonsterData { element = "plant", maxHealth = 15, maxEnergy = 17, attack = 1, defense = 8, recovery = 1, speed = 1, evolveInto = "Cleaf", evolveLevel = 4, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "leaf") } } },
        { "Cleaf", new MonsterData { element = "plant", maxHealth = 18, maxEnergy = 20, attack = 3, defense = 10, recovery = 1.7f, speed = 1.6f, abilities = new[] { new Ability(0, "scratch"), new Ability(3, "leaf"), new Ability(5, "heal") } } },
        { "Jacana", new MonsterData { element = "fire", maxHealth = 12, maxEnergy = 19, attack = 3, defense = 10, recovery = 2.1f, speed = 2.6f, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "ember"), new Ability(15, "burn"), new Ability(20, "explosion"), new Ability(25, "battlecry") } } },
        { "Friolera", new MonsterData { element = "water", maxHealth = 13, maxEnergy = 20, attack = 4, defense = 6, recovery = 1.3f, speed = 2, abilities = new[] { new Ability(0, "scratch"), new Ability(5, "bubble"), new Ability(10, "splash"), new Ability(20, "ice"), new Ability(25, "battlecry") } } },
    };
}
