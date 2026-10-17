using UnityEngine;

// Pixel-art helpers that work on the pixels of a (readable) sprite
public static class PixelArt
{
    // pygame.transform.scale2x: the Scale2x (AdvanceMAME) algorithm. Every pixel becomes 2 x 2; a corner takes the colour of a
    // neighbour when two neighbours agree and the others don't, so diagonal edges come out smooth instead of blocky.
    public static Sprite Scale2x(Sprite sprite)
    {
        if (!sprite.texture.isReadable)
            throw new System.InvalidOperationException($"The texture '{sprite.texture.name}' is not readable. Use Reset on the component that owns it, which turns on Read/Write.");

        var rect = sprite.rect;
        int width = (int)rect.width, height = (int)rect.height;
        var all = sprite.texture.GetPixels32();

        // rows from the top, like pygame's y axis
        uint[,] source = new uint[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = all[((int)rect.y + (height - 1 - y)) * sprite.texture.width + (int)rect.x + x];
                source[x, y] = (uint)(c.r | c.g << 8 | c.b << 16 | c.a << 24);
            }
        }

        uint At(int x, int y) => source[Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1)];

        int outWidth = width * 2, outHeight = height * 2;
        var output = new Color32[outWidth * outHeight];

        void Put(int x, int y, uint value)
        {
            output[(outHeight - 1 - y) * outWidth + x] = new Color32((byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24));
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint b = At(x, y - 1), d = At(x - 1, y), e = At(x, y), f = At(x + 1, y), h = At(x, y + 1);

                Put(x * 2, y * 2, d == b && b != f && d != h ? d : e);
                Put(x * 2 + 1, y * 2, b == f && b != d && f != h ? f : e);
                Put(x * 2, y * 2 + 1, d == h && d != b && h != f ? d : e);
                Put(x * 2 + 1, y * 2 + 1, h == f && d != h && b != f ? f : e);
            }
        }

        var texture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(output);
        texture.Apply();
        return UiKit.SpriteFrom(texture, new Rect(0, 0, outWidth, outHeight));
    }
}
