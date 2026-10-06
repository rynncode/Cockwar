using UnityEngine;

/// <summary>
/// The supply plane. Made in code by CrateDropManager for one delivery: it flies in from off the
/// side, drops a crate over the chosen spot, keeps going and removes itself once it is out of
/// sight. It has no collider, so shots and cockroaches pass through it.
/// </summary>
public class CrateAircraft : MonoBehaviour
{
    private const int SortingOrder = 40;   // over the land and crates, under the acid and HUD

    /// <summary>True once the crate has been released.</summary>
    public bool HasDropped { get; private set; }

    private System.Action<Vector2> onDrop;
    private float direction;   // +1 = flying right, -1 = flying left
    private float speed;
    private float dropX;
    private float endX;
    private float baseY;
    private float age;
    private float dropBelow;   // how far under the plane the crate appears
    private AudioSource engine;

    /// <summary>
    /// Starts a delivery flight at a fixed height, from startX past dropX to endX.
    /// onDrop runs once, with the release point, when the plane is over dropX.
    /// </summary>
    public static CrateAircraft Launch(CrateSettings settings, float startX, float dropX, float endX, float height, System.Action<Vector2> onDrop)
    {
        GameObject planeObject = new GameObject("Supply Plane");
        planeObject.transform.position = new Vector3(startX, height, 0f);

        CrateAircraft plane = planeObject.AddComponent<CrateAircraft>();
        plane.direction = endX >= startX ? 1f : -1f;
        plane.speed = Mathf.Max(1f, settings.aircraftSpeed);
        plane.dropX = dropX;
        plane.endX = endX;
        plane.baseY = height;
        plane.onDrop = onDrop;

        SpriteRenderer art = planeObject.AddComponent<SpriteRenderer>();
        art.sprite = settings.aircraftSprite != null ? settings.aircraftSprite : CrateArt.Aircraft();
        art.sortingOrder = SortingOrder;
        art.flipX = plane.direction < 0f;

        // About as wide as five cockroaches, whatever sprite is used.
        float width = Mathf.Max(1f, settings.crateSize * 4.5f);
        float scale = width / Mathf.Max(0.0001f, art.sprite.bounds.size.x);
        planeObject.transform.localScale = new Vector3(scale, scale, 1f);
        plane.dropBelow = art.sprite.bounds.size.y * scale * 0.5f + settings.crateSize * 0.6f;

        if (settings.aircraftSound != null)
        {
            // On the plane itself, so the engine stops when it leaves.
            plane.engine = planeObject.AddComponent<AudioSource>();
            plane.engine.clip = settings.aircraftSound;
            plane.engine.spatialBlend = 0f;
            plane.engine.volume = settings.aircraftVolume * GameSettings.SfxVolume;
            plane.engine.outputAudioMixerGroup = Sfx.Output;
            plane.engine.Play();
        }

        return plane;
    }

    private void Update()
    {
        age += Time.deltaTime;

        Vector3 position = transform.position;
        position.x += direction * speed * Time.deltaTime;
        position.y = baseY + Mathf.Sin(age * 2.2f) * 0.6f;   // a gentle bob
        transform.position = position;

        if (!HasDropped && (position.x - dropX) * direction >= 0f)
        {
            HasDropped = true;
            onDrop?.Invoke(new Vector2(dropX, position.y - dropBelow));
        }

        if ((position.x - endX) * direction >= 0f) Destroy(gameObject);
    }
}
