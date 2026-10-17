using System.Collections.Generic;
using UnityEngine;

// The wild monsters of the tall grass. In the Python game every patch of grass lists the same two monsters (Atrox and Pouch);
// here each biome has its own pool and every encounter rolls from it, so the monsters change and match the place.
public static class WildEncounters
{
    struct Entry
    {
        public string monster;
        public int weight;   // how often it shows up compared with the others in its pool
        public Entry(string monster, int weight) { this.monster = monster; this.weight = weight; }
    }

    // The pools list first forms. A monster that is rolled at a level where it would already have evolved comes out evolved
    // (Larvea evolves into Cleaf at level 4, so in the wild it is always a Cleaf).
    // forest: plant monsters, and a bird that wandered in; ice: water monsters and a stray; sand: fire monsters
    static readonly Dictionary<string, Entry[]> Pools = new Dictionary<string, Entry[]>
    {
        { "forest", new[] { new Entry("Plumette", 3), new Entry("Larvea", 3), new Entry("Pouch", 3), new Entry("Draem", 2), new Entry("Jacana", 1) } },
        { "ice",    new[] { new Entry("Finsta", 4), new Entry("Friolera", 4), new Entry("Pouch", 1) } },
        { "sand",   new[] { new Entry("Sparchu", 3), new Entry("Atrox", 3), new Entry("Jacana", 3) } },
    };

    const float TwoMonstersChance = 0.4f; // otherwise one; the Python game always had two

    // The patches say level 10, which is a fair fight for the monsters the Python game starts you with (levels 9 to 32), but a
    // lone level 12 starter has the weakest stats of the game. Wild monsters are this many levels below the patch.
    const int WildLevelOffset = 3;

    // The opponents of one encounter: one or two different monsters of the biome, around the patch level minus the offset
    public static SortedDictionary<int, Monster> Roll(string biome, int patchLevel)
    {
        if (!Pools.TryGetValue(biome, out var pool)) pool = Pools["forest"];

        int count = Random.value < TwoMonstersChance ? 2 : 1;
        var opponents = new SortedDictionary<int, Monster>();
        var chosen = new List<string>();

        while (chosen.Count < count)
        {
            string monster = Pick(pool);
            if (chosen.Contains(monster) && pool.Length > chosen.Count) continue; // not the same monster twice, when there is a choice
            chosen.Add(monster);

            int level = Mathf.Max(2, patchLevel - WildLevelOffset + Random.Range(-3, 4));
            opponents[opponents.Count] = new Monster(Evolved(monster, level), level);
        }
        return opponents;
    }

    // The form a monster has at a level: every evolution whose level has been reached is applied
    static string Evolved(string monster, int level)
    {
        var data = MonsterDatabase.Monsters[monster];
        while (data.evolveInto != null && level >= data.evolveLevel)
        {
            monster = data.evolveInto;
            data = MonsterDatabase.Monsters[monster];
        }
        return monster;
    }

    static string Pick(Entry[] pool)
    {
        int total = 0;
        foreach (var entry in pool) total += entry.weight;

        int roll = Random.Range(0, total);
        foreach (var entry in pool)
        {
            roll -= entry.weight;
            if (roll < 0) return entry.monster;
        }
        return pool[0].monster;
    }
}
