using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws numbers (0-9 and a minus sign) in the world with a tiny built-in pixel font,
/// matching the pixelated P1 / P2 labels. Used for the health number on the health bar
/// and the floating "-10" damage labels. No font or image assets are needed.
/// Each glyph is a small sprite with a dark outline, so it reads on any background.
/// </summary>
public class PixelNumber
{
    private const string Characters = "0123456789-";

    private const int GlyphWidth = 3;
    private const int GlyphHeight = 5;

    // Each glyph sprite has a 1 pixel outline on every side.
    private const int SpriteWidth = GlyphWidth + 2;
    private const int SpriteHeight = GlyphHeight + 2;

    // Distance from one glyph to the next: 3 pixels of glyph + 1 pixel gap (neighbouring outlines overlap).
    private const int Advance = GlyphWidth + 1;

    /// <summary>Height of one glyph in font pixels, outline included.</summary>
    public const int HeightInPixels = SpriteHeight;

    /// <summary>Width in font pixels of a number with this many characters, outline included.</summary>
    public static int WidthInPixels(int characterCount) => characterCount * Advance + 1;

    // 3x5 patterns, in the same order as Characters. 1 = filled pixel.
    private static readonly string[][] Patterns =
    {
        new[] { "111", "101", "101", "101", "111" }, // 0
        new[] { "010", "110", "010", "010", "111" }, // 1
        new[] { "111", "001", "111", "100", "111" }, // 2
        new[] { "111", "001", "111", "001", "111" }, // 3
        new[] { "101", "101", "111", "001", "001" }, // 4
        new[] { "111", "100", "111", "001", "111" }, // 5
        new[] { "111", "100", "111", "101", "111" }, // 6
        new[] { "111", "001", "001", "010", "010" }, // 7
        new[] { "111", "101", "111", "101", "111" }, // 8
        new[] { "111", "101", "111", "001", "111" }, // 9
        new[] { "000", "000", "111", "000", "000" }, // -
    };

    // One set of glyph sprites shared by every PixelNumber.
    private static Sprite[] sharedSprites;

    private readonly GameObject root;
    private readonly List<SpriteRenderer> glyphRenderers = new List<SpriteRenderer>();
    private readonly List<int> glyphIndices = new List<int>();
    private readonly float pixelSize;
    private readonly int sortingLayerId;
    private readonly int sortingOrder;

    private string currentText = null;
    private Color tint = Color.white;
    private float alpha = 1f;

    /// <param name="parent">Object to attach to (it then moves with it), or null for a free-standing number.</param>
    /// <param name="pixelSize">World size of ONE font pixel.</param>
    public PixelNumber(Transform parent, string name, int sortingLayerId, int sortingOrder, float pixelSize)
    {
        EnsureSprites();

        this.sortingLayerId = sortingLayerId;
        this.sortingOrder = sortingOrder;
        this.pixelSize = pixelSize;

        root = new GameObject(name);
        if (parent != null)
            root.transform.SetParent(parent, false);
    }

    /// <summary>Centre of the number in the world (or relative to its parent, if it has one).</summary>
    public Transform Transform => root.transform;

    public void SetActive(bool active)
    {
        if (root.activeSelf != active)
            root.SetActive(active);
    }

    public void SetColor(Color color)
    {
        tint = color;
        ApplyColor();
    }

    public void SetAlpha(float newAlpha)
    {
        alpha = Mathf.Clamp01(newAlpha);
        ApplyColor();
    }

    /// <summary>Shows the digits (and minus signs) in the text. Other characters are ignored.</summary>
    public void SetText(string text)
    {
        if (text == currentText)
            return;

        currentText = text;

        glyphIndices.Clear();
        foreach (char c in text)
        {
            int index = Characters.IndexOf(c);
            if (index >= 0)
                glyphIndices.Add(index);
        }

        int count = glyphIndices.Count;
        float totalWidth = WidthInPixels(count) * pixelSize;

        while (glyphRenderers.Count < count)
            glyphRenderers.Add(CreateGlyphRenderer());

        for (int i = 0; i < glyphRenderers.Count; i++)
        {
            SpriteRenderer glyphRenderer = glyphRenderers[i];
            bool used = i < count;

            glyphRenderer.gameObject.SetActive(used);
            if (!used)
                continue;

            glyphRenderer.sprite = sharedSprites[glyphIndices[i]];

            // Glyph sprites have their pivot on the LEFT edge, so this centres the whole number.
            glyphRenderer.transform.localPosition = new Vector3(i * Advance * pixelSize - totalWidth * 0.5f, 0f, 0f);
        }

        ApplyColor();
    }

    public void Destroy()
    {
        if (root != null)
            Object.Destroy(root);
    }

    private SpriteRenderer CreateGlyphRenderer()
    {
        GameObject glyph = new GameObject("Glyph");
        glyph.transform.SetParent(root.transform, false);
        glyph.transform.localScale = new Vector3(pixelSize, pixelSize, 1f);

        SpriteRenderer glyphRenderer = glyph.AddComponent<SpriteRenderer>();
        glyphRenderer.sortingLayerID = sortingLayerId;
        glyphRenderer.sortingOrder = sortingOrder;
        return glyphRenderer;
    }

    private void ApplyColor()
    {
        // The glyph fill is white and the outline is black, so tinting only changes the fill.
        Color color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);

        foreach (SpriteRenderer glyphRenderer in glyphRenderers)
            glyphRenderer.color = color;
    }

    // --- Building the shared glyph sprites (done once) ---

    private static void EnsureSprites()
    {
        // The first sprite being null also covers "destroyed when leaving Play mode".
        if (sharedSprites != null && sharedSprites.Length > 0 && sharedSprites[0] != null)
            return;

        sharedSprites = new Sprite[Patterns.Length];

        for (int i = 0; i < Patterns.Length; i++)
            sharedSprites[i] = BuildGlyphSprite(Patterns[i]);
    }

    private static Sprite BuildGlyphSprite(string[] pattern)
    {
        // y = 0 is the BOTTOM row; pattern rows are written top to bottom.
        bool[,] filled = new bool[SpriteWidth, SpriteHeight];

        for (int row = 0; row < GlyphHeight; row++)
        {
            int y = 1 + (GlyphHeight - 1 - row);
            for (int col = 0; col < GlyphWidth; col++)
            {
                if (pattern[row][col] == '1')
                    filled[1 + col, y] = true;
            }
        }

        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 outline = new Color32(0, 0, 0, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[SpriteWidth * SpriteHeight];

        for (int y = 0; y < SpriteHeight; y++)
        {
            for (int x = 0; x < SpriteWidth; x++)
            {
                if (filled[x, y])
                    pixels[y * SpriteWidth + x] = fill;
                else if (TouchesFilledPixel(filled, x, y))
                    pixels[y * SpriteWidth + x] = outline;
                else
                    pixels[y * SpriteWidth + x] = clear;
            }
        }

        Texture2D texture = new Texture2D(SpriteWidth, SpriteHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // keep the pixels sharp
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(pixels);
        texture.Apply();

        // Pivot on the left edge, vertically centred; 1 pixel = 1 unit (scaled by pixelSize later).
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, SpriteWidth, SpriteHeight),
            new Vector2(0f, 0.5f),
            1f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static bool TouchesFilledPixel(bool[,] filled, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= SpriteWidth || ny >= SpriteHeight)
                    continue;

                if (filled[nx, ny])
                    return true;
            }
        }

        return false;
    }
}
