using UnityEngine;

/// <summary>
/// A see-through band showing a straight path at its full length, ignoring walls, with a ring at
/// the far end. The Blowtorch uses it to show exactly where its tunnel will go.
/// ShowPath must be called every frame it should be visible; on any frame nobody calls it, it
/// hides itself, so it can never be left behind when the turn ends or the weapon changes.
/// </summary>
public class PathPreview : MonoBehaviour
{
    private static PathPreview instance;

    private SpriteRenderer band;
    private SpriteRenderer edge;
    private SpriteRenderer endRing;
    private int shownFrame = -1;

    /// <param name="width">Width of the band in world units (the tunnel's diameter).</param>
    /// <param name="endRing">Draw a ring at the far end. Off when the weapon's crosshair already sits there.</param>
    public static void ShowPath(Vector2 from, Vector2 direction, float length, float width, Color color, bool endRing = true)
    {
        if (instance == null) Create();
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 middle = from + direction * length * 0.5f;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);

        // The tunnel area, faint.
        Place(instance.band, middle, angle, length, width);
        instance.band.color = new Color(color.r, color.g, color.b, 0.22f * pulse);

        // A brighter centre line, so the direction reads clearly over busy ground.
        Place(instance.edge, middle, angle, length, Mathf.Max(0.4f, width * 0.08f));
        instance.edge.color = new Color(color.r, color.g, color.b, 0.8f * pulse);

        // Where it stops.
        instance.endRing.transform.position = from + direction * length;
        WeaponFx.SetWidth(instance.endRing, width * 1.1f);
        instance.endRing.color = new Color(color.r, color.g, color.b, 0.9f);

        instance.band.enabled = instance.edge.enabled = true;
        instance.endRing.enabled = endRing;
        instance.shownFrame = Time.frameCount;
    }

    private static void Create()
    {
        GameObject go = new GameObject("Path Preview");
        instance = go.AddComponent<PathPreview>();
        instance.band = WeaponFx.MakeSprite("Band", WeaponArt.Pixel(), Vector2.zero, 1f, 58, go.transform);
        instance.edge = WeaponFx.MakeSprite("Centre", WeaponArt.Pixel(), Vector2.zero, 1f, 59, go.transform);
        instance.endRing = WeaponFx.MakeSprite("End", WeaponArt.TeleportMarker(), Vector2.zero, 1f, 59, go.transform);
    }

    /// <summary>Stretches a 1x1 pixel sprite into a rotated rectangle.</summary>
    private static void Place(SpriteRenderer part, Vector2 centre, float angle, float length, float width)
    {
        part.transform.position = centre;
        part.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        part.transform.localScale = new Vector3(length, width, 1f);
    }

    private void LateUpdate()
    {
        if (shownFrame >= Time.frameCount - 1) return;
        band.enabled = edge.enabled = endRing.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
