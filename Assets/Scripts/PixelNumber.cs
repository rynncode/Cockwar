using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws text in the world with a tiny built-in pixel font (digits, A-Z, minus, space and "!"),
/// matching the pixelated P1 / P2 labels. Used for the health number on the health bar,
/// the floating "-10" damage labels and the "WINNER" text.
/// (The name is from when it only did numbers.)
/// Each glyph is a small sprite with a dark outline, so it reads on any background.
/// No font or image assets are needed.
/// </summary>
public class PixelNumber
{
    private const int GlyphHeight = 5;

    // Each glyph sprite has a 1 pixel outline on every side.
    private const int OutlineSize = 1;
    private const int SpriteHeight = GlyphHeight + OutlineSize * 2;

    // Digits are 3 pixels wide, and the gap between glyphs is 1 pixel, so one digit takes 4.
    private const int DigitAdvance = 4;

    /// <summary>Height of one glyph in font pixels, outline included.</summary>
    public const int HeightInPixels = SpriteHeight;

    /// <summary>Width in font pixels of a number with this many digits, outline included.</summary>
    public static int WidthInPixels(int digitCount) => digitCount * DigitAdvance + 1;

    /// <summary>Width in font pixels of any text, outline included.</summary>
    public static int MeasureWidthInPixels(string text)
    {
        int width = 0;
        int count = 0;

        foreach (char c in text)
        {
            if (!Patterns.TryGetValue(char.ToUpperInvariant(c), out string[] pattern))
                continue;

            width += pattern[0].Length + 1;
            count++;
        }

        return count == 0 ? 0 : width - 1 + OutlineSize * 2;
    }

    // 5 rows per glyph, top to bottom. 1 = filled pixel. Width = length of a row.
    // Most letters are 3 wide; M, N and W are 5 wide so they stay readable.
    private static readonly Dictionary<char, string[]> Patterns = new Dictionary<char, string[]>
    {
        { '0', new[] { "111", "101", "101", "101", "111" } },
        { '1', new[] { "010", "110", "010", "010", "111" } },
        { '2', new[] { "111", "001", "111", "100", "111" } },
        { '3', new[] { "111", "001", "111", "001", "111" } },
        { '4', new[] { "101", "101", "111", "001", "001" } },
        { '5', new[] { "111", "100", "111", "001", "111" } },
        { '6', new[] { "111", "100", "111", "101", "111" } },
        { '7', new[] { "111", "001", "001", "010", "010" } },
        { '8', new[] { "111", "101", "111", "101", "111" } },
        { '9', new[] { "111", "101", "111", "001", "111" } },
        { '-', new[] { "000", "000", "111", "000", "000" } },
        { ' ', new[] { "00", "00", "00", "00", "00" } },
        { '!', new[] { "1", "1", "1", "0", "1" } },
        { 'A', new[] { "010", "101", "111", "101", "101" } },
        { 'B', new[] { "110", "101", "110", "101", "110" } },
        { 'C', new[] { "011", "100", "100", "100", "011" } },
        { 'D', new[] { "110", "101", "101", "101", "110" } },
        { 'E', new[] { "111", "100", "110", "100", "111" } },
        { 'F', new[] { "111", "100", "110", "100", "100" } },
        { 'G', new[] { "011", "100", "101", "101", "011" } },
        { 'H', new[] { "101", "101", "111", "101", "101" } },
        { 'I', new[] { "111", "010", "010", "010", "111" } },
        { 'J', new[] { "001", "001", "001", "101", "010" } },
        { 'K', new[] { "101", "101", "110", "101", "101" } },
        { 'L', new[] { "100", "100", "100", "100", "111" } },
        { 'M', new[] { "10001", "11011", "10101", "10001", "10001" } },
        { 'N', new[] { "10001", "11001", "10101", "10011", "10001" } },
        { 'O', new[] { "111", "101", "101", "101", "111" } },
        { 'P', new[] { "110", "101", "110", "100", "100" } },
        { 'Q', new[] { "111", "101", "101", "111", "001" } },
        { 'R', new[] { "110", "101", "110", "101", "101" } },
        { 'S', new[] { "011", "100", "010", "001", "110" } },
        { 'T', new[] { "111", "010", "010", "010", "010" } },
        { 'U', new[] { "101", "101", "101", "101", "111" } },
        { 'V', new[] { "101", "101", "101", "101", "010" } },
        { 'W', new[] { "10001", "10001", "10101", "10101", "01010" } },
        { 'X', new[] { "101", "101", "010", "101", "101" } },
        { 'Y', new[] { "101", "101", "010", "010", "010" } },
        { 'Z', new[] { "111", "001", "010", "100", "111" } },
    };

    // One set of glyph sprites shared by every PixelNumber.
    private static Dictionary<char, Sprite> sharedSprites;

    private readonly GameObject root;
    private readonly List<SpriteRenderer> glyphRenderers = new List<SpriteRenderer>();
    private readonly List<char> glyphChars = new List<char>();
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

    /// <summary>Centre of the text in the world (or relative to its parent, if it has one).</summary>
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

    /// <summary>Shows the text. Letters are shown as capitals; anything not in the font is ignored.</summary>
    public void SetText(string text)
    {
        if (text == currentText)
            return;

        currentText = text;

        glyphChars.Clear();
        foreach (char c in text)
        {
            char upper = char.ToUpperInvariant(c);
            if (Patterns.ContainsKey(upper))
                glyphChars.Add(upper);
        }

        int count = glyphChars.Count;
        float totalWidth = MeasureWidthInPixels(text) * pixelSize;

        while (glyphRenderers.Count < count)
            glyphRenderers.Add(CreateGlyphRenderer());

        // Each glyph sprite starts where the previous one's fill ends plus a 1 pixel gap.
        // (Neighbouring outlines overlap by one pixel.)
        int offsetPixels = 0;

        for (int i = 0; i < glyphRenderers.Count; i++)
        {
            SpriteRenderer glyphRenderer = glyphRenderers[i];
            bool used = i < count;

            glyphRenderer.gameObject.SetActive(used);
            if (!used)
                continue;

            char c = glyphChars[i];
            glyphRenderer.sprite = sharedSprites[c];

            // Glyph sprites have their pivot on the LEFT edge, so this centres the whole text.
            glyphRenderer.transform.localPosition = new Vector3(offsetPixels * pixelSize - totalWidth * 0.5f, 0f, 0f);

            offsetPixels += Patterns[c][0].Length + 1;
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
        // The '0' sprite being null also covers "destroyed when leaving Play mode".
        if (sharedSprites != null && sharedSprites.TryGetValue('0', out Sprite zero) && zero != null)
            return;

        sharedSprites = new Dictionary<char, Sprite>();

        foreach (KeyValuePair<char, string[]> entry in Patterns)
            sharedSprites[entry.Key] = BuildGlyphSprite(entry.Value);
    }

    private static Sprite BuildGlyphSprite(string[] pattern)
    {
        int glyphWidth = pattern[0].Length;
        int spriteWidth = glyphWidth + OutlineSize * 2;

        // y = 0 is the BOTTOM row; pattern rows are written top to bottom.
        bool[,] filled = new bool[spriteWidth, SpriteHeight];

        for (int row = 0; row < GlyphHeight; row++)
        {
            int y = OutlineSize + (GlyphHeight - 1 - row);
            for (int col = 0; col < glyphWidth; col++)
            {
                if (pattern[row][col] == '1')
                    filled[OutlineSize + col, y] = true;
            }
        }

        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 outline = new Color32(0, 0, 0, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[spriteWidth * SpriteHeight];

        for (int y = 0; y < SpriteHeight; y++)
        {
            for (int x = 0; x < spriteWidth; x++)
            {
                if (filled[x, y])
                    pixels[y * spriteWidth + x] = fill;
                else if (TouchesFilledPixel(filled, x, y, spriteWidth))
                    pixels[y * spriteWidth + x] = outline;
                else
                    pixels[y * spriteWidth + x] = clear;
            }
        }

        Texture2D texture = new Texture2D(spriteWidth, SpriteHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // keep the pixels sharp
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(pixels);
        texture.Apply();

        // Pivot on the left edge, vertically centred; 1 pixel = 1 unit (scaled by pixelSize later).
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, spriteWidth, SpriteHeight),
            new Vector2(0f, 0.5f),
            1f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static bool TouchesFilledPixel(bool[,] filled, int x, int y, int spriteWidth)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= spriteWidth || ny >= SpriteHeight)
                    continue;

                if (filled[nx, ny])
                    return true;
            }
        }

        return false;
    }
}
