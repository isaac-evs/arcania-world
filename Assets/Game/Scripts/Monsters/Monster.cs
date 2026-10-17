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

    public void UpdateXp(float amount)
    {
        if (levelUp - xp > amount)
        {
            xp += amount;
        }
        else
        {
            level += 1;
            xp = amount - (levelUp - xp);
            levelUp = level * 150;
        }
    }

    public void StatLimiter()
    {
        health = Mathf.Max(0, Mathf.Min(health, GetStat("max_health")));
        energy = Mathf.Max(0, Mathf.Min(energy, GetStat("max_energy")));
    }

    // Not in the Python game, where energy never came back: it recovers slowly during a fight, faster with a higher recovery stat.
    // (recovery * level energy points per second, times this)
    const float EnergyRegen = 0.3f;

    public void Update(float dt)
    {
        StatLimiter();
        if (paused) return;

        initiative += GetStat("speed") * dt;
        energy += GetStat("recovery") * EnergyRegen * dt;
    }
}
