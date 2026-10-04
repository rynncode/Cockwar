using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 11: Procedural terrain (Worms W.M.D. style).
/// Step 12: Destructible terrain — see CarveCircle near the bottom.
///
/// The ground is stored as a grid of solid/empty pixels (the "solid" array),
/// NOT as a height per column. That is what allows overhangs and arches now,
/// and lets step 12 erase circular craters later.
///
/// How a map is built:
///  1. A rolling "surface line" is made for every column: gentle hills, plus
///     the occasional tall tower, tapering down toward the left/right edges.
///     Everything below that line is solid, so the land is ONE connected mass.
///  2. A 2D noise pushes the surface up and down by a few units depending on
///     height as well as position. That is what makes overhangs, arches and
///     chunky cliffs instead of a smooth hill outline.
///  3. A few thin tunnels are carved deep inside the ground (optional).
///  4. Tiny floating specks are deleted and tiny air pockets are filled in.
///  5. The grid is painted into a texture using your grass and dirt tiles (a
///     random variant per 16-pixel cell) and shown with a Sprite. Grass goes
///     ONLY on the original top surface (the first ground met from the sky in
///     each column, remembered in grassDepthMap). Craters, cave floors and
///     overhang undersides are plain dirt, and stay dirt after being carved.
///  6. A PolygonCollider2D is built from a hidden "mask" sprite of just the solid
///     pixels, so decorations like grass blades never affect collisions.
///  7. Every cockroach in the TurnManager's list is placed on flat open ground.
///
/// How a crater is carved (step 12):
///  1. CarveCircle flips solid pixels to false inside a circle. Only removes
///     ground, never adds it back.
///  2. Only the columns the circle actually touched are repainted (not the
///     whole map), using the same tile-picking logic as the full build, kept
///     around as cached fields so it does not need to re-read your tiles.
///  3. The hidden mask sprite is recreated from the updated pixels — Unity
///     bakes a sprite's physics outline in at creation time, so just
///     re-applying texture pixels does NOT update the collider by itself.
///  4. The PolygonCollider2D is rebuilt from that new mask sprite.
///
/// Keep this object's Scale at (1,1,1) and Rotation at 0. The collider and
/// the spawn maths both assume that.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Map Size")]
    [Tooltip("Map width in world units. This project's world is large: at max camera zoom (100) you see roughly 350 units across.")]
    public float mapWidth = 500f;

    [Tooltip("Map height in world units.")]
    public float mapHeight = 220f;

    [Tooltip("Texture pixels per world unit. Higher = smoother edges but slower to generate. 2 gives a 1000x440 texture at the default size.")]
    public int pixelsPerUnit = 2;

    [Header("Seed")]
    [Tooltip("When true, every Play session makes a new random map (the seed is printed in the Console). Turn off to keep reusing the Seed below.")]
    public bool randomSeedEachGame = true;

    [Tooltip("Same seed = same map. Copy a seed from the Console if you like a map and want it back.")]
    public int seed = 12345;

    [Header("Landmass: Hills")]
    [Tooltip("Average ground height as a fraction of the map height (0 = bottom, 1 = top).")]
    [Range(0.1f, 0.8f)]
    public float groundLevel = 0.38f;

    [Tooltip("How far the hills rise and dip around the average ground height, in world units.")]
    public float hillHeight = 50f;

    [Tooltip("Horizontal size of one hill in world units. Bigger = long lazy slopes. Smaller = choppy.")]
    public float hillSize = 150f;

    [Header("Landmass: Towers")]
    [Tooltip("Extra height of the tallest towers/mountains, in world units. 0 = no towers.")]
    public float towerHeight = 60f;

    [Tooltip("Horizontal size of the tower pattern in world units.")]
    public float towerSize = 80f;

    [Tooltip("How rare towers are. Higher = fewer towers. Around 0.55 to 0.7 works well.")]
    [Range(0.4f, 0.8f)]
    public float towerThreshold = 0.6f;

    [Header("Landmass: Overhangs and Arches")]
    [Tooltip("How far, in world units, the surface can be pushed up or down by the 2D noise. Higher = more overhangs, arches and ragged cliffs. 0 = smooth hills only.")]
    public float overhangStrength = 20f;

    [Tooltip("Size of the overhang pattern in world units. Smaller = busier cliffs.")]
    public float overhangSize = 40f;

    [Header("Landmass: Edges and Cleanup")]
    [Tooltip("Distance from the left and right edges over which the land slopes down, in world units.")]
    public float edgeFadeDistance = 70f;

    [Tooltip("Ground height at the very edges of the map, as a fraction of the map height. Water (step 14) will cover the low parts later.")]
    [Range(0f, 0.5f)]
    public float edgeLevel = 0.12f;

    [Tooltip("Floating chunks and air pockets smaller than this many square world units are removed. Raise it for a cleaner map.")]
    public float minRegionArea = 200f;

    [Header("Caves")]
    [Tooltip("How much of the underground is allowed to have tunnels, 0 to 1. 0 = no caves at all. Low values give a few rare tunnels.")]
    [Range(0f, 1f)]
    public float caveAmount = 0.3f;

    [Tooltip("Width of the winding tunnels. Try 0.02 to 0.08.")]
    [Range(0f, 0.2f)]
    public float caveWidth = 0.03f;

    [Tooltip("Size of the cave pattern in world units. Bigger = longer, lazier tunnels.")]
    public float caveFeatureSize = 50f;

    [Tooltip("Caves only start this many world units below the surface, so they never wreck the walkable top.")]
    public float caveMinDepth = 30f;

    [Header("Look")]
    [Tooltip("Your grass-edge tiles (Grass-1, Grass-2, Grass-3). One is picked at random for each tile-wide stretch of ground surface. All tiles in all three lists must be the same size. In each tile's import settings turn ON Read/Write and set Compression to None. Leave empty to use a plain green edge.")]
    public Texture2D[] grassTiles;

    [Tooltip("Your plain dirt tiles (Dirt-1, Dirt-2, Dirt-3). They fill everything below the grass edge, one picked at random per tile-sized cell. Leave empty to use plain brown colours.")]
    public Texture2D[] dirtTiles;

    [Tooltip("Optional rare dirt tiles, like the skull. Now and then one of these replaces a normal dirt tile.")]
    public Texture2D[] rareDirtTiles;

    [Tooltip("Chance that a dirt cell uses a rare tile instead of a normal one. 0.03 = about 3 in 100 cells.")]
    [Range(0f, 0.2f)]
    public float rareDirtChance = 0.03f;

    [Tooltip("Each pixel of your art becomes this many terrain pixels (a square). With Pixels Per Unit = 2, a value of 2 makes one art pixel exactly 1 world unit, so the 16 pixel tile is 16 units wide. Raise it to make the art bigger and chunkier.")]
    [Range(1, 8)]
    public int terrainPixelsPerArtPixel = 2;

    [Tooltip("How many rows at the top of your tile are the grass part (including the blade tips). Everything below is treated as dirt and tiled. For Grass-2 use 8.")]
    public int grassRows = 8;

    [Tooltip("How many of those top rows are blade tips that stick up ABOVE the ground line (they are drawn only, never solid). For Grass-2 use 2.")]
    public int grassTipRows = 2;

    [Tooltip("Keep the pixel art sharp (no smoothing). Recommended for pixel art.")]
    public bool crispPixels = true;

    [Tooltip("Fallback colours, used only when no Ground Texture is assigned.")]
    public Color dirtColorLight = new Color(0.55f, 0.38f, 0.22f, 1f);
    public Color dirtColorDark = new Color(0.40f, 0.26f, 0.15f, 1f);

    [Tooltip("Fallback grass colour, used only when no Ground Texture is assigned.")]
    public Color grassColor = new Color(0.30f, 0.62f, 0.20f, 1f);

    [Tooltip("Fallback grass thickness in world units, used only when no Ground Texture is assigned. 0 = none.")]
    public float grassThickness = 2f;

    [Tooltip("Sorting order of the terrain sprite. Keep it below your cockroaches so they draw in front.")]
    public int terrainSortingOrder = -10;

    [Header("Player Spawning")]
    [Tooltip("The TurnManager whose Players list should be placed on the map.")]
    public TurnManager turnManager;

    [Tooltip("World units above the surface to drop each cockroach from, so none start stuck inside the ground.")]
    public float spawnDropHeight = 2f;

    [Tooltip("Spawn spots must be flat for this many world units to each side of the spot.")]
    public float spawnFlatHalfWidth = 6f;

    [Tooltip("How much the ground height may change within that flat area, in world units.")]
    public float spawnMaxSlope = 4f;

    [Tooltip("Keep spawn spots this far from the left and right edges, in world units.")]
    public float spawnEdgeMargin = 60f;

    [Tooltip("Shuffle which player gets which part of the map (otherwise player 1 is always on the far left).")]
    public bool randomizeSpawnOrder = true;

    /// <summary>
    /// Step 12: lets Explosion (and anything else) find the one terrain in the
    /// scene without an expensive search. Set automatically in Awake.
    /// </summary>
    public static TerrainGenerator Instance { get; private set; }

    // --- Generated data ---
    private int widthPx;
    private int heightPx;

    // One entry per pixel: true = solid ground. Index = y * widthPx + x.
    // CarveCircle flips entries to false and triggers a repaint.
    private bool[] solid;

    private System.Random rng;
    private float offsetX, offsetY;
    private float caveOffsetX, caveOffsetY;
    private float dirtOffsetX, dirtOffsetY;

    // Kept so we can clean up the old ones when regenerating.
    private Texture2D texture;
    private Sprite sprite;

    // Hidden black-and-white version of the map (opaque = solid). The collider is
    // built from this, not from the painted texture, so grass blades don't collide.
    private Texture2D maskTexture;
    private Sprite maskSprite;

    // Size of one art tile in pixels. All tiles must match; set by LoadTiles.
    private int tileW;
    private int tileH;

    // --- Step 12: cached paint buffers and settings ---
    // Kept after a full generation so CarveCircle can repaint just the
    // columns that changed, instead of redoing the whole map and re-reading
    // every tile from disk again.
    private Color32[] pixels;
    private Color32[] maskPixels;

    // For every pixel: how many pixels below the ORIGINAL top surface it is, if it is
    // part of the grass layer (0 = the very top pixel), or NotGrass if it is just dirt.
    // Worked out once when the map is generated and never changed by craters, so ground
    // exposed by an explosion later stays dirt instead of growing a new grass edge.
    private byte[] grassDepthMap;
    private const byte NotGrass = 255;

    private List<Color32[]> grassData = new List<Color32[]>();
    private List<Color32[]> dirtData = new List<Color32[]>();
    private List<Color32[]> rareData = new List<Color32[]>();
    private bool useGrassArt;
    private bool useDirtArt;
    private int paintScale;
    private int artGrassRows;
    private int artTipRows;
    private int grassPxFallback;
    private Color32 clearPixel;
    private Color32 maskClearPixel;
    private Color32 maskSolidPixel;

    private void Awake()
    {
        Instance = this;

        GenerateTerrain();
        PlacePlayers();
    }

    /// <summary>
    /// Right-click this component's header in the Inspector while the game is
    /// running and choose "Regenerate Terrain" to roll a new map without
    /// restarting. Handy for tuning the settings above.
    /// </summary>
    [ContextMenu("Regenerate Terrain")]
    private void RegenerateInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("TerrainGenerator: Regenerate Terrain only works while the game is running.");
            return;
        }

        GenerateTerrain();
        PlacePlayers();
    }

    // ------------------------------------------------------------------
    // Generation
    // ------------------------------------------------------------------

    private void GenerateTerrain()
    {
        if (randomSeedEachGame)
        {
            seed = Random.Range(0, 1000000);
        }

        rng = new System.Random(seed);
        offsetX = (float)(rng.NextDouble() * 10000.0);
        offsetY = (float)(rng.NextDouble() * 10000.0);
        caveOffsetX = (float)(rng.NextDouble() * 10000.0);
        caveOffsetY = (float)(rng.NextDouble() * 10000.0);
        dirtOffsetX = (float)(rng.NextDouble() * 10000.0);
        dirtOffsetY = (float)(rng.NextDouble() * 10000.0);

        widthPx = Mathf.RoundToInt(mapWidth * pixelsPerUnit);
        heightPx = Mathf.RoundToInt(mapHeight * pixelsPerUnit);
        solid = new bool[widthPx * heightPx];

        Debug.Log("TerrainGenerator: seed " + seed + ", " + widthPx + "x" + heightPx + " pixels.");

        BuildShape();

        // Delete floating specks, then fill pin-hole air pockets.
        int minPixels = Mathf.RoundToInt(minRegionArea * pixelsPerUnit * pixelsPerUnit);
        if (minPixels > 0)
        {
            RemoveSmallRegions(true, minPixels);
            RemoveSmallRegions(false, minPixels);
        }

        BuildTextureAndSprite();
        BuildCollider();
    }

    /// <summary>
    /// Decides, pixel by pixel, whether it is solid.
    /// Step A: work out a smooth surface height for every column (hills + towers + edge slope).
    /// Step B: for pixels near that surface, let a 2D noise push the surface up or
    ///         down, which is what creates overhangs and arches. Pixels far below the
    ///         surface are always solid and pixels far above it are always empty,
    ///         so the land stays one big connected mass.
    /// Step C: carve a few tunnels deep underground.
    /// </summary>
    private void BuildShape()
    {
        // ----- Step A: the surface line, one height per column -----
        float[] surfaceHeight = new float[widthPx];

        for (int x = 0; x < widthPx; x++)
        {
            float worldX = x / (float)pixelsPerUnit;

            // Gentle rolling hills. The constant y values (5.5, 17.3) just pick a different
            // "row" of the noise for each purpose so hills and towers do not match up.
            float hills = FractalNoise(worldX / hillSize, 5.5f, offsetX, offsetY, 3);
            float surface = groundLevel * mapHeight + (hills - 0.5f) * 2f * hillHeight;

            // Occasional tall towers: only where this noise rises above the threshold.
            float towerNoise = FractalNoise(worldX / towerSize, 17.3f, offsetX, offsetY, 2);
            float towerAmount = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(towerThreshold, towerThreshold + 0.15f, towerNoise));
            surface += towerAmount * towerHeight;

            // Slope the land down toward the left and right edges.
            float distToSide = Mathf.Min(worldX, mapWidth - worldX);
            float edgeT = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(distToSide / Mathf.Max(0.01f, edgeFadeDistance)));
            surface = Mathf.Lerp(edgeLevel * mapHeight, surface, edgeT);

            surfaceHeight[x] = surface;
        }

        // ----- Steps B and C: fill the grid -----
        bool cavesOn = caveAmount > 0f && caveWidth > 0f;
        // Where the "tunnels allowed here" noise must be above to carve. More caveAmount = lower bar.
        float caveRegionBar = Mathf.Lerp(0.8f, 0.4f, caveAmount);

        for (int y = 0; y < heightPx; y++)
        {
            float worldY = y / (float)pixelsPerUnit;

            for (int x = 0; x < widthPx; x++)
            {
                float worldX = x / (float)pixelsPerUnit;

                // Positive = this pixel is below the smooth surface, negative = above it.
                float inside = surfaceHeight[x] - worldY;

                bool isSolid;
                if (inside > overhangStrength)
                {
                    isSolid = true;     // deep enough that noise cannot change it
                }
                else if (inside < -overhangStrength)
                {
                    isSolid = false;    // high enough above the surface to always be sky
                }
                else
                {
                    // Near the surface: noise (0..1, centred on 0.5) shifts the surface up or down.
                    float n = FractalNoise(
                        worldX / overhangSize, worldY / overhangSize,
                        offsetX, offsetY, 3);

                    isSolid = inside + (n - 0.5f) * 2f * overhangStrength > 0f;
                }

                // Tunnels: only deep underground, and only in regions the mask allows,
                // so caves are occasional instead of everywhere.
                if (isSolid && cavesOn && inside > caveMinDepth)
                {
                    float region = FractalNoise(
                        worldX / (caveFeatureSize * 2f), worldY / (caveFeatureSize * 2f),
                        caveOffsetX + 500f, caveOffsetY + 500f, 1);

                    if (region > caveRegionBar)
                    {
                        float cave = FractalNoise(
                            worldX / caveFeatureSize, worldY / caveFeatureSize,
                            caveOffsetX, caveOffsetY, 2);

                        if (Mathf.Abs(cave - 0.5f) < caveWidth)
                        {
                            isSolid = false;
                        }
                    }
                }

                solid[y * widthPx + x] = isSolid;
            }
        }
    }

    /// <summary>
    /// Layers several Perlin noise samples at increasing detail and returns a
    /// value between roughly 0.2 and 0.8, centred on 0.5.
    /// </summary>
    private float FractalNoise(float x, float y, float offX, float offY, int octaveCount)
    {
        const float persistence = 0.5f;

        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxAmplitude = 0f;

        for (int o = 0; o < Mathf.Max(1, octaveCount); o++)
        {
            total += Mathf.PerlinNoise(x * frequency + offX, y * frequency + offY) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        return total / maxAmplitude;
    }

    /// <summary>
    /// Finds every connected blob of pixels that are currently "targetState"
    /// (true = solid, false = empty) and flips any blob smaller than minPixels.
    /// With targetState = true this deletes floating specks; with false it fills
    /// tiny air pockets. Uses a flood fill that spreads to the 4 neighbours.
    /// </summary>
    private void RemoveSmallRegions(bool targetState, int minPixels)
    {
        int total = widthPx * heightPx;
        bool[] visited = new bool[total];
        int[] queue = new int[total];

        for (int start = 0; start < total; start++)
        {
            if (visited[start] || solid[start] != targetState) continue;

            // Flood fill this blob. 'queue' doubles as the list of its pixels.
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = true;

            while (head < tail)
            {
                int current = queue[head++];
                int cx = current % widthPx;
                int cy = current / widthPx;
                int neighbour;

                if (cx > 0)
                {
                    neighbour = current - 1;
                    if (!visited[neighbour] && solid[neighbour] == targetState)
                    {
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }
                if (cx < widthPx - 1)
                {
                    neighbour = current + 1;
                    if (!visited[neighbour] && solid[neighbour] == targetState)
                    {
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }
                if (cy > 0)
                {
                    neighbour = current - widthPx;
                    if (!visited[neighbour] && solid[neighbour] == targetState)
                    {
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }
                if (cy < heightPx - 1)
                {
                    neighbour = current + widthPx;
                    if (!visited[neighbour] && solid[neighbour] == targetState)
                    {
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }
            }

            // 'tail' is now the size of the blob. Too small? Flip all of it.
            if (tail < minPixels)
            {
                for (int i = 0; i < tail; i++)
                {
                    solid[queue[i]] = !targetState;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Visuals
    // ------------------------------------------------------------------

    /// <summary>
    /// Paints the solid grid into two textures:
    ///  - the visible one (your art, or plain colours where no art is assigned), and
    ///  - a hidden black/white mask used only to build the collider.
    /// This is the FULL build, used after generating a new map. CarveCircle
    /// uses RepaintColumns instead, which reuses the cached data this sets up.
    /// </summary>
    private void BuildTextureAndSprite()
    {
        pixels = new Color32[widthPx * heightPx];
        maskPixels = new Color32[widthPx * heightPx];

        PreparePaintData();
        BuildGrassMap();

        for (int x = 0; x < widthPx; x++)
        {
            PaintColumn(x);
        }

        // Second pass: grass blade tips, drawn in the empty pixels directly
        // above the ground. Needs every column's ground already painted first,
        // which the loop above just did.
        for (int x = 0; x < widthPx; x++)
        {
            PaintColumnTips(x);
        }

        // Throw away last map's textures/sprites (only ones we made ourselves).
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
        if (maskSprite != null) Destroy(maskSprite);
        if (maskTexture != null) Destroy(maskTexture);

        Rect fullRect = new Rect(0, 0, widthPx, heightPx);

        // Visible texture. Pivot (0.5, 0.5) = this object's position is the CENTRE of the map.
        texture = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false);
        texture.filterMode = crispPixels ? FilterMode.Point : FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();

        sprite = Sprite.Create(texture, fullRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = terrainSortingOrder;

        // Hidden mask: never drawn. The last argument asks Unity to generate a
        // physics outline from it, which BuildCollider reads.
        maskTexture = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false);
        maskTexture.wrapMode = TextureWrapMode.Clamp;
        maskTexture.SetPixels32(maskPixels);
        maskTexture.Apply();

        maskSprite = Sprite.Create(maskTexture, fullRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.Tight, Vector4.zero, true);
    }

    /// <summary>
    /// Reads every assigned tile once and works out how to paint with them
    /// (which rows are grass, which are dirt, and so on). Cached into fields
    /// so CarveCircle can repaint columns later without doing this again.
    /// </summary>
    private void PreparePaintData()
    {
        tileW = 0;
        tileH = 0;
        grassData.Clear();
        dirtData.Clear();
        rareData.Clear();

        LoadTiles(grassTiles, grassData);
        LoadTiles(dirtTiles, dirtData);
        LoadTiles(rareDirtTiles, rareData);

        // Tiles need at least 2 rows to be split into "grass part" and "dirt part".
        if (tileH < 2)
        {
            grassData.Clear();
            dirtData.Clear();
            rareData.Clear();
        }

        useGrassArt = grassData.Count > 0;
        useDirtArt = dirtData.Count > 0;
        paintScale = Mathf.Max(1, terrainPixelsPerArtPixel);

        // How a grass tile is split: top rows = grass part (below that the dirt tiles take over).
        artGrassRows = 0;
        artTipRows = 0;
        if (useGrassArt)
        {
            artGrassRows = Mathf.Clamp(grassRows, 1, tileH - 1);
            artTipRows = Mathf.Clamp(grassTipRows, 0, artGrassRows);
        }

        // Empty pixels use the dirt colour with alpha 0, so smoothed edges
        // do not get a dark or white fringe.
        Color clearColor = dirtColorDark;
        clearColor.a = 0f;
        clearPixel = clearColor;

        maskClearPixel = new Color32(255, 255, 255, 0);
        maskSolidPixel = new Color32(255, 255, 255, 255);

        // Only used when there are no grass tiles.
        grassPxFallback = grassThickness <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(grassThickness * pixelsPerUnit));
    }

    /// <summary>
    /// Marks which pixels are the grass layer: in each column, the first ground met
    /// scanning down from the sky, and the few pixels just below it (as thick as the
    /// grass part of your tile). Everything else, including cave floors, the tops of
    /// lower ledges, and any ground a crater exposes later, is not grass.
    /// Called once per full map build, after the shape is final.
    /// </summary>
    private void BuildGrassMap()
    {
        grassDepthMap = new byte[widthPx * heightPx];
        for (int i = 0; i < grassDepthMap.Length; i++)
        {
            grassDepthMap[i] = NotGrass;
        }

        // How thick the grass layer is, in terrain pixels: the tile's grass rows
        // (not counting the blade tips, which are drawn above the ground).
        int bandPx = useGrassArt ? (artGrassRows - artTipRows) * paintScale : grassPxFallback;
        bandPx = Mathf.Clamp(bandPx, 0, NotGrass - 1);
        if (bandPx <= 0) return;

        for (int x = 0; x < widthPx; x++)
        {
            int top = FindSurfaceY(x, heightPx - 1);
            if (top < 0) continue; // no ground in this column

            for (int depth = 0; depth < bandPx; depth++)
            {
                int y = top - depth;
                if (y < 0 || !solid[y * widthPx + x]) break; // thin ground: stop where it ends

                grassDepthMap[y * widthPx + x] = (byte)depth;
            }
        }
    }

    /// <summary>
    /// Paints one column of the "pixels" and "maskPixels" arrays from the
    /// current "solid" data: grass on the original top surface, dirt everywhere else.
    /// Self-contained — does not read or depend on neighbouring columns —
    /// so CarveCircle can safely call this for just the columns it changed.
    /// </summary>
    private void PaintColumn(int x)
    {
        // Position in "art pixels", and which tile-wide cell this column is in.
        int artPixelX = x / paintScale;
        int artX = tileW > 0 ? artPixelX % tileW : 0;
        int cellX = tileW > 0 ? artPixelX / tileW : 0;

        // One grass variant per cell, so a stretch of surface looks like one tile.
        Color32[] grassTile = useGrassArt ? grassData[PickIndex(cellX, 0, grassData.Count, 1)] : null;

        for (int y = heightPx - 1; y >= 0; y--)
        {
            int index = y * widthPx + x;

            if (!solid[index])
            {
                pixels[index] = clearPixel;
                maskPixels[index] = maskClearPixel;
                continue;
            }

            maskPixels[index] = maskSolidPixel;

            // 1) The grass edge: ONLY pixels that belong to the original top surface.
            //    depthIndex = how far below that original surface this pixel is. Ground that
            //    a crater has exposed is not in the grass map, so it falls through to dirt.
            int depthIndex = grassDepthMap[index];
            if (depthIndex != NotGrass)
            {
                if (useGrassArt)
                {
                    // Tip rows sit above the ground line, so the first solid pixel starts below them.
                    int grassRow = depthIndex / paintScale + artTipRows;
                    if (grassRow < artGrassRows)
                    {
                        pixels[index] = SampleArt(grassTile, tileW, tileH, artX, grassRow);
                        continue;
                    }
                }
                else
                {
                    pixels[index] = grassColor;
                    continue;
                }
            }

            // 2) Everything below the grass: dirt.
            if (useDirtArt)
            {
                // Dirt cells are anchored to the world (not the surface),
                // so the pebbles don't shear apart on slopes.
                int artPixelY = y / paintScale;
                int cellY = artPixelY / tileH;
                int rowFromTop = tileH - 1 - (artPixelY % tileH);

                Color32[] dirtTile;
                if (rareData.Count > 0 && Hash01(cellX, cellY, 2) < rareDirtChance)
                {
                    dirtTile = rareData[PickIndex(cellX, cellY, rareData.Count, 3)];
                }
                else
                {
                    dirtTile = dirtData[PickIndex(cellX, cellY, dirtData.Count, 4)];
                }

                pixels[index] = SampleArt(dirtTile, tileW, tileH, artX, rowFromTop);
            }
            else
            {
                // Cheap speckle so the fallback dirt is not one flat colour.
                float t = Mathf.PerlinNoise(x * 0.04f + dirtOffsetX, y * 0.04f + dirtOffsetY);
                pixels[index] = Color.Lerp(dirtColorDark, dirtColorLight, t);
            }
        }
    }

    /// <summary>
    /// Draws grass blade tips for one column, in the empty pixels directly
    /// ABOVE the ground. Only goes into the visible texture, never the mask,
    /// so blade tips have no collision. Call PaintColumn for this column first.
    /// </summary>
    private void PaintColumnTips(int x)
    {
        if (!useGrassArt || artTipRows <= 0) return;

        int tipPx = artTipRows * paintScale;

        int artPixelX = x / paintScale;
        int artX = artPixelX % tileW;
        int cellX = artPixelX / tileW;
        Color32[] grassTile = grassData[PickIndex(cellX, 0, grassData.Count, 1)];

        for (int y = 0; y < heightPx; y++)
        {
            int index = y * widthPx + x;
            if (!solid[index]) continue;

            // Blade tips only grow on the original top surface (depth 0), never on
            // ground that a crater or cave has exposed.
            if (grassDepthMap[index] != 0) continue;

            // And only where there is open air directly above it.
            bool airAbove = (y == heightPx - 1) || !solid[index + widthPx];
            if (!airAbove) continue;

            for (int h = 1; h <= tipPx; h++)
            {
                int ty = y + h;
                if (ty >= heightPx) break;

                int tipIndex = ty * widthPx + x;
                if (solid[tipIndex]) break;   // ran into other ground

                int tipRow = artTipRows - 1 - (h - 1) / paintScale;
                Color32 c = SampleArt(grassTile, tileW, tileH, artX, tipRow);
                if (c.a > 0) pixels[tipIndex] = c;
            }
        }
    }

    /// <summary>
    /// Repaints just the given range of columns (inclusive) and pushes the
    /// result to the GPU, then rebuilds the collider from a fresh mask
    /// sprite. Used by CarveCircle so a shot does not repaint the whole map.
    /// </summary>
    private void RepaintColumns(int xStart, int xEnd)
    {
        xStart = Mathf.Clamp(xStart, 0, widthPx - 1);
        xEnd = Mathf.Clamp(xEnd, 0, widthPx - 1);

        for (int x = xStart; x <= xEnd; x++)
        {
            PaintColumn(x);
        }
        for (int x = xStart; x <= xEnd; x++)
        {
            PaintColumnTips(x);
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        maskTexture.SetPixels32(maskPixels);
        maskTexture.Apply();

        // A sprite's physics outline is baked in when the sprite is created,
        // so re-applying the texture above does NOT update the collider by
        // itself. The mask sprite has to be recreated for BuildCollider to
        // see the new shape. The visible sprite does not need this, since it
        // has no physics shape.
        if (maskSprite != null) Destroy(maskSprite);

        Rect fullRect = new Rect(0, 0, widthPx, heightPx);
        maskSprite = Sprite.Create(maskTexture, fullRect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.Tight, Vector4.zero, true);

        BuildCollider();
    }

    /// <summary>
    /// Step 12: erases a circle of ground (never adds it back), then repaints
    /// only the columns the circle touched and rebuilds the collider.
    /// </summary>
    /// <param name="worldPosition">Centre of the crater, in world space (e.g. an explosion's position).</param>
    /// <param name="radiusWorldUnits">Crater radius in world units.</param>
    public void CarveCircle(Vector2 worldPosition, float radiusWorldUnits)
    {
        if (solid == null) return; // Terrain has not been generated yet.

        // World position to pixel position: the inverse of PixelToWorld.
        float localX = (worldPosition.x - transform.position.x) * pixelsPerUnit + widthPx * 0.5f;
        float localY = (worldPosition.y - transform.position.y) * pixelsPerUnit + heightPx * 0.5f;
        float radiusPx = Mathf.Max(1f, radiusWorldUnits * pixelsPerUnit);

        int minX = Mathf.Clamp(Mathf.FloorToInt(localX - radiusPx), 0, widthPx - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(localX + radiusPx), 0, widthPx - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(localY - radiusPx), 0, heightPx - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(localY + radiusPx), 0, heightPx - 1);

        float radiusPxSqr = radiusPx * radiusPx;
        bool anyChanged = false;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - localX;
                float dy = y - localY;
                if (dx * dx + dy * dy > radiusPxSqr) continue;

                int index = y * widthPx + x;
                if (solid[index])
                {
                    solid[index] = false;
                    anyChanged = true;
                }
            }
        }

        // Blast landed entirely in open air, or entirely off the map: nothing to repaint.
        if (!anyChanged) return;

        RepaintColumns(minX, maxX);
    }

    /// <summary>
    /// Reads the pixels of every assigned texture into the destination list.
    /// The first readable tile sets the tile size; tiles of a different size are skipped.
    /// </summary>
    private void LoadTiles(Texture2D[] source, List<Color32[]> destination)
    {
        if (source == null) return;

        foreach (Texture2D tile in source)
        {
            if (tile == null) continue;

            Color32[] data;
            try
            {
                data = tile.GetPixels32();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("TerrainGenerator: could not read the tile '" + tile.name + "'. In its import settings turn ON Read/Write and set Compression to None. Skipping it. (" + e.Message + ")");
                continue;
            }

            if (tileW == 0)
            {
                tileW = tile.width;
                tileH = tile.height;
            }

            if (tile.width != tileW || tile.height != tileH)
            {
                Debug.LogWarning("TerrainGenerator: the tile '" + tile.name + "' is " + tile.width + "x" + tile.height + " but the first tile was " + tileW + "x" + tileH + ". All tiles must be the same size. Skipping it.");
                continue;
            }

            destination.Add(data);
        }
    }

    /// <summary>
    /// Reads one pixel of a tile. artRow counts from the TOP of the image
    /// (like in an art program); Unity stores textures bottom-up, so it is flipped here.
    /// </summary>
    private static Color32 SampleArt(Color32[] tile, int tw, int th, int artX, int artRow)
    {
        int row = Mathf.Clamp(artRow, 0, th - 1);
        return tile[(th - 1 - row) * tw + artX];
    }

    /// <summary>
    /// Turns a cell position plus the map seed into a repeatable "random" number
    /// (always the same for the same cell and seed). 'salt' keeps different uses independent.
    /// </summary>
    private int CellHash(int cellX, int cellY, int salt)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + cellX;
            h = h * 31 + cellY;
            h = h * 31 + salt;
            h ^= h >> 15;
            h *= (int)0x2c1b3c6d;
            h ^= h >> 12;
            h *= (int)0x297a2d39;
            h ^= h >> 15;
            return h & 0x7fffffff;
        }
    }

    /// <summary>A repeatable random index from 0 to count - 1 for this cell.</summary>
    private int PickIndex(int cellX, int cellY, int count, int salt)
    {
        return CellHash(cellX, cellY, salt) % count;
    }

    /// <summary>A repeatable random number from 0 to 1 for this cell.</summary>
    private float Hash01(int cellX, int cellY, int salt)
    {
        return (CellHash(cellX, cellY, salt) % 10000) / 10000f;
    }

    // ------------------------------------------------------------------
    // Collider
    // ------------------------------------------------------------------

    /// <summary>
    /// Copies the hidden mask sprite's physics outline into the PolygonCollider2D.
    /// Islands and cave holes each become their own path.
    /// </summary>
    private void BuildCollider()
    {
        PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();

        int shapeCount = maskSprite.GetPhysicsShapeCount();
        if (shapeCount == 0)
        {
            Debug.LogError("TerrainGenerator: the mask sprite has no physics outline, so there is no collider. Cockroaches will fall through the map.");
            polygonCollider.pathCount = 0;
            return;
        }

        polygonCollider.pathCount = shapeCount;

        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < shapeCount; i++)
        {
            points.Clear();
            maskSprite.GetPhysicsShape(i, points);
            polygonCollider.SetPath(i, points);
        }

        Debug.Log("TerrainGenerator: collider built from " + shapeCount + " outline shape(s).");
    }

    // ------------------------------------------------------------------
    // Spawning
    // ------------------------------------------------------------------

    /// <summary>
    /// Splits the map into one slot per player and puts each cockroach on the
    /// surface somewhere inside its slot, on flat ground with open sky above.
    /// </summary>
    private void PlacePlayers()
    {
        if (turnManager == null || turnManager.players == null || turnManager.players.Count == 0)
        {
            Debug.LogWarning("TerrainGenerator: no TurnManager (or no players) assigned, so nobody was placed on the map.");
            return;
        }

        int count = turnManager.players.Count;

        // Which slot each player gets (0 = leftmost).
        List<int> slotOrder = new List<int>();
        for (int i = 0; i < count; i++) slotOrder.Add(i);

        if (randomizeSpawnOrder)
        {
            // Fisher-Yates shuffle, using the seeded generator so a seed reproduces the same layout.
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int temp = slotOrder[i];
                slotOrder[i] = slotOrder[j];
                slotOrder[j] = temp;
            }
        }

        float usableWidth = mapWidth - 2f * spawnEdgeMargin;
        float slotWidth = usableWidth / count;

        for (int i = 0; i < count; i++)
        {
            CockroachMovement player = turnManager.players[i];
            if (player == null) continue;

            float slotStart = spawnEdgeMargin + slotOrder[i] * slotWidth;

            int foundX = -1;
            int foundSurfaceY = -1;

            // Try random spots inside the slot until one is flat enough.
            for (int attempt = 0; attempt < 60 && foundX < 0; attempt++)
            {
                float worldX = slotStart + (float)rng.NextDouble() * slotWidth;
                int px = Mathf.Clamp(Mathf.RoundToInt(worldX * pixelsPerUnit), 0, widthPx - 1);

                if (IsGoodSpawnColumn(px, out int surfaceY))
                {
                    foundX = px;
                    foundSurfaceY = surfaceY;
                }
            }

            // Fallback: no flat spot found, so settle for the slot's centre column.
            if (foundX < 0)
            {
                foundX = Mathf.Clamp(Mathf.RoundToInt((slotStart + slotWidth * 0.5f) * pixelsPerUnit), 0, widthPx - 1);
                foundSurfaceY = FindSurfaceY(foundX, heightPx - 1);
                Debug.LogWarning("TerrainGenerator: no flat spawn spot found for " + player.name + ". Using the slot centre. Try raising Spawn Max Slope or lowering Overhang Strength.");
            }

            if (foundSurfaceY < 0)
            {
                Debug.LogWarning("TerrainGenerator: no ground at all in " + player.name + "'s slot. Skipping.");
                continue;
            }

            Vector3 position = PixelToWorld(foundX, foundSurfaceY + 1);
            position.y += spawnDropHeight;
            position.z = player.transform.position.z;

            player.transform.position = position;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// A column is a good spawn spot if its top surface is flat for
    /// spawnFlatHalfWidth to each side. "Top surface" means the first solid
    /// pixel found scanning down from the top of the map, so it always has open
    /// sky above it and is never inside a cave.
    /// </summary>
    private bool IsGoodSpawnColumn(int px, out int surfaceY)
    {
        surfaceY = FindSurfaceY(px, heightPx - 1);
        if (surfaceY < 0) return false;

        int halfWidthPx = Mathf.RoundToInt(spawnFlatHalfWidth * pixelsPerUnit);
        int slopePx = Mathf.RoundToInt(spawnMaxSlope * pixelsPerUnit);

        for (int dx = -halfWidthPx; dx <= halfWidthPx; dx++)
        {
            int nx = px + dx;
            if (nx < 0 || nx >= widthPx) return false;

            // Start just above the allowed slope. If we land on that very start
            // pixel, the neighbour is a wall taller than allowed, so it fails.
            int startY = surfaceY + slopePx + 1;
            int neighbourY = FindSurfaceY(nx, startY);

            if (neighbourY < 0) return false;
            if (neighbourY > surfaceY + slopePx) return false;
            if (neighbourY < surfaceY - slopePx) return false;
        }

        return true;
    }

    /// <summary>Scans down a column from startY and returns the first solid pixel's y, or -1 if none.</summary>
    private int FindSurfaceY(int x, int startY)
    {
        for (int y = Mathf.Min(startY, heightPx - 1); y >= 0; y--)
        {
            if (solid[y * widthPx + x]) return y;
        }
        return -1;
    }

    /// <summary>Converts a pixel position in the grid to a world position.</summary>
    private Vector3 PixelToWorld(int px, int py)
    {
        return transform.position + new Vector3(
            (px - widthPx * 0.5f) / pixelsPerUnit,
            (py - heightPx * 0.5f) / pixelsPerUnit,
            0f);
    }

    /// <summary>Draws the map's bounds in the Scene view so you can see where it will appear.</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(mapWidth, mapHeight, 0f));
    }
}