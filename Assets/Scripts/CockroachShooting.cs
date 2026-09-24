using UnityEngine;

/// <summary>
/// Step 4: Projectile (firing side).
/// Hold the left mouse button to charge power, release to fire.
/// Reads aim direction and origin from CockroachAim.
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

    private CockroachAim aim;
    private CockroachMovement movement;

    private bool isCharging;
    private float chargeStartTime;

    // --- Read-only state, useful later for a UI power bar ---

    /// <summary>0 when not charging, 1 when fully charged. For a power bar in step 15.</summary>
    public float ChargeRatio01 { get; private set; }

    private void Awake()
    {
        aim = GetComponent<CockroachAim>();
        movement = GetComponent<CockroachMovement>();
    }

    private void Update()
    {
        if (!movement.isMyTurn)
        {
            isCharging = false;
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
        shot.GetComponent<Projectile>().Launch(aim.AimDirection, power);
    }
}
