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

    private Rigidbody2D body;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Destroy(gameObject, maxLifetime);
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
