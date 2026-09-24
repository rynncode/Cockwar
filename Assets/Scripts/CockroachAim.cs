using UnityEngine;

/// <summary>
/// Step 3: Mouse aiming.
/// A crosshair orbits the cockroach at a fixed distance and points toward the mouse.
/// The projectile script (step 4) will read AimOrigin and AimDirection from here.
/// The crosshair is only visible while it is this cockroach's turn.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
public class CockroachAim : MonoBehaviour
{
    [Header("Crosshair")]
    [Tooltip("The crosshair object. Do NOT make it a child of the cockroach, see the notes.")]
    public Transform crosshair;

    [Tooltip("How far from the aim origin the crosshair sits, in world units.")]
    public float crosshairDistance = 1.5f;

    [Header("Aim Origin")]
    [Tooltip("Offset from the cockroach's pivot (its feet) to where shots start. Raise Y to aim from the body, not the feet.")]
    public Vector2 aimOriginOffset = new Vector2(0f, 0.5f);

    private CockroachMovement movement;
    private Camera mainCamera;

    // Unit-length direction from the aim origin toward the mouse. Starts facing right.
    private Vector2 aimDirection = Vector2.right;

    // --- Read-only state for other scripts (the projectile will use these) ---

    /// <summary>World position that shots start from.</summary>
    public Vector2 AimOrigin => (Vector2)transform.position + aimOriginOffset;

    /// <summary>Direction toward the mouse, length 1.</summary>
    public Vector2 AimDirection => aimDirection;

    /// <summary>Aim angle in degrees. 0 = right, 90 = up, 180 = left.</summary>
    public float AimAngle => Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("CockroachAim: no camera tagged MainCamera found in the scene.");
        }
    }

    private void Update()
    {
        // Hide the crosshair and ignore the mouse when it is not this player's turn.
        if (crosshair != null)
        {
            crosshair.gameObject.SetActive(movement.isMyTurn);
        }

        if (!movement.isMyTurn || mainCamera == null) return;

        UpdateAimDirection();
        UpdateCrosshairPosition();
    }

    /// <summary>
    /// Converts the mouse from screen pixels into a world position,
    /// then works out the direction from the aim origin to that point.
    /// </summary>
    private void UpdateAimDirection()
    {
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = (Vector2)mouseWorld - AimOrigin;

        // If the mouse is exactly on the origin there is no direction, so keep the old one.
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            aimDirection = toMouse.normalized;
        }
    }

    /// <summary>
    /// Places the crosshair in world space at a fixed distance along the aim direction.
    /// </summary>
    private void UpdateCrosshairPosition()
    {
        if (crosshair == null) return;

        crosshair.position = AimOrigin + aimDirection * crosshairDistance;
    }

    /// <summary>
    /// Draws the aim origin in the Scene view so you can tune aimOriginOffset.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AimOrigin, 0.1f);
    }
}
