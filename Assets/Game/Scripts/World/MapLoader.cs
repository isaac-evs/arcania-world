using System;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of Game.setup, Game.transition_check and the map switching of Game.tint_screen in main.py.
// One object in the scene. It builds the current map, moves the player to the right entrance and switches maps
// through the Transition rectangles with a fade to black.
public class MapLoader : MonoBehaviour
{
    const float TileSize = 64f;

    [SerializeField] GameObject[] mapPrefabs;   // the imported .tmx files
    [SerializeField] Texture2D[] waterFrames;
    [SerializeField] Texture2D coastSheet;
    [SerializeField] Texture2D[] characterSheets;
    [SerializeField] Texture2D shadowTexture;
    [SerializeField] Font dialogFont;
    [SerializeField] string startMap = "world"; // self.setup(self.tmx_maps['world'], 'house')
    [SerializeField] string startPosition = "house";

    struct Transition
    {
        public Rect rect;
        public string map, position;
    }

    // The one MapLoader of the scene, so other scripts can ask for a save
    static MapLoader instance;

    string currentMapName, currentEntrance;

    // The map the player is in now ("world", "house"...): the other player is only shown when they are in the same one
    public string CurrentMapName => currentMapName;
    PlayerController player;
    DialogSystem dialogSystem;
    ScreenTint tint;
    GameObject currentMap;
    readonly List<Transition> transitions = new List<Transition>();
    bool transitionStarted;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();

        // a map that was dropped into the scene by hand is replaced by the one built here
        foreach (var oldMap in FindObjectsByType<SuperMap>(FindObjectsSortMode.None))
        {
            oldMap.gameObject.SetActive(false);
            Destroy(oldMap.gameObject);
        }

        dialogSystem = FindFirstObjectByType<DialogSystem>();
        if (dialogSystem == null)
        {
            dialogSystem = gameObject.AddComponent<DialogSystem>();
            dialogSystem.Configure(dialogFont);
        }

        tint = gameObject.AddComponent<ScreenTint>();

        // the screen outside the map is black, like display_surface.fill('black')
        var camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        // with a title menu in the scene the game waits for Play; without one it starts straight away
        if (FindFirstObjectByType<TitleMenu>() != null) player.Block();
        else StartGame();
    }

    // Game.__init__ in main.py: the first map, the player at its entrance, and the music
    // With a save, the game continues from it; without one it starts like the Python game does
    public void StartGame(SaveData save = null)
    {
        if (save != null)
        {
            PlayerParty.Load(save.party);
            TrainerDatabase.ApplyDefeated(save.defeatedTrainers);
            DayNightCycle.SetHour(save.hour);
            LoadMap(save.map, save.entrance, save: false);

            // back to the exact spot where the game was saved (a new save starts at the entrance)
            if (save.hasPosition)
            {
                player.transform.position = new Vector3(save.x, save.y, 0);
                if (Enum.TryParse(save.facing, true, out Direction facing)) player.SetFacing(facing);
            }
        }
        else
        {
            LoadMap(startMap, startPosition, save: false);
        }

        AudioManager.StartMapMusic(); // self.audio['overworld'].play(-1)
        AutoSave(); // a new save holds its starting party from the first moment
    }

    // The game is saved by itself when the map changes and after fights, healing and evolutions
    public static void AutoSaveNow()
    {
        if (instance != null) instance.AutoSave();
    }

    void AutoSave()
    {
        if (SaveSystem.CurrentSlot == 0 || currentMap == null) return;

        SaveSystem.Write(SaveSystem.CurrentSlot, new SaveData
        {
            party = PlayerParty.ToSave(),
            defeatedTrainers = TrainerDatabase.DefeatedIds(),
            map = currentMapName,
            entrance = currentEntrance,
            hasPosition = true,
            x = player.transform.position.x,
            y = player.transform.position.y,
            facing = player.FacingDirection.ToString().ToLower(),
            hour = DayNightCycle.Hour,
        });
    }

    void Update()
    {
        // transition_check: touching a Transition rectangle starts the fade to the other map
        if (transitionStarted || currentMap == null) return;

        foreach (var transition in transitions)
        {
            if (!WorldColliders.Overlap(player.HitboxRect, transition.rect)) continue;

            transitionStarted = true;
            player.Block();
            var target = transition;
            tint.Begin(() =>
            {
                LoadMap(target.map, target.position);
                transitionStarted = false;
            });
            break;
        }
    }

    void LoadMap(string mapName, string playerPosition, bool save = true)
    {
        if (currentMap != null)
        {
            currentMap.SetActive(false);
            Destroy(currentMap);
        }

        var prefab = Array.Find(mapPrefabs, p => p != null && p.name == mapName);
        if (prefab == null)
        {
            Debug.LogError($"No map named '{mapName}' in Map Prefabs");
            return;
        }

        currentMap = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        currentMap.name = mapName;
        currentMapName = mapName;
        currentEntrance = playerPosition;

        // terrain, objects, collisions and grass first, then the player, then the characters (they need to know the player)
        // Each step is on its own: if one of them fails (the error goes to the log), the others still run, and above all the player
        // is still put in the map and released. Before, one failure left the player frozen, with every other key still working.
        Step("objects and collisions", () => currentMap.AddComponent<OverworldObjects>().Build(mapName == "world"));
        Step("water and coast", () => currentMap.AddComponent<OverworldAnimations>().Build(waterFrames, coastSheet));
        Step("the player", () => PlacePlayer(playerPosition));
        Step("transitions", ReadTransitions);
        Step("characters", () => currentMap.AddComponent<OverworldCharacters>().Build(characterSheets, shadowTexture, player, dialogSystem));

        // the sprites of the map react to light; the world gets its night-time door lights
        Step("light", () => DayNightCycle.MapBuilt(currentMap, mapName));

        // hospitals and gyms have their own music; the world and the house share the overworld songs
        AudioManager.SetMap(mapName);

        if (save) AutoSave(); // the map changed: a good moment to save
    }

    static void Step(string what, Action step)
    {
        try
        {
            step();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Building the map failed at {what}: {exception}");
        }
    }

    // 'Entities': the Player object whose 'pos' matches where we come from
    void PlacePlayer(string playerPosition)
    {
        foreach (var layer in currentMap.GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name != "Entities") continue;

            foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            {
                if (obj.m_TiledName != "Player" || Property(obj, "pos") != playerPosition) continue;

                player.transform.position = new Vector3(obj.m_X / TileSize, -obj.m_Y / TileSize, 0);
                string facing = Property(obj, "direction");
                player.SetFacing((Direction)Enum.Parse(typeof(Direction), facing == "" ? "down" : facing, true));
                player.direction = Vector2.zero;
                player.noticed = false;
                player.Unblock(); // a new Player is created for each map in Python
                return;
            }
        }

        Debug.LogError($"No Player entrance '{playerPosition}' in map '{currentMap.name}'");
    }

    void ReadTransitions()
    {
        transitions.Clear();
        transitionStarted = false;

        foreach (var layer in currentMap.GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name != "Transition") continue;

            foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            {
                // TransitionSprite: x, y is the top-left corner of the rectangle
                var rect = new Rect(obj.m_X / TileSize, -(obj.m_Y + obj.m_Height) / TileSize, obj.m_Width / TileSize, obj.m_Height / TileSize);
                transitions.Add(new Transition { rect = rect, map = Property(obj, "target"), position = Property(obj, "pos") });
            }
        }
    }

    static string Property(SuperObject obj, string name)
    {
        var props = obj.GetComponent<SuperCustomProperties>();
        return props != null && props.TryGetCustomProperty(name, out var property) ? property.m_Value : "";
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): fills everything in
    void Reset()
    {
        var maps = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Game/data/maps" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".tmx")) maps.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        mapPrefabs = maps.ToArray();

        waterFrames = new Texture2D[4];
        for (int i = 0; i < 4; i++)
            waterFrames[i] = OverworldAnimations.LoadPixelArt($"Assets/Game/graphics/tilesets/water/{i}.png");
        coastSheet = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/tilesets/coast.png");

        shadowTexture = OverworldAnimations.LoadPixelArt("Assets/Game/graphics/other/shadow.png");

        var sheets = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/graphics/characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/player.png")) continue; // already set up by hand for the player
            sheets.Add(OverworldAnimations.LoadPixelArt(path));
        }
        characterSheets = sheets.ToArray();

        dialogFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/graphics/fonts/PixeloidSans.ttf");
    }
#endif
}
