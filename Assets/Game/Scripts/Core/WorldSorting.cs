using UnityEngine;

// Draw order, ported from WORLD_LAYERS in settings.py and AllSprites.draw in groups.py.
// Everything uses the Default sorting layer; the sorting order decides who is drawn on top.
public static class WorldSorting
{
    public const int Water = -10;       // WORLD_LAYERS['water']
    public const int Terrain = 0;       // 'bg': Terrain, then Terrain Top (importer orders 0 and 1)
    public const int Coast = 1;
    public const int Bg = 2;            // sand patches live in 'bg', above terrain and coast
    public const int MainBase = 10;     // 'main': sorted by y_sort, so lower on screen means drawn later
    public const int Top = 20000;       // 'top': always above the player

    // ySortPx is the sprite's rect.centery in pygame pixels (y grows downwards)
    public static int Main(float ySortPx)
    {
        return MainBase + Mathf.RoundToInt(ySortPx);
    }

    public static float PixelsFromUnityY(float unityY)
    {
        return -unityY * 64f;
    }
}
