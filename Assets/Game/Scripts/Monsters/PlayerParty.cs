using System.Collections.Generic;
using UnityEngine;

// Game.player_monsters in main.py: the monsters the player owns, in the order the monster index shows them
public static class PlayerParty
{
    static List<Monster> monsters;

    public static List<Monster> Monsters
    {
        get
        {
            if (monsters == null) Create();
            return monsters;
        }
    }

    // The party as it goes into a save file
    public static List<MonsterSave> ToSave()
    {
        var saved = new List<MonsterSave>();
        foreach (var monster in Monsters)
            saved.Add(new MonsterSave { name = monster.name, level = monster.level, xp = monster.xp, health = monster.health, energy = monster.energy });
        return saved;
    }

    // The party of a save file. A brand new save has none, and then the game's starting party is used.
    public static void Load(List<MonsterSave> saved)
    {
        if (saved == null || saved.Count == 0) return;

        var loaded = new List<Monster>();
        foreach (var entry in saved)
        {
            if (!MonsterDatabase.Monsters.ContainsKey(entry.name)) continue; // a monster that no longer exists in the game

            var monster = new Monster(entry.name, entry.level);
            monster.xp = entry.xp;
            monster.health = entry.health;
            monster.energy = entry.energy;
            loaded.Add(monster);
        }
        monsters = loaded;
    }

    // a new game starts with a new party
    public static void ResetState()
    {
        monsters = null;
    }

    static void Create()
    {
        monsters = new List<Monster>
        {
            new Monster("Ivieron", 32),
            new Monster("Atrox", 15),
            new Monster("Cindrill", 16),
            new Monster("Atrox", 10),
            new Monster("Sparchu", 11),
            new Monster("Gulfin", 9),
            new Monster("Jacana", 10),
        };

        // monster.xp += randint(0, monster.level * 100)
        foreach (var monster in monsters)
            monster.xp += Random.Range(0, monster.level * 100 + 1);
    }
}
