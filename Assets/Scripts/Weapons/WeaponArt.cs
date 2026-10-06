using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Built-in pixel art for the special weapons, drawn in code with a dark 1-pixel outline like the
/// game's other sprites, so the weapons work without any image files. Every special weapon also has
/// optional sprite slots (in its attack asset) to swap any of these for hand-made art.
/// Each sprite is 1 world unit wide at scale 1; callers scale it. Sprites are cached.
/// </summary>
public static class WeaponArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static readonly Color32 Outline = new Color32(30, 20, 26, 255);

    private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        { 'K', new Color32(30, 20, 26, 255) },     // near-black (seams, eyes)
        { 'O', new Color32(236, 120, 40, 255) },   // orange
        { 'o', new Color32(184, 78, 26, 255) },    // dark orange
        { 'Y', new Color32(255, 214, 74, 255) },   // yellow
        { 'y', new Color32(255, 244, 190, 255) },  // pale yellow (hottest)
        { 'R', new Color32(214, 48, 40, 255) },    // red
        { 'r', new Color32(132, 22, 30, 255) },    // dark red
        { 'W', new Color32(255, 255, 255, 255) },  // white
        { 'w', new Color32(206, 208, 216, 255) },  // light grey
        { 'G', new Color32(140, 142, 156, 255) },  // grey
        { 'g', new Color32(84, 86, 100, 255) },    // dark grey
        { 'N', new Color32(120, 226, 96, 255) },   // alien green
        { 'n', new Color32(48, 140, 62, 255) },    // dark green
        { 'L', new Color32(186, 255, 120, 255) },  // lime glow
        { 'C', new Color32(96, 232, 236, 255) },   // cyan light
        { 'M', new Color32(244, 96, 210, 255) },   // magenta light
        { 'b', new Color32(122, 82, 52, 255) },    // rock brown
        { 'd', new Color32(76, 50, 36, 255) },     // dark rock
        { 'V', new Color32(118, 128, 62, 255) },   // olive (nuke)
        { 'v', new Color32(78, 86, 40, 255) },     // dark olive
    };

    // ---------------- RENEITOR ----------------

    public static Sprite Basketball() => Get("basketball", () => FromRows(new[]
    {
        "....oooo....",
        "..oOOKOOOo..",
        ".oOOOKOOOOo.",
        ".OKOOKOOOKO.",
        "oOOKOKOOKOOo",
        "KKKKKKKKKKKK",
        "oOOKOKOOKOOo",
        ".OKOOKOOOKO.",
        ".oOOOKOOOOo.",
        "..oOOKOOOo..",
        "....oooo....",
    }, true));

    public static Sprite Meteor() => Get("meteor", () => FromRows(new[]
    {
        "....dddd....",
        "..ddbbbbdd..",
        ".dbbObbbbbd.",
        ".dbOYObbdbd.",
        "dbbbObbbbObd",
        "dbdbbbbbOYOd",
        "dbbbbdbbbObd",
        "dbObbbbbbbbd",
        ".dYObbbdbbd.",
        ".dbObbbbbbd.",
        "..ddbbbbdd..",
        "....dddd....",
    }, true));

    // ---------------- SATELASER ----------------

    public static Sprite Ufo() => Get("ufo", () => FromRows(new[]
    {
        "..........nnnnnn..........",
        ".........nLLLLLLn.........",
        "........nLLNNNNLLn........",
        "........nLNKNNKNLn........",
        "....wwwwwwwwwwwwwwwwww....",
        "..wwGGGGGGGGGGGGGGGGGGww..",
        ".gGGMGGGCGGGGGMGGGCGGGMGg.",
        "..ggGGGGGGGGGGGGGGGGGGgg..",
        "....gggggggggggggggggg....",
        ".......gg........gg.......",
    }, true));

    public static Sprite AlienTarget() => Get("alienTarget", () => FromRows(new[]
    {
        "......LLLLL......",
        "....LL.....LL....",
        "...L....L....L...",
        "..L.....L.....L..",
        ".L.............L.",
        ".L....nnnnn....L.",
        "L....nNNNNNn....L",
        "L....nKNNNKn....L",
        "LLLL.nNNNNNn.LLLL",
        "L.....nNNNn.....L",
        "L......nnn......L",
        ".L.............L.",
        ".L.............L.",
        "..L.....L.....L..",
        "...L....L....L...",
        "....LL.....LL....",
        "......LLLLL......",
    }, true));

    // ---------------- AIRSTRIKE ----------------

    public static Sprite Remote(bool pressed) => Get(pressed ? "remoteOn" : "remote", () => FromRows(new[]
    {
        "...............Y",
        "..............g.",
        ".............g..",
        "ggggggggggggg...",
        pressed ? "gGGGrrrGGwGwg..." : "gGGGRRRGGwGwg...",
        pressed ? "gGGrrRrrGGGGg..." : "gGGRRyRRGGGGg...",
        pressed ? "gGGGrrrGGwGwg..." : "gGGGRRRGGwGwg...",
        "ggggggggggggg...",
    }, true));

    public static Sprite Rocket() => Get("rocket", () => FromRows(new[]
    {
        "gg..........",
        "gwwwwwwwwRR.",
        ".GwwwwwwwRRR",
        "gwwwwwwwwRR.",
        "gg..........",
    }, true));

    // ---------------- ALL DEMONS ARE HERE! ----------------

    public static Sprite Pitchfork() => Get("pitchfork", () => FromRows(new[]
    {
        "..............RRRRRRRO",
        "..............R.......",
        "..............R.......",
        "rrrrrrrrrrrrrrRRRRRRRO",
        "..............R.......",
        "..............R.......",
        "..............RRRRRRRO",
    }, true));

    /// <summary>One of three flame frames (0-2), pivot at the bottom middle.</summary>
    public static Sprite Flame(int frame)
    {
        frame = Mathf.Abs(frame) % 3;
        return Get("flame" + frame, () =>
        {
            string[] rows = frame == 2 ? FlameB : FlameA;
            if (frame == 1) rows = Mirror(FlameA);
            return FromRows(rows, true, new Vector2(0.5f, 0f));
        });
    }

    private static readonly string[] FlameA =
    {
        "....R....",
        "...RR....",
        "...ROR...",
        "..ROOR...",
        "..ROYOR..",
        ".RROYOR..",
        ".ROYYOOR.",
        "RROYyYOR.",
        "ROOYyYOOR",
        "ROYYyYYOR",
        "ROYyyyYOR",
        ".ROYyYOR.",
        "..RRRRR..",
    };

    private static readonly string[] FlameB =
    {
        ".......R.",
        "..R...RR.",
        "..RR..ROR",
        ".ROR.ROOR",
        ".ROORROYR",
        "RROYOOYOR",
        "ROYYOYYOR",
        "ROYyYYyOR",
        "ROYyyyyOR",
        "ROYyyyYOR",
        ".ROYyYOR.",
        ".RROOORR.",
        "..RRRRR..",
    };

    // ---------------- DOOOOOOOOOM! ----------------

    public static Sprite Nuke() => Get("nuke", () => FromRows(new[]
    {
        "KK....................",
        "Kv.......vvvvvvvv.....",
        "Kvv....vvVVVVVVVVvv...",
        ".KvvvvvVVVVYYVVVVVVv..",
        "..KvVVVVVVYKKYVVVVVVv.",
        "..KvVVVVVYKYYKYVVVVVv.",
        "..KvVVVVVVYKKYVVVVVVv.",
        ".KvvvvvVVVVYYVVVVVVv..",
        "Kvv....vvVVVVVVVVvv...",
        "Kv.......vvvvvvvv.....",
        "KK....................",
    }, true));

    // ---------------- Shared ----------------

    /// <summary>Soft round blob for smoke and sparks (white, tint it). No outline.</summary>
    public static Sprite Puff() => Get("puff", () => FromRows(new[]
    {
        "..WWWW..",
        ".WWWWWW.",
        "WWWWWWWW",
        "WWWWWWWW",
        "WWWWWWWW",
        "WWWWWWWW",
        ".WWWWWW.",
        "..WWWW..",
    }, false));

    /// <summary>A plain white pixel, for beams and bars stretched to any size.</summary>
    public static Sprite Pixel() => Get("pixel", () => FromRows(new[] { "W" }, false));

    private static string[] Mirror(string[] rows)
    {
        string[] result = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            char[] chars = rows[i].ToCharArray();
            System.Array.Reverse(chars);
            result[i] = new string(chars);
        }
        return result;
    }

    private static Sprite Get(string key, System.Func<Sprite> build)
    {
        // The null check also covers sprites destroyed when leaving Play mode.
        if (cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

        sprite = build();
        cache[key] = sprite;
        return sprite;
    }

    private static Sprite FromRows(string[] rows, bool outline) => FromRows(rows, outline, new Vector2(0.5f, 0.5f));

    /// <summary>
    /// Rows top to bottom, one palette letter per pixel, '.' = clear. Short rows are padded.
    /// With outline on, every clear pixel touching a coloured one becomes the dark outline colour.
    /// </summary>
    private static Sprite FromRows(string[] rows, bool outline, Vector2 pivot)
    {
        int artWidth = 0;
        foreach (string row in rows) artWidth = Mathf.Max(artWidth, row.Length);

        int pad = outline ? 1 : 0;
        int width = artWidth + pad * 2;
        int height = rows.Length + pad * 2;

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] pixels = new Color32[width * height];
        bool[] filled = new bool[width * height];

        for (int row = 0; row < rows.Length; row++)
        {
            int y = height - 1 - pad - row; // textures are stored bottom-up
            for (int col = 0; col < artWidth; col++)
            {
                char c = col < rows[row].Length ? rows[row][col] : '.';
                int x = col + pad;
                if (Palette.TryGetValue(c, out Color32 color))
                {
                    pixels[y * width + x] = color;
                    filled[y * width + x] = true;
                }
                else
                {
                    pixels[y * width + x] = clear;
                }
            }
        }

        if (outline)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (filled[y * width + x]) continue;
                    if (Touches(filled, x, y, width, height)) pixels[y * width + x] = Outline;
                }
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

    private static bool Touches(bool[] filled, int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (dx != 0 && dy != 0) continue; // edges only, so corners stay crisp
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (filled[ny * width + nx]) return true;
            }
        }
        return false;
    }
}
