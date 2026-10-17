using System;
using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A day and night cycle for the overworld (not in the Python game). A clock runs through the day and tints the whole world with
// the Global Light 2D: dawn orange, white noon, dusk orange, dark blue night. At night the doors of the buildings glow and the
// player carries a soft lantern. Interiors always stay fully lit, and the clock stands still during dialogues, fights,
// evolutions, the monster index and the title screens. One per scene: add it to the Game object.
public class DayNightCycle : MonoBehaviour
{
    const float TileSize = 64f;

    [SerializeField] Material litMaterial;     // Sprite-Lit-Default: reacts to light
    [SerializeField] Material unlitMaterial;   // Sprite-Unlit-Default: for things that must not get dark (the dialogue bubble)
    [SerializeField] float dayLengthMinutes = 10f;

    // the colour of the sky through the day: hour, colour (it multiplies everything the camera sees)
    static readonly (float hour, Color color)[] Sky =
    {
        (0f,   new Color(0.05f, 0.07f, 0.18f)),   // night
        (5f,   new Color(0.07f, 0.09f, 0.21f)),
        (6.5f, new Color(0.95f, 0.62f, 0.45f)),   // dawn
        (8f,   new Color(1.00f, 0.95f, 0.88f)),   // morning
        (12f,  new Color(1.00f, 1.00f, 1.00f)),   // noon
        (17f,  new Color(1.00f, 0.95f, 0.85f)),
        (19f,  new Color(1.00f, 0.55f, 0.40f)),   // dusk
        (20.5f, new Color(0.20f, 0.16f, 0.35f)),
        (22f,  new Color(0.06f, 0.08f, 0.20f)),   // night
        (24f,  new Color(0.05f, 0.07f, 0.18f)),
    };

    // the buildings that get a light at their door: houses, the hospital and the gyms (tile ids in objects.tsx)
    static readonly HashSet<uint> BuildingTiles = new HashSet<uint> { 211, 223, 224, 225, 229, 230, 231, 232 };

    const float BuildingLightRadius = 3.2f, BuildingLightIntensity = 1.4f;
    const float LanternRadius = 4.2f, LanternIntensity = 1.1f;
    static readonly Color WarmLight = new Color(1f, 0.78f, 0.45f), LanternLight = new Color(1f, 0.88f, 0.68f);

    static DayNightCycle instance;

    float hour = 22f;                // 0-24, a new game starts at night: the night of the Great Starfall
    bool outdoors = true;            // only the world follows the clock
    Color currentColor = Color.white;
    Light2D globalLight, lantern;
    readonly List<Light2D> buildingLights = new List<Light2D>();
    StarShower starShower;

    PlayerController player;
    DialogSystem dialogSystem;
    BattleSystem battleSystem;
    EvolutionSystem evolutionSystem;
    MonsterIndex monsterIndex;
    IntroSequence intro;

    public static float Hour => instance != null ? instance.hour : 22f;
    // True when it is night and the player is in the open world (indoors the time of day does not show)
    public static bool NightOutdoors => instance != null && instance.outdoors && IsNight(instance.hour);

    public static Material UnlitMaterial => instance != null ? instance.unlitMaterial : null;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        dialogSystem = FindFirstObjectByType<DialogSystem>();
        battleSystem = FindFirstObjectByType<BattleSystem>();
        evolutionSystem = FindFirstObjectByType<EvolutionSystem>();
        monsterIndex = FindFirstObjectByType<MonsterIndex>();
        intro = FindFirstObjectByType<IntroSequence>();

        foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.lightType == Light2D.LightType.Global) globalLight = light;
        }
        if (globalLight == null) Debug.LogError("There is no Global Light 2D in the scene: the day and night cycle needs one.");

        currentColor = SkyColor(hour);
        starShower = gameObject.AddComponent<StarShower>();
    }

    // The save file stores the hour
    public static void SetHour(float value)
    {
        if (instance == null) return;
        instance.hour = Mathf.Repeat(value, 24f);
        AudioManager.SetNight(IsNight(instance.hour)); // the music of the right time is chosen before the game starts
    }

    // Night songs play from 8 in the evening to half past five in the morning
    static bool IsNight(float hour)
    {
        return hour >= 20f || hour < 5.5f;
    }

    // A map has been built: its sprites are made to react to light, and the outdoor map gets its glowing doors
    public static void MapBuilt(GameObject map, string mapName)
    {
        if (instance != null) instance.OnMapBuilt(map, mapName);
    }

    void OnMapBuilt(GameObject map, string mapName)
    {
        outdoors = mapName == "world";

        if (litMaterial != null)
        {
            foreach (var renderer in map.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer || renderer is UnityEngine.Tilemaps.TilemapRenderer) renderer.sharedMaterial = litMaterial;
            }

            // the player and the shadow under the feet react to light too, but the "!" above the head stays bright
            if (player != null)
            {
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.sharedMaterial = renderer.gameObject.name == "Notice" && unlitMaterial != null ? unlitMaterial : litMaterial;
                }
            }
        }

        buildingLights.Clear(); // the old ones went away with the old map
        if (outdoors) CreateBuildingLights(map);
        EnsureLantern();
        lantern.gameObject.SetActive(outdoors);
    }

    // A warm light in front of the door (the middle of the bottom edge) of every house, the hospital and the gyms
    void CreateBuildingLights(GameObject map)
    {
        foreach (var layer in map.GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name != "Objects") continue;

            foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            {
                if (!BuildingTiles.Contains(obj.m_TileId & 0x1FFFFFFFu)) continue;

                // Tiled tile objects are positioned by their bottom-left corner, y down
                var position = new Vector3((obj.m_X + obj.m_Width / 2f) / TileSize, -obj.m_Y / TileSize + 0.5f, 0);
                var light = NewPointLight("Door light", map.transform, position, WarmLight, BuildingLightRadius);
                light.intensity = 0f;
                buildingLights.Add(light);
            }
        }
    }

    // The soft glow the player carries at night
    void EnsureLantern()
    {
        if (lantern != null || player == null) return;
        lantern = NewPointLight("Lantern", player.transform, new Vector3(0, -0.2f, 0), LanternLight, LanternRadius);
        lantern.intensity = 0f;
    }

    static Light2D NewPointLight(string name, Transform parent, Vector3 localPosition, Color color, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.pointLightInnerRadius = radius * 0.15f;
        light.pointLightOuterRadius = radius;
        light.falloffIntensity = 0.6f;

        // a light made in code must be told which sorting layers it lights: all of them
        var layers = new int[SortingLayer.layers.Length];
        for (int i = 0; i < layers.Length; i++) layers[i] = SortingLayer.layers[i].id;
        light.targetSortingLayers = layers;
        return light;
    }

    void Update()
    {
        if (globalLight == null) return;

        if (!Paused()) hour = Mathf.Repeat(hour + 24f / (dayLengthMinutes * 60f) * Time.deltaTime, 24f);

        // the sky follows the clock outdoors and is plain white indoors; the change is blended so it never pops
        Color target = outdoors ? SkyColor(hour) : Color.white;
        currentColor = Color.Lerp(currentColor, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        globalLight.color = currentColor;

        // the lights come on as it gets dark: the darker the sky, the brighter they are
        float night = outdoors ? NightFactor(SkyColor(hour)) : 0f;
        foreach (var light in buildingLights) light.intensity = night * BuildingLightIntensity;
        if (lantern != null) lantern.intensity = night * LanternIntensity;
        if (starShower != null) starShower.Intensity = night;
        AudioManager.SetNight(IsNight(hour));
    }

    // The clock stands still while something else has the player's attention
    bool Paused()
    {
        return (dialogSystem != null && dialogSystem.Active)
            || (battleSystem != null && battleSystem.Active)
            || (evolutionSystem != null && evolutionSystem.Active)
            || (monsterIndex != null && monsterIndex.IsOpen)
            || TitleMenu.IsOpen
            || (intro != null && intro.IsPlaying);
    }

    static Color SkyColor(float hour)
    {
        for (int i = 1; i < Sky.Length; i++)
        {
            if (hour > Sky[i].hour) continue;
            float t = Mathf.InverseLerp(Sky[i - 1].hour, Sky[i].hour, hour);
            return Color.Lerp(Sky[i - 1].color, Sky[i].color, t);
        }
        return Sky[Sky.Length - 1].color;
    }

    // 0 in daylight, 1 in the middle of the night
    static float NightFactor(Color sky)
    {
        float brightness = sky.r * 0.299f + sky.g * 0.587f + sky.b * 0.114f;
        return 1f - Mathf.InverseLerp(0.35f, 0.85f, brightness);
    }

#if UNITY_EDITOR
    // Runs when the component is added (or via Reset in the component menu): finds the two sprite materials of the 2D renderer
    void Reset()
    {
        litMaterial = FindMaterial("Sprite-Lit-Default");
        unlitMaterial = FindMaterial("Sprite-Unlit-Default");
    }

    static Material FindMaterial(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        return null;
    }
#endif
}
