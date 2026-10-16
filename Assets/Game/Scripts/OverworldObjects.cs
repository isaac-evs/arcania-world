using SuperTiled2Unity;
using UnityEngine;

// Port of the 'Objects', 'Collisions' and 'Monsters' (grass) parts of Game.setup in main.py.
// Attach to the imported map root (the "world" object), next to OverworldAnimations.
public class OverworldObjects : MonoBehaviour
{
    const float TileSize = 64f;

    [SerializeField] bool addMissingRidges = true;

    void Awake()
    {
        WorldColliders.Rects.Clear();

        foreach (var layer in GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name == "Objects") SetupObjects(layer);
            else if (layer.name == "Collisions") SetupCollisions(layer);
            else if (layer.name == "Monsters") SetupGrass(layer);
        }

        if (addMissingRidges) SetupExtraRidges();
    }

    // Tiled tile objects are positioned by their bottom-left corner; pygame (pytmx) gives the top-left.
    // Returns rect.centery in pygame pixels.
    static float CenterY(SuperObject obj)
    {
        return obj.m_Y - obj.m_Height / 2f;
    }

    void SetupObjects(SuperObjectLayer layer)
    {
        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
        {
            var sr = obj.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;

            if (obj.m_TiledName == "top")
            {
                // Sprite(..., WORLD_LAYERS['top']): drawn over the player, no collision
                sr.sortingOrder = WorldSorting.Top;
                continue;
            }

            // CollidableSprite: y-sorted, and the hitbox is the rect shrunk to 40% of its height (inflate(0, -height * 0.6))
            float centerY = CenterY(obj);
            sr.sortingOrder = WorldSorting.Main(centerY);

            float hitboxHeight = obj.m_Height * 0.4f;
            AddCollider(obj.m_X, centerY - hitboxHeight / 2f, obj.m_Width, hitboxHeight);
        }
    }

    void SetupCollisions(SuperObjectLayer layer)
    {
        // BorderSprite: the Tiled rectangle itself (x, y is the top-left corner)
        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            AddCollider(obj.m_X, obj.m_Y, obj.m_Width, obj.m_Height);
    }

    // Not in the Python game: ridges that its Collisions layer leaves open (see ExtraCollisions.cs)
    void SetupExtraRidges()
    {
        var data = ExtraCollisions.WorldRidges;
        for (int i = 0; i < data.Length; i += 4)
            AddCollider(data[i], data[i + 1], data[i + 2], data[i + 3]);
    }

    void SetupGrass(SuperObjectLayer layer)
    {
        // MonsterPatchSprite: not solid. Sand patches are drawn in 'bg', the rest in 'main' with y_sort lowered by 40
        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
        {
            var sr = obj.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;

            var props = obj.GetComponent<SuperCustomProperties>();
            bool sand = props != null && props.TryGetCustomProperty("biome", out var biome) && biome.m_Value == "sand";
            sr.sortingOrder = sand ? WorldSorting.Bg : WorldSorting.Main(CenterY(obj) - 40f);
        }
    }

    // x, y, width, height in Tiled pixels with a top-left origin
    void AddCollider(float x, float y, float width, float height)
    {
        var origin = (Vector2)transform.position;
        var rect = new Rect(x / TileSize, -(y + height) / TileSize, width / TileSize, height / TileSize);
        rect.position += origin;
        WorldColliders.Rects.Add(rect);
    }

    // Select the world object while playing to see every collision rectangle in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (var rect in WorldColliders.Rects)
            Gizmos.DrawWireCube(rect.center, rect.size);
    }
}
