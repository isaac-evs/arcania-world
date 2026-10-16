using System.Collections.Generic;

// Port of TRAINER_DATA in game_data.py (Nurse included, keyed by character_id)
public struct MonsterEntry
{
    public string name;
    public int level;
    public MonsterEntry(string name, int level) { this.name = name; this.level = level; }
}

public class TrainerData
{
    public MonsterEntry[] monsters;     // null for the nurse
    public string[] dialogDefault;
    public string[] dialogDefeated;     // null for the nurse
    public Direction[] directions;      // the directions the character looks in
    public bool lookAround;             // can notice and challenge the player
    public bool defeated;
    public string biome;                // battle background, null for the nurse
}

public static class TrainerDatabase
{
    public static readonly Dictionary<string, TrainerData> Trainers = new Dictionary<string, TrainerData>
    {
        { "o1", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Jacana", 14), new MonsterEntry("Cleaf", 15) },
            dialogDefault = new[] { "Hey, how are you?", "Oh, so you want to fight?", "FIGHT!" },
            dialogDefeated = new[] { "You are very strong!", "Let's fight again sometime?" },
            directions = new[] { Direction.Down },
            lookAround = true,
            defeated = false,
            biome = "forest"
        } },
        { "o2", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Atrox", 14), new MonsterEntry("Pouch", 15), new MonsterEntry("Draem", 13), new MonsterEntry("Cindrill", 13) },
            dialogDefault = new[] { "I don't like sand", "It's coarse and rough", "oh god, fight" },
            dialogDefeated = new[] { "May the force be with you" },
            directions = new[] { Direction.Left, Direction.Down },
            lookAround = false,
            defeated = false,
            biome = "sand"
        } },
        { "o3", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Atrox", 14), new MonsterEntry("Pouch", 15), new MonsterEntry("Draem", 13), new MonsterEntry("Cindrill", 13) },
            dialogDefault = new[] { "I love skating!", "FIGHT!" },
            dialogDefeated = new[] { "Good luck with the boss", "It's so cold in here" },
            directions = new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "o4", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love skating!", "FIGHT!" },
            dialogDefeated = new[] { "Good luck with the boss", "It's so cold in here" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "forest"
        } },
        { "o5", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Plumette", 20), new MonsterEntry("Ivieron", 22), new MonsterEntry("Atrox", 24), new MonsterEntry("Pouch", 19) },
            dialogDefault = new[] { "So you want to challenge the big ones", "This will be fun!" },
            dialogDefeated = new[] { "I hope the lawyers will never spot you", "<3" },
            directions = new[] { Direction.Up, Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "forest"
        } },
        { "o6", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Finsta", 15), new MonsterEntry("Finsta", 15), new MonsterEntry("Finsta", 15) },
            dialogDefault = new[] { "I love skating!", "FIGHT!" },
            dialogDefeated = new[] { "Good luck with the boss", "It's so cold in here" },
            directions = new[] { Direction.Down },
            lookAround = false,
            defeated = false,
            biome = "ice"
        } },
        { "o7", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "There are no bugs in the snow!" },
            dialogDefeated = new[] { "Maybe I should check a vulcano...", "It's so cold in here" },
            directions = new[] { Direction.Right },
            lookAround = false,
            defeated = false,
            biome = "ice"
        } },
        { "p1", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love trees", "and fights" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = false,
            defeated = false,
            biome = "forest"
        } },
        { "p2", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love trees", "and fights" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = false,
            defeated = false,
            biome = "forest"
        } },
        { "p3", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love trees", "and fights" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = false,
            defeated = false,
            biome = "forest"
        } },
        { "p4", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love trees", "and fights" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = false,
            defeated = false,
            biome = "forest"
        } },
        { "px", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Atrox", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I love trees", "and fights" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Down },
            lookAround = false,
            defeated = false,
            biome = "forest"
        } },
        { "w1", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "It's so cold in here", "maybe a fight will warm me up" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Left },
            lookAround = true,
            defeated = false,
            biome = "ice"
        } },
        { "w2", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "It's so cold in here", "maybe a fight will warm me up" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "ice"
        } },
        { "w3", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "It's so cold in here", "maybe a fight will warm me up" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "ice"
        } },
        { "w4", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "It's so cold in here", "maybe a fight will warm me up" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Left },
            lookAround = true,
            defeated = false,
            biome = "ice"
        } },
        { "w5", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "It's so cold in here", "maybe a fight will warm me up" },
            dialogDefeated = new[] { "Good luck with the boss!" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "ice"
        } },
        { "wx", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Friolera", 25), new MonsterEntry("Gulfin", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Finiette", 30) },
            dialogDefault = new[] { "I hope you brought rations", "This will be a long journey" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Down },
            lookAround = false,
            defeated = false,
            biome = "ice"
        } },
        { "f1", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "f2", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Right, Direction.Left },
            lookAround = false,
            defeated = false,
            biome = "sand"
        } },
        { "f3", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Right, Direction.Left },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "f4", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Up, Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "f5", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Left },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "f6", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "This place feels kinda warm...", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Right },
            lookAround = true,
            defeated = false,
            biome = "sand"
        } },
        { "fx", new TrainerData
        {
            monsters = new[] { new MonsterEntry("Cindrill", 15), new MonsterEntry("Jacana", 20), new MonsterEntry("Draem", 24), new MonsterEntry("Atrox", 30) },
            dialogDefault = new[] { "Time to bring the heat", "fight!" },
            dialogDefeated = new[] { "Congratultion!" },
            directions = new[] { Direction.Down },
            lookAround = false,
            defeated = false,
            biome = "sand"
        } },
        { "Nurse", new TrainerData
        {
            dialogDefault = new[] { "Welcome to the hospital", "Your monsters have been healed" },
            directions = new[] { Direction.Down },
            lookAround = false,
            defeated = false
        } },
    };
}
