using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small shared helpers for the special weapons, so each weapon script stays short:
/// finding the players, the mouse position, the camera, and making simple sprites in the world.
/// </summary>
public static class WeaponFx
{
    private static TurnManager turnManager;

    /// <summary>Every cockroach in the match (alive or not), from the TurnManager. Works for 2-4 players.</summary>
    public static List<CockroachMovement> AllPlayers()
    {
        if (turnManager == null) turnManager = Object.FindFirstObjectByType<TurnManager>();
        return turnManager != null ? turnManager.players : new List<CockroachMovement>();
    }

    /// <summary>Living cockroaches only.</summary>
    public static IEnumerable<CockroachMovement> LivingPlayers()
    {
        foreach (CockroachMovement player in AllPlayers())
            if (IsAlive(player)) yield return player;
    }

    public static bool IsAlive(CockroachMovement player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    /// <summary>Middle of a cockroach's body (its pivot is at the feet).</summary>
    public static Vector2 BodyCenter(CockroachMovement player)
    {
        Collider2D body = player.GetComponent<Collider2D>();
        return body != null ? (Vector2)body.bounds.center : (Vector2)player.transform.position;
    }

    /// <summary>Deals damage through the normal Health component (health bar, "-10" label, death all still work).</summary>
    public static void Damage(CockroachMovement player, int amount)
    {
        if (!IsAlive(player)) return;
        Health health = player.GetComponent<Health>();
        if (health != null) health.TakeDamage(amount);
    }

    /// <summary>Mouse position in the world.</summary>
    public static Vector2 MouseWorld()
    {
        Camera cam = Camera.main;
        return cam != null ? (Vector2)cam.ScreenToWorldPoint(Input.mousePosition) : Vector2.zero;
    }

    public static void Shake(float magnitude, float seconds)
    {
        if (CameraController.Instance != null) CameraController.Instance.Shake(magnitude, seconds);
    }

    public static void Follow(Transform target)
    {
        if (CameraController.Instance != null && target != null) CameraController.Instance.SetTarget(target);
    }

    public static void Look(Vector2 point)
    {
        if (CameraController.Instance != null) CameraController.Instance.SetTargetPosition(point);
    }

    /// <summary>Creates a sprite in the world, scaled to the given world width.</summary>
    public static SpriteRenderer MakeSprite(string name, Sprite sprite, Vector2 position, float width, int sortingOrder, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = sortingOrder;
        SetWidth(spriteRenderer, width);
        return spriteRenderer;
    }

    /// <summary>Scales a sprite renderer so its sprite is this many world units wide.</summary>
    public static void SetWidth(SpriteRenderer spriteRenderer, float width)
    {
        float spriteWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        float scale = width / Mathf.Max(0.0001f, spriteWidth);
        Vector3 parentScale = spriteRenderer.transform.parent != null ? spriteRenderer.transform.parent.lossyScale : Vector3.one;
        spriteRenderer.transform.localScale = new Vector3(scale / Mathf.Max(0.0001f, parentScale.x), scale / Mathf.Max(0.0001f, parentScale.y), 1f);
    }

    /// <summary>Top of the ground at a world x (first ground from the sky), or the fallback height if there is none.</summary>
    public static float SurfaceY(float x, float fallback)
    {
        TerrainGenerator terrain = TerrainGenerator.Instance;
        if (terrain != null && terrain.TryFindSurface(x, 0f, 9999f, out Vector2 surface)) return surface.y;
        return fallback;
    }

    /// <summary>First solid (non-trigger) collider between two points, if any.</summary>
    public static bool SolidHit(Vector2 from, Vector2 to, out RaycastHit2D solidHit)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to))
        {
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                solidHit = hit;
                return true;
            }
        }

        solidHit = default;
        return false;
    }

    /// <summary>Spawns a burst of flames, for fire trails, burning players and impacts.</summary>
    public static void FireBurst(Vector2 position, float size, int count)
    {
        WeaponEffectArt art = WeaponEffectArt.Get();
        if (art != null && WeaponEffectArt.Has(art.fire))
        {
            // The Fire frames are big, detailed flames, so fewer of them are needed than blobs.
            int flames = Mathf.Max(1, Mathf.CeilToInt(count * 0.5f));
            for (int i = 0; i < flames; i++)
            {
                Vector2 offset = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.1f)) * size;
                FxAnim flame = FxAnim.Play(art.fire, position + offset, size * Random.Range(0.8f, 1.2f), Random.Range(12f, 16f),
                                           false, Color.white, 21, true);
                flame.WithFade(0.15f);
                if (Random.value < 0.5f) flame.Renderer.flipX = true;
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * size * 0.4f;
            Color hot = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f), Random.value);
            FxPuff.Spawn(WeaponArt.Puff(), position + offset, size * 0.5f, size * 0.15f, hot, Random.Range(0.25f, 0.45f),
                         Random.insideUnitCircle * size, 21);
        }
    }

    /// <summary>Moves a sprite so the bottom edge of its frame sits on this point (for flames and ground blasts).</summary>
    public static void PlaceBottom(SpriteRenderer spriteRenderer, Vector2 point)
    {
        Bounds local = spriteRenderer.sprite.bounds;
        float scaleY = Mathf.Abs(spriteRenderer.transform.lossyScale.y);
        float y = point.y + (local.extents.y - local.center.y) * scaleY;
        spriteRenderer.transform.position = new Vector3(point.x, y, 0f);
    }

    /// <summary>
    /// Hides an explosion prefab's own sprite animation (its sound, damage, crater, shake and debris
    /// all still happen), for when a special weapon draws its own blast with WeaponEffectArt frames.
    /// </summary>
    public static void HideExplosionArt(Explosion explosion)
    {
        foreach (Renderer part in explosion.GetComponentsInChildren<Renderer>()) part.enabled = false;
    }

    /// <summary>
    /// Spawns an explosion with this weapon's numbers. If frames are given, they are played as the
    /// blast (size = world width) and the prefab's own art is hidden; otherwise the prefab looks as usual.
    /// lift moves the art up by that fraction of its width (the frames' blasts don't all start at the bottom edge).
    /// </summary>
    public static void Blast(GameObject prefab, Vector2 point, System.Action<Explosion> configure, Sprite[] frames, float size, float fps, float lift = 0f)
    {
        bool customArt = WeaponEffectArt.Has(frames);
        Explosion.Spawn(prefab, point, explosion =>
        {
            configure?.Invoke(explosion);
            if (customArt) HideExplosionArt(explosion);
        });

        if (customArt) FxAnim.Play(frames, point + Vector2.up * size * lift, size, fps, false, Color.white, 52);
    }

    /// <summary>Carves a crater, if there is terrain.</summary>
    public static void Carve(Vector2 point, float radius)
    {
        if (TerrainGenerator.Instance != null && radius > 0f) TerrainGenerator.Instance.CarveCircle(point, radius);
    }
}
