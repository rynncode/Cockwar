using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small pixel-art sprites made in code for the screen HUD (turn timer, player bubbles),
/// using the same pixel font as PixelNumber so the HUD matches the labels in the world.
/// Every sprite is white where it should take a color and black for its outline,
/// so a UI Image's color tints the fill and leaves the outline dark.
/// Sprites are cached, so asking for the same one twice costs nothing.
/// </summary>
public static class PixelSprites
{
    // Same as a Canvas's default Reference Pixels Per Unit, so a sliced Image's border
    // is (border pixels / Image.pixelsPerUnitMultiplier) UI units wide.
    private const float PixelsPerUnit = 100f;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Text in the pixel font: white letters with a 1 pixel black outline, 7 pixels tall.</summary>
    public static Sprite Text(string text) => Get("text:" + text, () => BuildText(text));

    /// <summary>
    /// A box with a black outline and cut corners, white inside. 9-sliced with a 2 pixel border,
    /// so it stretches to any size; set Image.pixelsPerUnitMultiplier = 1 / (UI units per pixel).
    /// </summary>
    public static Sprite Panel() => Get("panel", () => FromColorRows(new[]
    {
        ".KKK.",
        "KKWKK",
        "KWWWK",
        "KKWKK",
        ".KKK.",
    }, new Vector4(2f, 2f, 2f, 2f)));

    /// <summary>Same shape as Panel but solid white with no outline, for glows and shadows. 9-sliced the same way.</summary>
    public static Sprite Block() => Get("block", () => FromColorRows(new[]
    {
        ".WWW.",
        "WWWWW",
        "WWWWW",
        "WWWWW",
        ".WWW.",
    }, new Vector4(2f, 2f, 2f, 2f)));

    /// <summary>
    /// Speech-bubble tail pointing down, 7 x 4 pixels. Its top row is meant to sit on top of
    /// the Panel's bottom outline, so the bubble and its tail join without a line between them.
    /// </summary>
    public static Sprite TailDown() => Get("tail", () => FromColorRows(new[]
    {
        "KWWWWWK",
        ".KWWWK.",
        "..KWK..",
        "...K...",
    }, Vector4.zero));

    /// <summary>Arrow head pointing right, 6 x 7 pixels with its outline.</summary>
    public static Sprite ArrowRight() => Get("arrow", () => FromMask(new[]
    {
        "1000",
        "1100",
        "1110",
        "1100",
        "1000",
    }));

    private static Sprite Get(string key, System.Func<Sprite> build)
    {
        // The null check also covers sprites destroyed when leaving Play mode.
        if (cache.TryGetValue(key, out Sprite sprite) && sprite != null)
            return sprite;

        sprite = build();
        cache[key] = sprite;
        return sprite;
    }

    private static Sprite BuildText(string text)
    {
        string[] rows = new string[5];
        for (int r = 0; r < rows.Length; r++)
            rows[r] = "";

        bool first = true;
        foreach (char c in text)
        {
            if (!PixelNumber.TryGetPattern(c, out string[] pattern))
                continue;

            for (int r = 0; r < rows.Length; r++)
                rows[r] += (first ? "" : "0") + pattern[r]; // 1 pixel gap between letters

            first = false;
        }

        if (first)
            rows = new[] { "0", "0", "0", "0", "0" }; // nothing drawable: a blank glyph

        return FromMask(rows);
    }

    /// <summary>Rows of '1' (filled) and '0', top to bottom; adds a 1 pixel black outline all round.</summary>
    private static Sprite FromMask(string[] rows)
    {
        int maskWidth = rows[0].Length;
        int width = maskWidth + 2;
        int height = rows.Length + 2;

        // y = 0 is the BOTTOM row; rows are written top to bottom.
        bool[,] filled = new bool[width, height];
        for (int row = 0; row < rows.Length; row++)
        {
            int y = 1 + (rows.Length - 1 - row);
            for (int col = 0; col < maskWidth; col++)
                filled[1 + col, y] = rows[row][col] == '1';
        }

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (filled[x, y])
                    pixels[y * width + x] = White;
                else if (TouchesFilled(filled, x, y, width, height))
                    pixels[y * width + x] = Black;
                else
                    pixels[y * width + x] = Clear;
            }
        }

        return MakeSprite(pixels, width, height, Vector4.zero);
    }

    /// <summary>Rows of 'K' (black), 'W' (white) and '.' (clear), top to bottom.</summary>
    private static Sprite FromColorRows(string[] rows, Vector4 border)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        Color32[] pixels = new Color32[width * height];

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                char c = rows[row][x];
                pixels[y * width + x] = c == 'K' ? Black : c == 'W' ? White : Clear;
            }
        }

        return MakeSprite(pixels, width, height, border);
    }

    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Black = new Color32(0, 0, 0, 255);
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    private static Sprite MakeSprite(Color32[] pixels, int width, int height, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // keep the pixels sharp
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static bool TouchesFilled(bool[,] filled, int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && ny >= 0 && nx < width && ny < height && filled[nx, ny])
                    return true;
            }
        }

        return false;
    }
}
