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
