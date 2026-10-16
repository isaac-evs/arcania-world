using System.Collections.Generic;
using SuperTiled2Unity;
using UnityEngine;

// Port of the 'Objects', 'Collisions' and 'Monsters' (grass) parts of Game.setup in main.py.
// Attach to the imported map root (the "world" object), next to OverworldAnimations.
public class OverworldObjects : MonoBehaviour
{
    const float TileSize = 64f;
    const float StairSlack = 24f;         // px of extra room on each side of a stair, along the ridge
    const float StairSlackSideways = 44f; // stairs on vertical ridges are crossed sideways, and the 68 px tall hitbox needs more room
    const float StairReach = 64f;  // px the opening continues past each end of the stair tile, across the ridge

    readonly List<Rect> borderRects = new List<Rect>();

    [SerializeField] bool addMissingRidges = true;
    [SerializeField] bool blockWater = true; // not in the Python game: the player can't walk into the water

    void Awake()
    {
        WorldColliders.Rects.Clear();
        borderRects.Clear();

        foreach (var layer in GetComponentsInChildren<SuperObjectLayer>())
        {
            if (layer.name == "Objects") SetupObjects(layer);
            else if (layer.name == "Collisions") SetupCollisions(layer);
            else if (layer.name == "Monsters") SetupGrass(layer);
            else if (layer.name == "Water" && blockWater) SetupWater(layer);
        }

        if (addMissingRidges) SetupExtraRidges();

        // The gaps in the original ridges are exactly as wide as the player's hitbox, so open them up a little
        var stairs = ExtraCollisions.Stairs;
        for (int i = 0; i < stairs.Length; i += 3)
        {
            bool vertical = stairs[i + 2] == 1;
            // along the ridge: a bit wider than the stair; across the ridge: a tile further each way so the walls flaring out stay clear too
            float padX = vertical ? StairReach : StairSlack, padY = vertical ? StairSlackSideways : StairReach;
            Carve(MakeRect(stairs[i] - padX, stairs[i + 1] - padY, TileSize + 2 * padX, TileSize + 2 * padY));
        }
        WorldColliders.Rects.AddRange(borderRects);
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
            borderRects.Add(MakeRect(obj.m_X, obj.m_Y, obj.m_Width, obj.m_Height));
    }

    // Not in the Python game: ridges that its Collisions layer leaves open (see ExtraCollisions.cs)
    void SetupExtraRidges()
    {
        var data = ExtraCollisions.WorldRidges;
        for (int i = 0; i < data.Length; i += 4)
            borderRects.Add(MakeRect(data[i], data[i + 1], data[i + 2], data[i + 3]));
    }

    void SetupWater(SuperObjectLayer layer)
    {
        // The rectangles that OverworldAnimations fills with water tiles...
        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
            AddCollider(obj.m_X, obj.m_Y, obj.m_Width, obj.m_Height);

        // ...plus the water and rocks of the Coast tiles around them, so the shore is where the player stops
        var edges = ExtraCollisions.WaterEdges;
        for (int i = 0; i < edges.Length; i += 4)
            AddCollider(edges[i], edges[i + 1], edges[i + 2], edges[i + 3]);
    }

    void SetupGrass(SuperObjectLayer layer)
    {
        // MonsterPatchSprite: not solid, y_sort lowered by 40. Python draws 'sand' biome patches flat on the ground ('bg'),
        // but all 44 of them sit on grass terrain (a quirk of the map data), so they are y-sorted like the rest and overlap the player.
        foreach (var obj in layer.GetComponentsInChildren<SuperObject>())
        {
            var sr = obj.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;
            sr.sortingOrder = WorldSorting.Main(CenterY(obj) - 40f);
        }
    }

    void AddCollider(float x, float y, float width, float height)
    {
        WorldColliders.Rects.Add(MakeRect(x, y, width, height));
    }

    // x, y, width, height in Tiled pixels with a top-left origin, converted to Unity units
    Rect MakeRect(float x, float y, float width, float height)
    {
        var rect = new Rect(x / TileSize, -(y + height) / TileSize, width / TileSize, height / TileSize);
        rect.position += (Vector2)transform.position;
        return rect;
    }

    // Removes the area of cut from every border rectangle, keeping the leftover pieces
    void Carve(Rect cut)
    {
        for (int i = borderRects.Count - 1; i >= 0; i--)
        {
            var r = borderRects[i];
            if (!r.Overlaps(cut)) continue;
            borderRects.RemoveAt(i);

            float x0 = Mathf.Max(r.xMin, cut.xMin), x1 = Mathf.Min(r.xMax, cut.xMax);
            if (r.xMin < cut.xMin) borderRects.Add(Rect.MinMaxRect(r.xMin, r.yMin, cut.xMin, r.yMax));
            if (r.xMax > cut.xMax) borderRects.Add(Rect.MinMaxRect(cut.xMax, r.yMin, r.xMax, r.yMax));
            if (r.yMin < cut.yMin) borderRects.Add(Rect.MinMaxRect(x0, r.yMin, x1, cut.yMin));
            if (r.yMax > cut.yMax) borderRects.Add(Rect.MinMaxRect(x0, cut.yMax, x1, r.yMax));
        }
    }

    // Select the world object while playing to see every collision rectangle in the Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (var rect in WorldColliders.Rects)
            Gizmos.DrawWireCube(rect.center, rect.size);
    }
}
