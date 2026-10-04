using UnityEngine;

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
    public float maxLifetime = 10f;

    [Tooltip("Explosion prefab to spawn at the impact point. Leave empty to just disappear silently, like before step 5.")]
    public GameObject explosionPrefab;

    [Header("Fuse (grenades)")]
    [Tooltip("On = explodes when it touches something (normal shell). Off = it bounces and rolls, and explodes when the fuse runs out.")]
    public bool explodeOnImpact = true;

    [Tooltip("Seconds until a fused projectile explodes. Only used when Explode On Impact is off.")]
    public float fuseSeconds = 3f;

    [Tooltip("Off = the fuse starts when thrown. On = the fuse starts the first time it touches something.")]
    public bool fuseStartsOnFirstHit = false;

    /// <summary>
    /// Fires exactly once, right before this projectile is destroyed —
    /// whether that's from hitting something or from maxLifetime running out.
    /// Carries the landing position, since the projectile itself (and its
    /// Transform) is gone by the time anything can react to this.
    /// TurnManager listens to this to know when a shot has actually landed.
    /// </summary>
    public event System.Action<Vector2> OnLanded;

    private Rigidbody2D body;
    private bool hasHit;
    private bool hasNotifiedLanded;
    private bool fuseRunning;
    private float fuseTimer;

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
    }

    private void StartFuse()
    {
        fuseRunning = true;
        fuseTimer = fuseSeconds;
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
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Fused projectile: just bounce/roll. Touching something may start the fuse.
        if (!explodeOnImpact)
        {
            if (fuseStartsOnFirstHit && !fuseRunning) StartFuse();
            return;
        }

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
