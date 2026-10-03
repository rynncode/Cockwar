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

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Destroy(gameObject, maxLifetime);
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
        // Ignore anything after the first hit, in case of multiple contact points in one frame.
        if (hasHit) return;
        hasHit = true;

        if (explosionPrefab != null)
        {
            Vector2 contactPoint = collision.GetContact(0).point;
            Instantiate(explosionPrefab, contactPoint, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
