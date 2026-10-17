using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// What a save file holds. The Python game has no saves: this is the state that makes up a game in progress.
[Serializable]
public class MonsterSave
{
    public string name;
    public int level;
    public float xp, health, energy;
}

[Serializable]
public class SaveData
{
    public List<MonsterSave> party = new List<MonsterSave>();           // empty in a brand new save: the game starts with its default party
    public List<string> defeatedTrainers = new List<string>();          // character_id of every trainer that was beaten
    public string map = "world";
    public string entrance = "house";                                   // the 'pos' of the Player object the map was entered by
    public bool hasPosition;                                            // false in a new save: the player starts at the entrance
    public float x, y;
    public string facing = "down";
    public float hour = 22f;                                            // the time of day: a new game starts at night, the night of the Great Starfall
}

// Up to three save files, one JSON file each in the game's data folder: save_1.json, save_2.json, save_3.json.
public static class SaveSystem
{
    public const int MaxSlots = 3;

    // The save the game in progress belongs to, 0 when there is none (the game is then never saved)
    public static int CurrentSlot { get; set; }

    static string PathOf(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    public static bool Exists(int slot) => File.Exists(PathOf(slot));

    // The numbers of the saves that exist, in order
    public static List<int> ExistingSlots()
    {
        var slots = new List<int>();
        for (int slot = 1; slot <= MaxSlots; slot++)
        {
            if (Exists(slot)) slots.Add(slot);
        }
        return slots;
    }

    // A new save in the first free slot; returns its number, or 0 when all three are taken
    public static int Create()
    {
        for (int slot = 1; slot <= MaxSlots; slot++)
        {
            if (Exists(slot)) continue;

            Write(slot, new SaveData());
            Debug.Log($"New save created: {PathOf(slot)}");
            return slot;
        }
        return 0;
    }

    public static void Delete(int slot)
    {
        if (Exists(slot)) File.Delete(PathOf(slot));
    }

    public static SaveData Load(int slot)
    {
        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot))) ?? new SaveData();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not read save {slot}, starting it fresh: {exception.Message}");
            return new SaveData();
        }
    }

    public static void Write(int slot, SaveData data)
    {
        File.WriteAllText(PathOf(slot), JsonUtility.ToJson(data, true));
    }

    public static void ResetState()
    {
        CurrentSlot = 0;
    }
}
