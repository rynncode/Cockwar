using UnityEngine;

/// <summary>
/// Step 4: Projectile (firing side).
/// Hold the left mouse button to charge power, release to fire.
/// Reads aim direction and origin from CockroachAim.
/// Step 9: raises OnFired after each shot so the TurnManager knows the turn's shot is done.
/// </summary>
[RequireComponent(typeof(CockroachAim))]
[RequireComponent(typeof(CockroachMovement))]
public class CockroachShooting : MonoBehaviour
{
    [Header("Projectile")]
    [Tooltip("Prefab that has the Projectile script on it.")]
    public GameObject projectilePrefab;

    [Header("Power")]
    [Tooltip("Launch speed when the mouse is clicked and released instantly.")]
    public float minPower = 10f;

    [Tooltip("Launch speed when fully charged.")]
    public float maxPower = 40f;

    [Tooltip("How many seconds of holding it takes to go from min to max power.")]
    public float maxChargeTime = 1.5f;

    [Header("Turn System")]
    [Tooltip("Step 9: whether firing this weapon ends the turn. Leave on for the current weapon. Future weapons (step 13) — a utility item, say — can turn this off so firing them does not end the turn.")]
    public bool endsTurnOnFire = true;

    private CockroachAim aim;
    private CockroachMovement movement;
    private Collider2D ownCollider;

    private bool isCharging;
    private float chargeStartTime;

    // --- Read-only state, useful later for a UI power bar ---

    /// <summary>0 when not charging, 1 when fully charged. For a power bar in step 15.</summary>
    public float ChargeRatio01 { get; private set; }

    /// <summary>
    /// Raised right after a projectile is fired. The TurnManager listens to this
    /// to end the turn. An event (instead of the TurnManager checking every frame)
    /// keeps this script unaware of the turn system.
    /// </summary>
    public event System.Action OnFired;

    /// <summary>
    /// Raised right after a projectile is launched, carrying the projectile
    /// itself. TurnManager uses this to have the camera follow it in flight.
    /// </summary>
    public event System.Action<Projectile> OnProjectileLaunched;

    private void Awake()
    {
        aim = GetComponent<CockroachAim>();
        movement = GetComponent<CockroachMovement>();
        ownCollider = GetComponent<Collider2D>();

        if (ownCollider == null)
        {
            Debug.LogWarning("CockroachShooting: no Collider2D found on this cockroach. Self-collision at spawn will not be prevented.");
        }
    }

    private void Update()
    {
        if (!movement.isMyTurn)
        {
            // Also reset the charge, so a turn that ends mid-charge
            // does not leave a half-full power bar behind.
            isCharging = false;
            ChargeRatio01 = 0f;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isCharging = true;
            chargeStartTime = Time.time;
        }

        if (isCharging)
        {
            float heldSeconds = Time.time - chargeStartTime;
            ChargeRatio01 = Mathf.Clamp01(heldSeconds / maxChargeTime);
        }

        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            Fire();
            isCharging = false;
            ChargeRatio01 = 0f;
        }
    }

    private void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("CockroachShooting: no Projectile Prefab assigned.");
            return;
        }

        float power = Mathf.Lerp(minPower, maxPower, ChargeRatio01);

        GameObject shot = Instantiate(projectilePrefab, aim.AimOrigin, Quaternion.identity);

        // Prevent the projectile from colliding with the cockroach that just fired it,
        // regardless of aim direction or how close the spawn point is to the body.
        Collider2D shotCollider = shot.GetComponent<Collider2D>();
        if (shotCollider != null && ownCollider != null)
        {
            Physics2D.IgnoreCollision(shotCollider, ownCollider, true);
        }

        Projectile projectile = shot.GetComponent<Projectile>();
        projectile.Launch(aim.AimDirection, power);

        // Tell anyone listening (the TurnManager) that a shot was fired,
        // and which projectile it was, so the camera can follow it.
        OnFired?.Invoke();
        OnProjectileLaunched?.Invoke(projectile);
    }
}
