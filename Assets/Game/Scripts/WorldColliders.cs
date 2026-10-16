using System.Collections.Generic;
using UnityEngine;

// The collision_sprites group from main.py: static hitboxes, in Unity units
public static class WorldColliders
{
    public static readonly List<Rect> Rects = new List<Rect>();

    // What blocks a character's line of sight: the full image rects of the objects and the original Collisions layer
    // (Character.collision_rects in entities.py), not the shrunken hitboxes and not the extra collisions added in Unity
    public static readonly List<Rect> SightBlockers = new List<Rect>();

    // Rect.Overlaps with a tiny tolerance. After a collision snaps the player flush against a wall, float32 rounding
    // can leave a 1e-7 overlap; without the tolerance the wall counts as a hit on the other axis too, and the player
    // is snapped to the end of the wall (a "teleport").
    const float Epsilon = 1e-4f;

    public static bool Overlap(Rect a, Rect b)
    {
        return a.xMax > b.xMin + Epsilon && a.xMin < b.xMax - Epsilon
            && a.yMax > b.yMin + Epsilon && a.yMin < b.yMax - Epsilon;
    }
}
