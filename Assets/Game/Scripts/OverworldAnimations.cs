using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Port of the water + coast setup from main.py. Attach to the imported map root (the "world" object).
// It reads the "Water" and "Coast" object layers that SuperTiled2Unity imported and spawns animated sprites.
public class OverworldAnimations : MonoBehaviour
{
    const int TileSize = 64;

    // same order as coast_importer in support.py
    static readonly string[] CoastTerrains = { "grass", "grass_i", "sand_i", "sand", "rock", "rock_i", "ice", "ice_i" };

    static readonly Dictionary<string, Vector2Int> CoastSides = new Dictionary<string, Vector2Int>
    {
        { "topleft", new Vector2Int(0, 0) },    { "top", new Vector2Int(1, 0) },    { "topright", new Vector2Int(2, 0) },
        { "left", new Vector2Int(0, 1) },                                           { "right", new Vector2Int(2, 1) },
        { "bottomleft", new Vector2Int(0, 2) }, { "bottom", new Vector2Int(1, 2) }, { "bottomright", new Vector2Int(2, 2) },
    };

    [SerializeField] Texture2D[] waterFrames;
    [SerializeField] Texture2D coastSheet;
    [SerializeField] float animationSpeed = 6f; // ANIMATION_SPEED in settings.py
    [SerializeField] int waterSortingOrder = -10; // below Terrain
    [SerializeField] int coastSortingOrder = 1;   // above Terrain, like WORLD_LAYERS['bg'] after the terrain

    class Animated
    {
        public SpriteRenderer renderer;
        public Sprite[] frames;
    }

    readonly List<Animated> animated = new List<Animated>();
    Sprite[] waterSprites;
    int lastFrame = -1;

    void Start()
    {
        waterSprites = new Sprite[waterFrames.Length];
        for (int i = 0; i < waterFrames.Length; i++)
            waterSprites[i] = CreateSprite(waterFrames[i], new Rect(0, 0, waterFrames[i].width, waterFrames[i].height));

        var coastFrames = BuildCoastFrames();

        foreach (var layer in GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name == "Water") SpawnWater(layer);
            else if (layer.name == "Coast") SpawnCoast(layer, coastFrames);
        }
    }

    void SpawnWater(SuperObjectLayer layer)
    {
        var parent = new GameObject("Water Animated").transform;
        parent.SetParent(transform, false);

        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
        {
            // main.py: for x in range(obj.x, obj.x + obj.width, TILE_SIZE) ...
            for (float x = obj.m_X; x < obj.m_X + obj.m_Width; x += TileSize)
                for (float y = obj.m_Y; y < obj.m_Y + obj.m_Height; y += TileSize)
                    Spawn(parent, x, y, waterSprites, waterSortingOrder);
        }
    }

    void SpawnCoast(SuperObjectLayer layer, Dictionary<string, Dictionary<string, Sprite[]>> coastFrames)
    {
        var parent = new GameObject("Coast Animated").transform;
        parent.SetParent(transform, false);

        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
        {
            var props = obj.GetComponent<SuperCustomProperties>();
            if (props == null) continue;
            if (!props.TryGetCustomProperty("terrain", out var terrain) || !props.TryGetCustomProperty("side", out var side)) continue;

            Spawn(parent, obj.m_X, obj.m_Y, coastFrames[terrain.m_Value][side.m_Value], coastSortingOrder);
        }
    }

    void Spawn(Transform parent, float tiledX, float tiledY, Sprite[] frames, int sortingOrder)
    {
        // Tiled: origin top-left, y down. Unity: y up, 1 unit = 1 tile (64 px). Objects are positioned by their top-left corner.
        var go = new GameObject("tile");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((tiledX + TileSize / 2f) / TileSize, -(tiledY + TileSize / 2f) / TileSize, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = sortingOrder;
        animated.Add(new Animated { renderer = sr, frames = frames });
    }

    // coast_importer in support.py: 4 animation frames are 3 rows apart in the 24x12 sheet
    Dictionary<string, Dictionary<string, Sprite[]>> BuildCoastFrames()
    {
        var result = new Dictionary<string, Dictionary<string, Sprite[]>>();
        for (int index = 0; index < CoastTerrains.Length; index++)
        {
            var sides = new Dictionary<string, Sprite[]>();
            foreach (var side in CoastSides)
            {
                var frames = new Sprite[4];
                for (int i = 0; i < 4; i++)
                    frames[i] = CoastCell(side.Value.x + index * 3, side.Value.y + i * 3);
                sides[side.Key] = frames;
            }
            result[CoastTerrains[index]] = sides;
        }
        return result;
    }

    Sprite CoastCell(int col, int row)
    {
        // texture rects start at the bottom-left, grid rows count from the top
        var rect = new Rect(col * TileSize, coastSheet.height - (row + 1) * TileSize, TileSize, TileSize);
        return CreateSprite(coastSheet, rect);
    }

    static Sprite CreateSprite(Texture2D texture, Rect rect)
    {
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), TileSize);
    }

    void Update()
    {
        // every sprite shares the same clock (frame_index += ANIMATION_SPEED * dt in AnimatedSprite)
        int frame = (int)(Time.time * animationSpeed);
        if (frame == lastFrame) return;
        lastFrame = frame;

        foreach (var a in animated)
            a.renderer.sprite = a.frames[frame % a.frames.Length];
    }

#if UNITY_EDITOR
    // Runs when the component is added: fills the texture fields and applies pixel-art import settings
    void Reset()
    {
        waterFrames = new Texture2D[4];
        for (int i = 0; i < 4; i++)
            waterFrames[i] = LoadPixelArt($"Assets/Game/graphics/tilesets/water/{i}.png");
        coastSheet = LoadPixelArt("Assets/Game/graphics/tilesets/coast.png");
    }

    static Texture2D LoadPixelArt(string path)
    {
        // Sprite type avoids Unity rescaling non power of two textures (coast.png is 1536x768)
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TileSize;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
#endif
}
