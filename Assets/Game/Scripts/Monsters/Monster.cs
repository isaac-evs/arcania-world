using System.Collections.Generic;
using UnityEngine;

// Port of Monster in monster.py
public class Monster
{
    public string name;
    public int level;
    public bool paused;

    // stats
    public string element;
    public MonsterData baseStats;
    public float health, energy, initiative;
    public Ability[] abilities;
    public bool defending;

    // experience
    public float xp;
    public int levelUp;
    public MonsterData evolutionData; // the 'evolve' entry: evolveInto null when it never evolves

    public Monster(string name, int level)
    {
        this.name = name;
        this.level = level;

        baseStats = MonsterDatabase.Monsters[name];
        element = baseStats.element;
        health = baseStats.maxHealth * level;
        energy = baseStats.maxEnergy * level;
        initiative = 0;
        abilities = baseStats.abilities;
        defending = false;

        xp = 0;
        levelUp = level * 150;
        evolutionData = baseStats;
    }

    public override string ToString() => $"monster: {name}, lvl: {level}";

    public bool CanEvolve => evolutionData.evolveInto != null;
    public string EvolveInto => evolutionData.evolveInto;
    public int EvolveLevel => evolutionData.evolveLevel;

    // get_stat: the base stat times the level
    public float GetStat(string stat)
    {
        switch (stat)
        {
            case "max_health": return baseStats.maxHealth * level;
            case "max_energy": return baseStats.maxEnergy * level;
            case "attack":     return baseStats.attack * level;
            case "defense":    return baseStats.defense * level;
            case "speed":      return baseStats.speed * level;
            case "recovery":   return baseStats.recovery * level;
        }
        Debug.LogError($"Unknown stat '{stat}'");
        return 0;
    }

    // get_stats: in the order the monster index lists them
    public List<KeyValuePair<string, float>> GetStats()
    {
        return new List<KeyValuePair<string, float>>
        {
            new KeyValuePair<string, float>("health", GetStat("max_health")),
            new KeyValuePair<string, float>("energy", GetStat("max_energy")),
            new KeyValuePair<string, float>("attack", GetStat("attack")),
            new KeyValuePair<string, float>("defense", GetStat("defense")),
            new KeyValuePair<string, float>("speed", GetStat("speed")),
            new KeyValuePair<string, float>("recovery", GetStat("recovery")),
        };
    }

    // get_abilities(all): every ability learned at this level; with all = false only those that cost less energy than we have
    public List<string> GetAbilities(bool all = true)
    {
        var result = new List<string>();
        foreach (var ability in abilities)
        {
            if (level < ability.level) continue;
            if (all || MonsterDatabase.Attacks[ability.attack].cost < energy) result.Add(ability.attack);
        }
        return result;
    }

    // get_info: (value, max) for health, energy and initiative
    public (float, float)[] GetInfo()
    {
        return new[] { (health, GetStat("max_health")), (energy, GetStat("max_energy")), (initiative, 100f) };
    }

    public void ReduceEnergy(string attack) => energy -= MonsterDatabase.Attacks[attack].cost;

    public float GetBaseDamage(string attack) => GetStat("attack") * MonsterDatabase.Attacks[attack].amount;

    // The Python game gave level * 100 xp per defeated monster, and a level needs level * 150: slow for a short game.
    // Every xp award is multiplied by this.
    public const float XpGainMultiplier = 4f;

    public void UpdateXp(float amount)
    {
        // as in the Python game, a full bar means a new level and what is left over starts the next bar; the difference is
        // that it keeps going, so a big award can give several levels at once (before, it gave one at most)
        xp += amount;
        while (xp >= levelUp)
        {
            xp -= levelUp;
            level += 1;
            levelUp = level * 150;
        }
    }

    public void StatLimiter()
    {
        health = Mathf.Max(0, Mathf.Min(health, GetStat("max_health")));
        energy = Mathf.Max(0, Mathf.Min(energy, GetStat("max_energy")));
    }

    // How fast the turn bar fills: speed * (level + this). The Python game used speed * level, which made a fight of a low-level
    // monster painfully slow (a level 5 fight took about 40 seconds); this keeps the monster's own speed mattering but makes the
    // level count much less, so every fight takes about the same time.
    const float TurnSpeedBase = 30f;

    // Not in the Python game, where energy never came back: it recovers during a fight, faster with a higher recovery stat.
    // (recovery * level energy points per second, times this; tuned together with the faster turns)
    const float EnergyRegen = 0.9f;

    public void Update(float dt)
    {
        StatLimiter();
        if (paused) return;

        initiative += baseStats.speed * (TurnSpeedBase + level) * dt;
        energy += GetStat("recovery") * EnergyRegen * dt;
    }
}
