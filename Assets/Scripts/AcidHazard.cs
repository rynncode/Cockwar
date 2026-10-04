using UnityEngine;

/// <summary>
/// Acid hazard: a pool of green acid along the bottom of the map and past both of its
/// edges. Anything with a Health component that sinks into it dies, so a cockroach that
/// is blasted off the edge, or digs down too far, is lost.
///
/// The acid is drawn in code (pixelated, with scrolling waves), so no art is needed.
/// Add this component to an empty GameObject. It needs the TerrainGenerator in the scene,
/// because it sizes itself from the map. It moves its own object to the middle of the acid.
/// </summary>
public class AcidHazard : MonoBehaviour
{
    [Header("Size and position")]
    [Tooltip("How high the acid surface sits above the bottom edge of the map, in world units. Keep it below your lowest ground so the acid only fills the outside and the very bottom. The Scene view shows the acid area when this object is selected.")]
    public float surfaceHeightAboveBottom = 18f;

    [Tooltip("How far the acid reaches past the left and right edges of the map. Make it wider than the camera can see.")]
    public float sideMargin = 500f;

    [Tooltip("How far the acid goes down below the bottom of the map.")]
    public float depthBelow = 300f;

    [Header("Death")]
    [Tooltip("How far the bottom of a cockroach has to be below the acid surface before it dies. 0 = the moment its feet are under the surface.")]
    public float killDepth = 0f;

    [Tooltip("Drag applied to a cockroach that dies in the acid, so it sinks slowly instead of dropping like a stone.")]
    public float sinkDrag = 3f;

    [Header("Look")]
    [Tooltip("World size of one pixel of the acid's art.")]
    public float pixelSize = 0.4f;

    [Tooltip("Main acid color. The highlights and the deep shade are worked out from it.")]
    public Color acidColor = new Color(0.45f, 0.95f, 0.2f, 1f);

    [Tooltip("How see-through the acid is. 1 = solid. Lower lets the ground and cockroach show through a little.")]
    [Range(0.3f, 1f)]
    public float opacity = 0.88f;

    [Tooltip("How fast the waves slide sideways, in world units per second.")]
    public float waveSpeed = 4f;

    [Tooltip("Draw order. It has to be above the terrain and the cockroaches, and below the health bars (they sit 100 above a cockroach).")]
    public int sortingOrder = 50;

    // Art tile sizes, in art pixels.
    private const int TileWidth = 64;
    private const int SurfaceHeight = 20;
    private const int SurfaceMidRow = 12;   // the row of the strip that sits on the surface line
    private const int BodyHeight = 32;

    private float surfaceY;
    private float tileWorldWidth;
    private float centerX;

    private Transform stripA;
    private Transform stripB;
    private float stripBaseY;

    private Texture2D surfaceTexture;
    private Texture2D bodyTexture;
    private Texture2D fadeTexture;
    private Sprite surfaceSprite;
    private Sprite bodySprite;
    private Sprite fadeSprite;

    private void Start()
    {
        TerrainGenerator generator = TerrainGenerator.Instance != null
            ? TerrainGenerator.Instance
            : FindFirstObjectByType<TerrainGenerator>();

        if (generator == null)
        {
            Debug.LogError("AcidHazard needs a TerrainGenerator in the scene to size itself from. The acid is switched off.");
            enabled = false;
            return;
        }

        ComputeRegion(generator, out float left, out float right, out float bottom);

        SpriteRenderer terrainRenderer = generator.GetComponent<SpriteRenderer>();
        int sortingLayerId = terrainRenderer != null ? terrainRenderer.sortingLayerID : 0;

        centerX = (left + right) * 0.5f;
        transform.position = new Vector3(centerX, (surfaceY + bottom) * 0.5f, 0f);

        BuildTrigger(right - left, surfaceY - bottom);
        BuildVisuals(left, right, bottom, sortingLayerId);
    }

    /// <summary>Works out the acid's edges from the map's size and position.</summary>
    private void ComputeRegion(TerrainGenerator generator, out float left, out float right, out float bottom)
    {
        Vector3 mapCenter = generator.transform.position;
        float halfWidth = generator.mapWidth * 0.5f;
        float mapBottom = mapCenter.y - generator.mapHeight * 0.5f;

        left = mapCenter.x - halfWidth - sideMargin;
        right = mapCenter.x + halfWidth + sideMargin;
        surfaceY = mapBottom + surfaceHeightAboveBottom;
        bottom = mapBottom - depthBelow;
    }

    // ---------------- Death ----------------

    private void BuildTrigger(float width, float height)
    {
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(width, height);
        box.offset = Vector2.zero;
    }

    // OnTriggerStay (not Enter) so the check repeats until the cockroach's feet are far enough under.
    private void OnTriggerStay2D(Collider2D other)
    {
        Health health = other.GetComponent<Health>();
        if (health == null || health.IsDead)
            return;

        // The collider's lowest point is at the cockroach's feet.
        if (other.bounds.min.y > surfaceY - killDepth)
            return;

        Dissolve(health, other);
    }

    private void Dissolve(Health health, Collider2D other)
    {
        Debug.Log(other.name + " fell into the acid.");

        // Sink slowly rather than dropping like a stone.
        Rigidbody2D body = other.attachedRigidbody;
        if (body != null)
        {
            body.linearDamping = sinkDrag;
            body.linearVelocity *= 0.3f;
        }

        // Kill it through the normal health system, so the death animation, the turn
        // system and the game-over check all react exactly as they do to any other death.
        health.TakeDamage(health.CurrentHealth);
    }

    // ---------------- Look ----------------

    private void BuildVisuals(float left, float right, float bottom, int sortingLayerId)
    {
        float ppu = 1f / Mathf.Max(0.01f, pixelSize);

        Color highlight = Color.Lerp(acidColor, Color.white, 0.55f);
        Color light = Color.Lerp(acidColor, Color.white, 0.2f);
        Color body = Color.Lerp(acidColor, Color.black, 0.12f);
        Color deep = Color.Lerp(acidColor, Color.black, 0.4f);

        surfaceTexture = BuildSurfaceTexture(highlight, light, body, deep);
        bodyTexture = BuildBodyTexture(highlight, body, deep);
        fadeTexture = BuildFadeTexture();

        surfaceSprite = MakeSprite(surfaceTexture, ppu);
        bodySprite = MakeSprite(bodyTexture, ppu);

        // The fade is stretched, not tiled, so one pixel = one unit and it is scaled to fit.
        fadeSprite = Sprite.Create(fadeTexture, new Rect(0, 0, fadeTexture.width, fadeTexture.height),
            new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

        tileWorldWidth = TileWidth * pixelSize;
        float stripWorldHeight = SurfaceHeight * pixelSize;
        float regionWidth = right - left;

        // The strip's "mid row" sits on the surface line.
        float stripBottom = surfaceY - SurfaceMidRow * pixelSize;
        stripBaseY = stripBottom + stripWorldHeight * 0.5f;

        // The body fills everything below the strip.
        float bodyTop = stripBottom + 2f * pixelSize;
        float bodyHeight = bodyTop - bottom;
        Vector3 bodyCenter = new Vector3(centerX, (bodyTop + bottom) * 0.5f, 0f);

        Color tint = new Color(1f, 1f, 1f, opacity);

        CreateTiled("AcidBody", bodySprite, bodyCenter, new Vector2(regionWidth, bodyHeight), tint, sortingLayerId, sortingOrder);

        // Dark fade so the acid gets deeper-looking toward the bottom.
        GameObject fade = new GameObject("AcidDepthShade");
        fade.transform.SetParent(transform, true);
        fade.transform.position = bodyCenter;
        fade.transform.localScale = new Vector3(regionWidth, bodyHeight / fadeTexture.height, 1f);
        SpriteRenderer fadeRenderer = fade.AddComponent<SpriteRenderer>();
        fadeRenderer.sprite = fadeSprite;
        fadeRenderer.sortingLayerID = sortingLayerId;
        fadeRenderer.sortingOrder = sortingOrder + 1;

        // Two wave strips, scrolling opposite ways, so the surface looks alive.
        Vector2 stripSize = new Vector2(regionWidth + tileWorldWidth * 2f, stripWorldHeight);
        stripA = CreateTiled("AcidWavesA", surfaceSprite, new Vector3(centerX, stripBaseY, 0f), stripSize, tint, sortingLayerId, sortingOrder + 2).transform;
        stripB = CreateTiled("AcidWavesB", surfaceSprite, new Vector3(centerX, stripBaseY, 0f), stripSize,
            new Color(0.85f, 1f, 0.85f, opacity * 0.55f), sortingLayerId, sortingOrder + 3).transform;
    }

    private GameObject CreateTiled(string objectName, Sprite sprite, Vector3 worldPosition, Vector2 size, Color color, int sortingLayerId, int order)
    {
        GameObject tiledObject = new GameObject(objectName);
        tiledObject.transform.SetParent(transform, true);
        tiledObject.transform.position = worldPosition;

        SpriteRenderer spriteRenderer = tiledObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.tileMode = SpriteTileMode.Continuous;
        spriteRenderer.size = size;
        spriteRenderer.color = color;
        spriteRenderer.sortingLayerID = sortingLayerId;
        spriteRenderer.sortingOrder = order;
        return tiledObject;
    }

    private static Sprite MakeSprite(Texture2D texture, float ppu)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }

    // Slides the waves sideways in whole art pixels, so they move like pixel art, not smoothly.
    private void Update()
    {
        if (stripA == null)
            return;

        float time = Time.time;
        float slide = Mathf.Repeat(time * waveSpeed, tileWorldWidth);

        stripA.position = new Vector3(centerX + Snap(slide), stripBaseY, 0f);

        // The second strip goes the other way, a bit slower, and bobs up and down by a pixel.
        float slideBack = Mathf.Repeat(time * waveSpeed * 0.6f, tileWorldWidth);
        float bob = Mathf.Round(Mathf.Sin(time * 1.3f)) * pixelSize;
        stripB.position = new Vector3(centerX - Snap(slideBack), stripBaseY + bob, 0f);
    }

    private float Snap(float value)
    {
        return Mathf.Floor(value / pixelSize) * pixelSize;
    }

    // ---------------- Art, drawn in code ----------------

    // A tile of the surface: a wavy top edge with a bright line along it and acid below.
    // It repeats every TileWidth pixels sideways without a visible seam.
    private Texture2D BuildSurfaceTexture(Color highlight, Color light, Color body, Color deep)
    {
        Color32[] pixels = new Color32[TileWidth * SurfaceHeight]; // all clear to start with

        for (int x = 0; x < TileWidth; x++)
        {
            float angle = x / (float)TileWidth * Mathf.PI * 2f;
            float wave = 3f * Mathf.Sin(angle) + 1.5f * Mathf.Sin(angle * 2f + 0.8f);
            int top = SurfaceMidRow + Mathf.RoundToInt(wave);

            for (int y = 0; y <= top; y++)
            {
                Color color;
                if (y == top) color = highlight;
                else if (y == top - 1) color = light;
                else color = Speckle(x, y, body, deep, light);

                pixels[y * TileWidth + x] = color;
            }
        }

        return MakeTexture(TileWidth, SurfaceHeight, pixels);
    }

    // A tile of plain acid with speckles and a few bubbles. It repeats both ways.
    private Texture2D BuildBodyTexture(Color highlight, Color body, Color deep)
    {
        Color32[] pixels = new Color32[TileWidth * BodyHeight];

        for (int y = 0; y < BodyHeight; y++)
        {
            for (int x = 0; x < TileWidth; x++)
                pixels[y * TileWidth + x] = Speckle(x, y, body, deep, highlight);
        }

        // A few 2x2 bubbles.
        int[,] bubbles = { { 9, 6 }, { 30, 20 }, { 47, 9 }, { 58, 26 }, { 19, 27 } };
        for (int i = 0; i < bubbles.GetLength(0); i++)
        {
            for (int dy = 0; dy < 2; dy++)
            {
                for (int dx = 0; dx < 2; dx++)
                    pixels[(bubbles[i, 1] + dy) * TileWidth + bubbles[i, 0] + dx] = highlight;
            }
        }

        return MakeTexture(TileWidth, BodyHeight, pixels);
    }

    // A thin column that goes from clear at the top to dark at the bottom.
    private static Texture2D BuildFadeTexture()
    {
        const int height = 16;
        Color32[] pixels = new Color32[height];

        for (int y = 0; y < height; y++)
        {
            // y = 0 is the bottom: darkest there.
            float darkness = (1f - y / (float)(height - 1)) * 0.7f;
            pixels[y] = new Color(0f, 0.08f, 0f, darkness);
        }

        return MakeTexture(1, height, pixels);
    }

    // Fixed pseudo-random speckles: a few darker and a few lighter pixels.
    private static Color Speckle(int x, int y, Color body, Color deep, Color light)
    {
        int hash = ((x * 73856093) ^ (y * 19349663)) & 255;

        if (hash < 26) return deep;
        if (hash > 244) return light;
        return body;
    }

    private static Texture2D MakeTexture(int width, int height, Color32[] pixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // keep the pixels sharp
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (surfaceSprite != null) Destroy(surfaceSprite);
        if (bodySprite != null) Destroy(bodySprite);
        if (fadeSprite != null) Destroy(fadeSprite);
        if (surfaceTexture != null) Destroy(surfaceTexture);
        if (bodyTexture != null) Destroy(bodyTexture);
        if (fadeTexture != null) Destroy(fadeTexture);
    }

    // Shows the acid area in the Scene view while this object is selected, so it can be tuned.
    private void OnDrawGizmosSelected()
    {
        TerrainGenerator generator = FindFirstObjectByType<TerrainGenerator>();
        if (generator == null)
            return;

        ComputeRegion(generator, out float left, out float right, out float bottom);

        Gizmos.color = new Color(0.4f, 1f, 0.2f, 0.9f);
        Vector3 center = new Vector3((left + right) * 0.5f, (surfaceY + bottom) * 0.5f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(right - left, surfaceY - bottom, 0f));
    }
}
