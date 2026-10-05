using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 5: Explosion.
/// Spawned by the projectile on impact. Plays a sprite animation and finds
/// everything within blastRadius so later steps (damage, knockback) can act on it.
/// This step only DETECTS what was caught in the blast — it does not apply
/// damage or force. Debug.Log lines show what was found, for testing now.
/// </summary>
public class Explosion : MonoBehaviour
{
    [Header("Blast Detection")]
    [Tooltip("How far the explosion reaches, in world units.")]
    public float blastRadius = 3f;

    [Tooltip("Which layers can be affected (e.g. Player, Terrain). Leave as Everything if unsure for now.")]
    public LayerMask affectedLayers;

    [Header("Damage")]
    [Tooltip("Damage dealt to something at the exact center of the blast. Falls off linearly to 0 at the edge of blastRadius.")]
    public int maxDamage = 50;

    [Header("Knockback")]
    [Tooltip("Push strength at the exact center of the blast. Falls off to 0 at the edge, same as damage.")]
    public float maxKnockback = 40f;

    [Header("Visual")]
    [Tooltip("How long the explosion sprite animation plays before this object destroys itself. Match this to your animation clip's length.")]
    public float effectDuration = 0.6f;

    [Header("Terrain")]
    [Tooltip("Step 12: radius of the crater carved into the ground. Leave at 0 to just reuse Blast Radius.")]
    public float craterRadius = 0f;

    [Header("Camera Shake")]
    [Tooltip("How strongly this explosion shakes the screen, as a multiplier on Max Knockback — so a bigger weapon automatically shakes harder with no extra tuning. 0 = no shake from this explosion.")]
    public float shakeMultiplier = 0.03f;

    [Tooltip("How long the screen shake lasts, in seconds.")]
    public float shakeDuration = 0.35f;

    [Header("Debris")]
    [Tooltip("Optional. A Particle System PREFAB (not a child of this object) that bursts dirt debris outward — make one standalone prefab and drag the same one into every weapon's Explosion prefab, rather than building a separate one each time. Leave empty to skip.")]
    public ParticleSystem debrisParticles;

    [Tooltip("Debris particle count per point of Max Knockback, so a bigger weapon throws more debris automatically.")]
    public float debrisCountPerKnockback = 0.5f;

    /// <summary>
    /// Everything the blast found, filled in once at spawn time.
    /// Damage (step 6) and knockback (step 7) will read this list.
    /// </summary>
    public List<Collider2D> HitColliders { get; private set; } = new List<Collider2D>();

    private void Awake()
    {
        DetectHits();
        CarveTerrain();
        ShakeCamera();
        PlayDebris();
        Destroy(gameObject, effectDuration);
    }

    /// <summary>
    /// Shakes the screen, scaled by this explosion's own Max Knockback so a
    /// bigger weapon automatically shakes harder with no per-weapon tuning.
    /// Does nothing if there is no CameraController in the scene.
    /// </summary>
    private void ShakeCamera()
    {
        if (CameraController.Instance == null) return;

        float magnitude = maxKnockback * shakeMultiplier;
        CameraController.Instance.Shake(magnitude, shakeDuration);
    }

    /// <summary>
    /// Instantiates the optional debris Particle System prefab at the blast
    /// position and bursts it, scaled the same way as the shake. Instantiated
    /// fresh rather than carried as a child, so the SAME debris prefab can be
    /// shared across every weapon's Explosion prefab instead of needing a
    /// separate copy built inside each one. The instance outlives this
    /// Explosion object on its own (effectDuration is usually much shorter
    /// than how long debris takes to fall and settle) — in the debris
    /// prefab's own Inspector, set Stop Action to Destroy so it cleans
    /// itself up once every particle has finished.
    /// </summary>
    private void PlayDebris()
    {
        if (debrisParticles == null) return;

        ParticleSystem instance = Instantiate(debrisParticles, transform.position, Quaternion.identity);

        ParticleSystem.EmissionModule emission = instance.emission;
        int count = Mathf.Max(1, Mathf.RoundToInt(maxKnockback * debrisCountPerKnockback));
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        instance.Play();
    }

    /// <summary>
    /// Step 12: erases a circle of ground where the explosion happened.
    /// Does nothing if there is no TerrainGenerator in the scene, so this is
    /// safe to leave on even while testing without terrain.
    /// </summary>
    private void CarveTerrain()
    {
        if (TerrainGenerator.Instance == null) return;

        float radius = craterRadius > 0f ? craterRadius : blastRadius;
        TerrainGenerator.Instance.CarveCircle(transform.position, radius);
    }

    private void DetectHits()
    {
        Collider2D[] found = Physics2D.OverlapCircleAll(transform.position, blastRadius, affectedLayers);
        HitColliders.AddRange(found);

        foreach (Collider2D hit in HitColliders)
        {
            ApplyDamage(hit);
            ApplyKnockback(hit);
        }
    }

    /// <summary>
    /// Pushes cockroaches away from the blast center. Strength falls off with
    /// distance, the same way damage does.
    /// </summary>
    private void ApplyKnockback(Collider2D hit)
    {
        CockroachMovement mover = hit.GetComponent<CockroachMovement>();
        if (mover == null) return; // Only cockroaches get pushed for now.

        // Use the collider's center, not its pivot. The pivot is at the feet,
        // which is often right at the blast point and gives no usable direction.
        Vector2 bodyCenter = hit.bounds.center;
        Vector2 away = bodyCenter - (Vector2)transform.position;

        // If the blast is exactly on the body center, push straight up.
        Vector2 direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

        float falloff = 1f - Mathf.Clamp01(away.magnitude / blastRadius);

        mover.ApplyKnockback(direction * maxKnockback * falloff);
    }

    /// <summary>
    /// Works out falloff damage based on distance from the blast center,
    /// then applies it if the object caught in the blast has a Health component.
    /// Self-damage is allowed: whoever fired the shot is treated the same as anyone else.
    /// </summary>
    private void ApplyDamage(Collider2D hit)
    {
        Health health = hit.GetComponent<Health>();
        if (health == null) return; // Not something that can take damage (e.g. the ground).

        float distance = Vector2.Distance(transform.position, hit.transform.position);

        // 1 at the very center, 0 at the edge of blastRadius, clamped so it never goes negative.
        float falloff = 1f - Mathf.Clamp01(distance / blastRadius);

        int damage = Mathf.RoundToInt(maxDamage * falloff);
        health.TakeDamage(damage);
    }

    /// <summary>
    /// Draws the blast radius in the Scene view so you can see and tune it.
    /// Only visible while the explosion object exists, which is brief, so it's
    /// more useful for checking blastRadius on the prefab asset itself.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
