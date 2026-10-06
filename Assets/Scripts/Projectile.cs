using UnityEngine;

/// <summary>How a projectile turns while it flies.</summary>
public enum ProjectileRotation
{
    /// <summary>Does not turn on purpose (physics may still turn it if it bounces).</summary>
    None,

    /// <summary>Tumbles. Smaller projectiles spin faster, larger ones slower.</summary>
    Spin,

    /// <summary>Rocket: always points along its direction of travel, so the nose follows the arc.</summary>
    FaceVelocity
}

/// <summary>
/// Step 4: Projectile.
/// Flies using normal Rigidbody2D physics (gravity pulls it into an arc).
/// When it hits something, it stops and destroys itself.
/// Explosion (step 5) will hook into OnHit later instead of just destroying.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Tooltip("Destroys the projectile automatically if it never hits anything, so stray shots don't fly forever.")]
    public float maxLifetime = 20f;

    [Tooltip("Explosion prefab to spawn at the impact point. Leave empty to just disappear silently, like before step 5.")]
    public GameObject explosionPrefab;

    [Header("Fuse (grenades)")]
    [Tooltip("On = explodes when it touches something (normal shell). Off = it bounces and rolls, and explodes when the fuse runs out.")]
    public bool explodeOnImpact = true;

    [Tooltip("Seconds until a fused projectile explodes. Only used when Explode On Impact is off.")]
    public float fuseSeconds = 3f;

    [Tooltip("Off = the fuse starts when thrown. On = the fuse starts the first time it touches something.")]
    public bool fuseStartsOnFirstHit = false;

    [Header("Fuse visuals (grenades)")]
    [Tooltip("Show a countdown above the projectile and pulse its color while the fuse burns.")]
    public bool showFuseVisuals = true;

    [Tooltip("Color the projectile pulses toward as the fuse runs down.")]
    public Color fusePulseColor = new Color(1f, 0.15f, 0.1f, 1f);

    [Tooltip("Size of the countdown badge, as a fraction of the camera's half-height, so it stays readable at any zoom.")]
    public float fuseBadgeSize = 0.06f;

    [Header("Sound (thrown)")]
    [Tooltip("Played the moment this projectile is thrown, e.g. the Holy Hand Grenade's hallelujah. Leave empty for none.")]
    public AudioClip thrownSound;

    [Range(0f, 1f)] public float thrownVolume = 1f;

    [Tooltip("On = the sound is cut off when the projectile explodes. Off = it plays to the end even after the explosion.")]
    public bool cutThrownSoundOnExplosion = true;

    [Header("Sound (grenades)")]
    [Tooltip("Played when a fused projectile bounces off something. One is picked at random. Leave empty for silent bounces.")]
    public AudioClip[] bounceSounds;

    [Range(0f, 1f)] public float bounceVolume = 0.6f;

    [Tooltip("Hits slower than this (units per second) make no sound, so a rolling grenade doesn't rattle constantly.")]
    public float minBounceSoundSpeed = 3f;

    [Header("Rotation")]
    [Tooltip("None = no turning. Spin = tumbles (smaller = faster). FaceVelocity = rocket, points where it is going.")]
    public ProjectileRotation rotation = ProjectileRotation.None;

    [Tooltip("Spin only. Degrees per second for a projectile of the Reference Size.")]
    public float spinSpeedAtReferenceSize = 360f;

    [Tooltip("Spin only. Physical size (collider width in world units) that spins at the speed above. Half the size spins twice as fast. The default projectile is 6.")]
    public float referenceSize = 6f;

    [Tooltip("Spin only. Slowest and fastest spin allowed, in degrees per second.")]
    public float minSpinSpeed = 45f;
    public float maxSpinSpeed = 1440f;

    [Tooltip("FaceVelocity only. Extra angle if the art does not point right: 0 = art points right, -90 = art points up, 90 = points down.")]
    public float faceVelocityAngleOffset = 0f;

    /// <summary>
    /// Fires exactly once, right before this projectile is destroyed —
    /// whether that's from hitting something or from maxLifetime running out.
    /// Carries the landing position, since the projectile itself (and its
    /// Transform) is gone by the time anything can react to this.
    /// TurnManager listens to this to know when a shot has actually landed.
    /// </summary>
    public event System.Action<Vector2> OnLanded;

    /// <summary>
    /// Impact projectiles only: fires once when it hits something, with what it hit and where,
    /// just before it explodes. The Reneitor uses this to see whether its basketball hit a player.
    /// </summary>
    public event System.Action<Collider2D, Vector2> OnImpact;

    private Rigidbody2D body;
    private bool hasHit;
    private bool hasNotifiedLanded;
    private bool fuseRunning;
    private float fuseTimer;
    private float fuseTotal;

    /// <summary>True while a fuse is burning. FuseVisual reads these three.</summary>
    public bool FuseRunning => fuseRunning;
    public float FuseTimeLeft => fuseTimer;
    public float FuseTotal => fuseTotal;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // A fused projectile must live long enough for its fuse to finish.
        float life = explodeOnImpact ? maxLifetime : Mathf.Max(maxLifetime, fuseSeconds + 1f);
        Destroy(gameObject, life);

        if (!explodeOnImpact && !fuseStartsOnFirstHit) StartFuse();

        PlayThrownSound();
    }

    private void PlayThrownSound()
    {
        if (thrownSound == null) return;

        if (!cutThrownSoundOnExplosion)
        {
            // Independent of this object, so it keeps playing after the projectile is gone.
            Sfx.Play(thrownSound, thrownVolume);
            return;
        }

        // Lives on the projectile, so it follows the throw and ends when the projectile is destroyed.
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = thrownSound;
        source.spatialBlend = 0f;
        source.volume = thrownVolume * GameSettings.SfxVolume;
        source.outputAudioMixerGroup = Sfx.Output;
        source.Play();
    }

    private void StartFuse()
    {
        fuseRunning = true;
        fuseTimer = fuseSeconds;
        fuseTotal = fuseSeconds;

        // The countdown badge and the color pulse are drawn by a helper component.
        if (showFuseVisuals && GetComponent<FuseVisual>() == null) gameObject.AddComponent<FuseVisual>();
    }

    private void Update()
    {
        if (!fuseRunning || hasHit) return;

        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0f) Explode(transform.position);
    }

    /// <summary>
    /// OnDestroy is the one place that always runs no matter which path
    /// destroyed this object (a collision, or the maxLifetime timeout), so
    /// this is the single, reliable spot to raise OnLanded exactly once.
    /// </summary>
    private void OnDestroy()
    {
        if (hasNotifiedLanded) return;
        hasNotifiedLanded = true;
        OnLanded?.Invoke(transform.position);
    }

    /// <summary>
    /// Call this right after Instantiate to launch the projectile.
    /// </summary>
    public void Launch(Vector2 direction, float speed)
    {
        body.linearVelocity = direction * speed;
        ApplyRotationMode(direction);
    }

    private void ApplyRotationMode(Vector2 direction)
    {
        if (rotation == ProjectileRotation.Spin)
        {
            // Smaller projectile = faster spin: speed scales with referenceSize / size.
            float size = Mathf.Max(0.01f, PhysicalSize());
            float spin = Mathf.Clamp(spinSpeedAtReferenceSize * referenceSize / size, minSpinSpeed, maxSpinSpeed);

            // Positive angular velocity is counter-clockwise, so spin clockwise when flying right.
            float turn = direction.x >= 0f ? -1f : 1f;

            body.freezeRotation = false;
            body.angularVelocity = turn * spin;
        }
        else if (rotation == ProjectileRotation.FaceVelocity)
        {
            // Physics must not twist the rocket; FixedUpdate sets its angle every step.
            body.freezeRotation = true;
            body.angularVelocity = 0f;
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + faceVelocityAngleOffset;
        }
    }

    private void FixedUpdate()
    {
        if (rotation != ProjectileRotation.FaceVelocity || hasHit) return;

        Vector2 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude < 0.01f) return;   // too slow to have a meaningful direction

        body.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + faceVelocityAngleOffset;
    }

    /// <summary>
    /// How big the projectile really is, in world units (collider width times scale).
    /// Computed from the collider's shape because Collider2D.bounds can be empty
    /// on the very frame a projectile is created.
    /// </summary>
    private float PhysicalSize()
    {
        Vector3 s = transform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
        Collider2D col = GetComponent<Collider2D>();

        if (col is CircleCollider2D circle) return circle.radius * 2f * scale;
        if (col is CapsuleCollider2D capsule) return Mathf.Max(capsule.size.x, capsule.size.y) * scale;
        if (col is BoxCollider2D box) return Mathf.Max(box.size.x, box.size.y) * scale;
        return scale;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Fused projectile: just bounce/roll. Touching something may start the fuse.
        if (!explodeOnImpact)
        {
            if (fuseStartsOnFirstHit && !fuseRunning) StartFuse();

            if (collision.relativeVelocity.magnitude >= minBounceSoundSpeed)
                Sfx.PlayRandom(bounceSounds, bounceVolume, 0.1f);
            return;
        }

        if (!hasHit) OnImpact?.Invoke(collision.collider, collision.GetContact(0).point);
        Explode(collision.GetContact(0).point);
    }

    /// <summary>Spawns the explosion at the given point and removes this projectile (runs once).</summary>
    private void Explode(Vector2 point)
    {
        // Ignore anything after the first hit, in case of multiple contact points in one frame.
        if (hasHit) return;
        hasHit = true;

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, point, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
