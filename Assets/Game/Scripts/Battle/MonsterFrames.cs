using System.Collections.Generic;
using UnityEngine;

// The frames of one monster sheet (4 x 2: idle row, attack row) plus what the battle derives from them:
// the white silhouettes shown while a monster is active (pygame.mask.from_surface(image).to_surface()) and the
// white outlines drawn around the selected monster (outline_creator in support.py).
public class MonsterFrames
{
    public Sprite[] idle, attack;
    public Sprite[] idleWhite, attackWhite;
    public Sprite[] idleOutline, attackOutline;

    const int OutlineWidth = 4; // BATTLE_OUTLINE_WIDTH in settings.py

    public static MonsterFrames Build(Texture2D sheet)
    {
        var frames = new MonsterFrames
        {
            idle = UiKit.CutRow(sheet, 4, 2, 0),
            attack = UiKit.CutRow(sheet, 4, 2, 1),
        };
        frames.idleWhite = Map(frames.idle, Silhouette);
        frames.attackWhite = Map(frames.attack, Silhouette);
        frames.idleOutline = Map(frames.idle, Outline);
        frames.attackOutline = Map(frames.attack, Outline);
        return frames;
    }

    public Sprite[] Frames(string state) => state == "attack" ? attack : idle;
    public Sprite[] White(string state) => state == "attack" ? attackWhite : idleWhite;
    public Sprite[] Outlines(string state) => state == "attack" ? attackOutline : idleOutline;

    static void RequireReadable(Sprite sprite)
    {
        if (!sprite.texture.isReadable)
            throw new System.InvalidOperationException($"The texture '{sprite.texture.name}' is not readable. Right-click the Battle System component header and choose Reset, which turns on Read/Write for the monster and UI pictures.");
    }

    static Sprite[] Map(Sprite[] source, System.Func<Sprite, Sprite> convert)
    {
        var result = new Sprite[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = convert(source[i]);
        return result;
    }

    // mask.from_surface: pixels with alpha above 127 are part of the mask
    static bool[,] ReadMask(Sprite sprite, out int width, out int height)
    {
        RequireReadable(sprite);
        var rect = sprite.rect;
        width = (int)rect.width;
        height = (int)rect.height;
        var pixels = sprite.texture.GetPixels32();
        int textureWidth = sprite.texture.width;
        var mask = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask[x, y] = pixels[((int)rect.y + y) * textureWidth + (int)rect.x + x].a > 127;
        return mask;
    }

    static Sprite FromMask(bool[,] mask, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = mask[x, y] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        texture.SetPixels32(pixels);
        texture.Apply();
        return UiKit.SpriteFrom(texture, new Rect(0, 0, width, height));
    }

    public static Sprite Silhouette(Sprite sprite)
    {
        var mask = ReadMask(sprite, out int width, out int height);
        return FromMask(mask, width, height);
    }

    // the silhouette stamped at 8 offsets around a (width + 2 * outline) square, centred on the original
    static Sprite Outline(Sprite sprite)
    {
        var mask = ReadMask(sprite, out int width, out int height);
        int w = width + OutlineWidth * 2, h = height + OutlineWidth * 2;
        var outline = new bool[w, h];

        // pygame y grows downwards, the texture's upwards: the offsets are symmetric so the result is the same
        int[,] offsets = { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 2, 1 }, { 2, 2 }, { 1, 2 }, { 0, 2 }, { 0, 1 } };
        for (int k = 0; k < 8; k++)
        {
            int ox = offsets[k, 0] * OutlineWidth, oy = offsets[k, 1] * OutlineWidth;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (mask[x, y]) outline[x + ox, y + oy] = true;
        }
        return FromMask(outline, w, h);
    }

    // pygame.transform.grayscale: luminance, alpha kept
    public static Sprite Grayscale(Sprite sprite)
    {
        RequireReadable(sprite);
        var rect = sprite.rect;
        int width = (int)rect.width, height = (int)rect.height;
        var source = sprite.texture.GetPixels32();
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = source[((int)rect.y + y) * sprite.texture.width + (int)rect.x + x];
                byte gray = (byte)Mathf.RoundToInt(c.r * 0.299f + c.g * 0.587f + c.b * 0.114f);
                pixels[y * width + x] = new Color32(gray, gray, gray, c.a);
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        return UiKit.SpriteFrom(texture, new Rect(0, 0, width, height));
    }
}
