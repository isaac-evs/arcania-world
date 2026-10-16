using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum Corners { None = 0, TopLeft = 1, TopRight = 2, BottomLeft = 4, BottomRight = 8, Left = TopLeft | BottomLeft, Right = TopRight | BottomRight, All = 15 }

// Which point of a pygame rect a position refers to (get_frect(midleft = ...) and friends)
public enum Anchor { TopLeft, MidLeft, BottomLeft, MidTop, Center, BottomRight }

public struct UiFont
{
    public Font font;
    public int size;
    public int surfaceHeight; // height of a pygame text surface for this font (its linesize)

    public UiFont(Font font, int size, int surfaceHeight) { this.font = font; this.size = size; this.surfaceHeight = surfaceHeight; }
}

// Helpers that build uGUI elements from pygame-style coordinates: pixels, origin top-left, y down,
// inside a 1280x720 panel. This lets the UI code read like the pygame drawing code it was ported from.
public static class UiKit
{
    // Sprites are created with 100 pixels per unit, the canvas' reference value, so one sprite pixel is one canvas pixel
    const float SpritePpu = 100f;

    static readonly Dictionary<string, Sprite> shapes = new Dictionary<string, Sprite>();

    public static RectTransform NewRect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(Mathf.Floor(x), -Mathf.Floor(y));
        rect.sizeDelta = new Vector2(Mathf.Floor(width), Mathf.Floor(height));
        return rect;
    }

    // pygame.draw.rect(surface, color, rect, 0, radius, ...): a filled rectangle with optionally rounded corners
    public static Image Box(Transform parent, float x, float y, float width, float height, Color color, int radius = 0, Corners corners = Corners.All)
    {
        var image = NewRect(parent, "Box", x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (radius > 0 && corners != Corners.None)
        {
            image.sprite = Shape(radius, corners);
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    // draw_bar in support.py: a background rectangle with a progress rectangle on top
    public static void Bar(Transform parent, float x, float y, float width, float height, float value, float maxValue, Color color, Color background, int radius = 1)
    {
        float ratio = width / maxValue;
        float progress = Mathf.Max(0, Mathf.Min(width, value * ratio));
        Box(parent, x, y, width, height, background, radius);
        Box(parent, x, y, progress, height, color, radius);
    }

    // A text surface placed like pygame's get_frect(anchor = (ax, ay)); returns the rect it occupies
    public static Rect Label(Transform parent, string content, UiFont font, Color color, Anchor anchor, float ax, float ay)
    {
        float width = Measure(font, content);
        var rect = Place(anchor, ax, ay, width, font.surfaceHeight);

        var text = NewRect(parent, "Text", rect.x, rect.y, width + 2, rect.height).gameObject.AddComponent<Text>();
        text.font = font.font;
        text.fontSize = font.size;
        text.color = color;
        text.text = content;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        return rect;
    }

    // pygame renders text as the sum of the character advances
    public static float Measure(UiFont font, string content)
    {
        font.font.RequestCharactersInTexture(content, font.size);
        float width = 0f;
        foreach (char c in content)
        {
            if (font.font.GetCharacterInfo(c, out CharacterInfo info, font.size)) width += info.advance;
        }
        return width;
    }

    public static Image Picture(Transform parent, Sprite sprite, Anchor anchor, float ax, float ay)
    {
        var rect = Place(anchor, ax, ay, sprite.rect.width, sprite.rect.height);
        var image = NewRect(parent, "Picture", rect.x, rect.y, rect.width, rect.height).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // Sprite from a texture (or a part of it) with the pixels untouched
    public static Sprite SpriteFrom(Texture2D texture, Rect rect)
    {
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), SpritePpu);
    }

    public static Rect Place(Anchor anchor, float ax, float ay, float width, float height)
    {
        switch (anchor)
        {
            case Anchor.MidLeft:     return new Rect(ax, ay - height / 2f, width, height);
            case Anchor.BottomLeft:  return new Rect(ax, ay - height, width, height);
            case Anchor.MidTop:      return new Rect(ax - width / 2f, ay, width, height);
            case Anchor.Center:      return new Rect(ax - width / 2f, ay - height / 2f, width, height);
            case Anchor.BottomRight: return new Rect(ax - width, ay - height, width, height);
            default:                 return new Rect(ax, ay, width, height);
        }
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    // A 9-sliced rounded rectangle, like pygame's border radius (hard edges, no anti-aliasing)
    static Sprite Shape(int radius, Corners corners)
    {
        string key = radius + ":" + (int)corners;
        if (shapes.TryGetValue(key, out var cached)) return cached;

        int size = radius * 2 + 2;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool left = x < radius, right = x >= size - radius;
                bool top = y >= size - radius, bottom = y < radius; // texture y grows upwards
                Corners corner = left ? (top ? Corners.TopLeft : bottom ? Corners.BottomLeft : Corners.None)
                               : right ? (top ? Corners.TopRight : bottom ? Corners.BottomRight : Corners.None)
                               : Corners.None;

                bool inside = true;
                if (corner != Corners.None && (corners & corner) != 0)
                {
                    float dx = left ? radius - (x + 0.5f) : (x + 0.5f) - (size - radius);
                    float dy = bottom ? radius - (y + 0.5f) : (y + 0.5f) - (size - radius);
                    inside = dx * dx + dy * dy <= radius * radius;
                }
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), SpritePpu, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        shapes[key] = sprite;
        return sprite;
    }
}
