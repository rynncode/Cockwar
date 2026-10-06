using UnityEngine;

/// <summary>
/// TELEPORTER (tool): a cyan ring follows the mouse; left-click to jump straight there.
/// The ring turns red where you would not fit (inside the ground, off the map, in the acid),
/// and clicking a red spot does nothing. Using it does NOT end the turn (Ends Turn On Fire is
/// off on its WeaponData), so you can still walk and shoot afterwards.
///
/// Asset: Assets/Weapons/Special/Attacks/Teleporter Attack.
/// </summary>
[CreateAssetMenu(fileName = "Teleporter Attack", menuName = "Cockwar/Special Attacks/Teleporter")]
public class TeleporterAttack : SpecialAttack
{
    [Tooltip("Furthest you can teleport, in world units. 0 = anywhere on the map.")]
    [SerializeField] private float maxRange = 0f;

    [Tooltip("Size of the flash at both ends of the jump.")]
    [SerializeField] private float effectSize = 14f;

    [Tooltip("Optional art for the target ring (empty = built-in).")]
    [SerializeField] private Sprite markerSprite;

    [SerializeField] private AudioClip teleportSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    public override SpecialActivation Activation => SpecialActivation.ClickTarget;
    public override Sprite TargetMarker => markerSprite != null ? markerSprite : WeaponArt.TeleportMarker();
    public override float TargetMarkerSize => 12f;

    /// <summary>The spot is where the cockroach's body centre would land. It must be open space on the map.</summary>
    public override bool IsValidTarget(Vector2 target, CockroachShooting shooter)
    {
        if (shooter == null) return false;
        Collider2D body = shooter.GetComponent<Collider2D>();
        Vector2 size = body != null ? (Vector2)body.bounds.size : Vector2.one * 5f;

        if (maxRange > 0f && Vector2.Distance(target, shooter.transform.position) > maxRange) return false;

        // On the map, and above the acid.
        TerrainGenerator terrain = TerrainGenerator.Instance;
        if (terrain != null)
        {
            Rect map = terrain.WorldBounds;
            if (target.x < map.xMin || target.x > map.xMax) return false;

            AcidHazard acid = FindFirstObjectByType<AcidHazard>();
            float floor = acid != null ? map.yMin + acid.surfaceHeightAboveBottom : map.yMin;
            if (target.y - size.y * 0.5f < floor + 2f) return false;
        }

        // Nothing solid where the body would be (crates, other cockroaches and the ground all count).
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(target, size * 0.95f, 0f))
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == shooter.gameObject) continue;
            return false;
        }

        return true;
    }

    public override Projectile Begin(AttackContext context)
    {
        CockroachShooting shooter = context.shooter;
        if (shooter == null) return null;

        Collider2D body = shooter.GetComponent<Collider2D>();
        Vector2 centre = body != null ? (Vector2)body.bounds.center : (Vector2)shooter.transform.position;
        Vector2 pivotOffset = (Vector2)shooter.transform.position - centre;   // the pivot is at the feet

        Flash(centre);

        Vector2 destination = context.target + pivotOffset;
        Rigidbody2D rigidbody = shooter.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
        {
            rigidbody.position = destination;
            rigidbody.linearVelocity = Vector2.zero;
        }
        shooter.transform.position = new Vector3(destination.x, destination.y, shooter.transform.position.z);

        Flash(context.target);
        if (teleportSound != null) Sfx.Play(teleportSound, volume);
        WeaponFx.Follow(shooter.transform);
        return null;
    }

    /// <summary>A burst of blue sparks (WeaponEffectArt > Beam Spark), or cyan puffs without that art.</summary>
    private void Flash(Vector2 point)
    {
        WeaponEffectArt art = WeaponEffectArt.Get();
        if (art != null && FxAnim.Play(art.beamSpark, point, effectSize, 14f, false, Color.white, 55) != null) return;

        for (int i = 0; i < 6; i++)
            FxPuff.Spawn(WeaponArt.Puff(), point + Random.insideUnitCircle * effectSize * 0.3f, effectSize * 0.3f, 0.2f,
                         new Color(0.4f, 0.95f, 1f, 1f), 0.4f, Random.insideUnitCircle * 6f, 55);
    }

    protected override Sprite BuildIcon() => WeaponArt.Teleporter();
    protected override Sprite BuildHeldSprite() => WeaponArt.Teleporter();
}
