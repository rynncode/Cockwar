using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Built-in pixel art for supply crates: the crate, its parachute and the supply plane.
/// Drawn in code, so the crate system works without any image assets. Drag your own
/// sprites into CrateSettings to replace any of them. Sprites are cached.
/// Every sprite here is 1 world unit wide at scale 1, so callers scale it to the size they want.
/// </summary>
public static class CrateArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { 'K', new Color32(38, 24, 14, 255) },     // outline
        { 'L', new Color32(196, 132, 66, 255) },   // light wood
        { 'B', new Color32(150, 92, 44, 255) },    // wood
        { 'D', new Color32(104, 60, 28, 255) },    // dark wood
        { 'M', new Color32(170, 172, 184, 255) },  // metal
        { 'R', new Color32(214, 48, 40, 255) },    // red
        { 'W', new Color32(240, 236, 226, 255) },  // white
        { 'S', new Color32(60, 50, 40, 255) },     // string
        { 'G', new Color32(104, 132, 66, 255) },   // plane green
        { 'g', new Color32(72, 94, 46, 255) },     // dark green
        { 'C', new Color32(150, 210, 235, 255) },  // cockpit glass
        { 'Y', new Color32(236, 196, 60, 255) },   // yellow stripe
        { 'P', new Color32(110, 110, 120, 255) },  // propeller
        { 'A', new Color32(246, 196, 64, 255) },   // gold
        { 'a', new Color32(170, 118, 28, 255) },   // dark gold
        { 'N', new Color32(30, 26, 34, 255) },     // near-black
        { 'n', new Color32(56, 50, 60, 255) },     // black-grey
        { 'E', new Color32(236, 236, 240, 255) },  // white body
        { 'e', new Color32(188, 190, 200, 255) },  // light grey
        { 'r', new Color32(150, 30, 30, 255) },    // dark red
        { 'U', new Color32(84, 136, 196, 255) },   // steel blue
        { 'u', new Color32(44, 78, 128, 255) },    // dark steel
        { 'V', new Color32(98, 132, 80, 255) },    // tool green
        { 'v', new Color32(60, 86, 50, 255) },     // dark green
        { 'X', new Color32(214, 216, 226, 255) },  // bright metal
    };

    /// <summary>Wooden crate with metal corners and a cross brace. Pivot in the centre.</summary>
    public static Sprite Crate() => Get("crate", () =>
    {
        const int size = 14;
        string[] rows = new string[size];

        for (int row = 0; row < size; row++)
        {
            char[] line = new char[size];
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || x == size - 1 || row == 0 || row == size - 1;
                bool frame = x <= 2 || x >= size - 3 || row <= 2 || row >= size - 3;
                bool corner = (x <= 2 || x >= size - 3) && (row <= 2 || row >= size - 3);
                bool brace = Mathf.Abs(x - row) <= 1 || Mathf.Abs(x - (size - 1 - row)) <= 1;

                if (edge) line[x] = 'K';
                else if (corner) line[x] = 'M';
                else if (frame) line[x] = row == 2 || x == 2 ? 'L' : (row == size - 3 || x == size - 3 ? 'D' : 'L');
                else if (brace) line[x] = 'L';
                else line[x] = row % 3 == 0 ? 'D' : 'B';
            }
            rows[row] = new string(line);
        }

        return FromRows(rows, new Vector2(0.5f, 0.5f));
    });

    /// <summary>
    /// The crate for a kind. Weapon = the wooden crate above. The others are 15x15 crates in their
    /// own colours with an emblem: Special = black and gold with a gold star, Health = white with a
    /// red cross, Shield = steel blue with a shield, Tool = green with a wrench.
    /// </summary>
    public static Sprite Crate(CrateKind kind)
    {
        switch (kind)
        {
            case CrateKind.Special:
                return Get("crateSpecial", () => EmblemCrate('A', 'a', 'A', 'N', 'n', new[]
                {
                    "...A...",
                    "..AAA..",
                    "AAAAAAA",
                    ".AAAAA.",
                    "..AAA..",
                    ".AA.AA.",
                    "AA...AA",
                }));
            case CrateKind.Health:
                return Get("crateHealth", () => EmblemCrate('E', 'e', 'M', 'E', 'e', new[]
                {
                    "..RRR..",
                    "..RRR..",
                    "RRRRRRR",
                    "RRRRRRR",
                    "RRRRRRR",
                    "..RRR..",
                    "..RRR..",
                }));
            case CrateKind.Shield:
                return Get("crateShield", () => EmblemCrate('U', 'u', 'M', 'U', 'u', new[]
                {
                    "KKKKKKK",
                    "KXXXXXK",
                    "KXXCXXK",
                    "KXCCCXK",
                    ".KXCXK.",
                    "..KXK..",
                    "...K...",
                }));
            case CrateKind.Tool:
                return Get("crateTool", () => EmblemCrate('V', 'v', 'M', 'V', 'v', new[]
                {
                    "....X.X",
                    "....XXX",
                    "...XXX.",
                    "..XXX..",
                    ".XXX...",
                    "XXX....",
                    "XX.....",
                }));
            default:
                return Crate();
        }
    }

    /// <summary>
    /// A 15x15 crate: outline, a 2-pixel frame (light top/left, dark bottom/right), corner plates,
    /// a body with faint horizontal planks, and a 7x7 emblem in the middle.
    /// </summary>
    private static Sprite EmblemCrate(char frameLight, char frameDark, char corner, char body, char bodyStripe, string[] emblem)
    {
        const int size = 15;
        const int emblemStart = 4;   // the 7x7 emblem covers pixels 4..10, centred
        string[] rows = new string[size];

        for (int row = 0; row < size; row++)
        {
            char[] line = new char[size];
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || x == size - 1 || row == 0 || row == size - 1;
                bool frame = x <= 2 || x >= size - 3 || row <= 2 || row >= size - 3;
                bool isCorner = (x <= 2 || x >= size - 3) && (row <= 2 || row >= size - 3);

                int ex = x - emblemStart, ey = row - emblemStart;
                char mark = ex >= 0 && ey >= 0 && ey < emblem.Length && ex < emblem[ey].Length ? emblem[ey][ex] : '.';

                if (edge) line[x] = 'K';
                else if (isCorner) line[x] = corner;
                else if (frame) line[x] = (row >= size - 3 || x >= size - 3) ? frameDark : frameLight;
                else if (mark != '.') line[x] = mark;
                else line[x] = row % 3 == 0 ? bodyStripe : body;
            }
            rows[row] = new string(line);
        }

        return FromRows(rows, new Vector2(0.5f, 0.5f));
    }

    /// <summary>Red and white canopy with strings. Pivot at the bottom middle, where the strings meet the crate.</summary>
    public static Sprite Parachute() => Get("parachute", () => FromRows(new[]
    {
        ".......KKKKKK.......",
        ".....KKRRWWRRKK.....",
        "...KKRRRWWWWRRRKK...",
        "..KRRRRWWWWWWRRRRK..",
        ".KRRRRWWWWWWWWRRRRK.",
        ".KRRRWWWWWWWWWWRRRK.",
        "KRRRRWWWWWWWWWWRRRRK",
        "KKKKKKKKKKKKKKKKKKKK",
        ".S.......SS.......S.",
        "..S......SS......S..",
        "..S......SS......S..",
        "...S.....SS.....S...",
        "...S.....SS.....S...",
        "....S....SS....S....",
        "....S....SS....S....",
        ".....S...SS...S.....",
    }, new Vector2(0.5f, 0f)));

    /// <summary>Small propeller plane facing right. Pivot in the centre.</summary>
    public static Sprite Aircraft() => Get("aircraft", () => FromRows(new[]
    {
        "..KK..........................",
        "..KGK.........................",
        "..KGGK.............KKKK.......",
        "..KGGGK...........KCCCCK......",
        "..KGGGGKKKKKKKKKKKKCCCCCKKK..P",
        ".KGGGGGGGGGGGGGGGGGGGGGGGGGKKP",
        "KggGGGGGGGGGGGGGGGGGGGGGGGGGKP",
        ".KggggYYYYYYYYYYYYYggggggggKKP",
        "..KKggggggggggggggggggggggKK.P",
        "....KKKKggggggggggKKKKKKKKK...",
        ".........KgggggggggK..........",
        "..........KKKKKKKKK...........",
    }, new Vector2(0.5f, 0.5f)));

    private static Sprite Get(string key, System.Func<Sprite> build)
    {
        // The null check also covers sprites destroyed when leaving Play mode.
        if (cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

        sprite = build();
        cache[key] = sprite;
        return sprite;
    }

    /// <summary>Rows top to bottom, one palette letter per pixel, '.' = clear. Short rows are padded.</summary>
    private static Sprite FromRows(string[] rows, Vector2 pivot)
    {
        int width = 0;
        foreach (string row in rows) width = Mathf.Max(width, row.Length);
        int height = rows.Length;

        Color32[] pixels = new Color32[width * height];
        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row; // textures are stored bottom-up
            for (int x = 0; x < width; x++)
            {
                char c = x < rows[row].Length ? rows[row][x] : '.';
                pixels[y * width + x] = Palette.TryGetValue(c, out Color32 color) ? color : new Color32(0, 0, 0, 0);
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();

        // Pixels per unit = width, so the sprite is exactly 1 world unit wide.
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, width);
    }
}
