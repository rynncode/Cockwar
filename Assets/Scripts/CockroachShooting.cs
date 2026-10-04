using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

/// <summary>
/// Step 4: Projectile (firing side).
/// Hold the left mouse button to charge power, release to fire.
/// Reads aim direction and origin from CockroachAim.
/// Step 9: raises OnFired after each shot so the TurnManager knows the turn's shot is done.
/// Step 13: multiple weapons. If the Weapons list has entries, the current
/// weapon decides the projectile, power range, whether firing ends the turn,
/// and ammo. If the list is empty, the old single-weapon fields below are used,
/// so a cockroach set up before step 13 keeps working unchanged.
/// The selected weapon and the ammo left persist across turns.
/// </summary>
[RequireComponent(typeof(CockroachAim))]
[RequireComponent(typeof(CockroachMovement))]
public class CockroachShooting : MonoBehaviour
{
    [Header("Step 13: Weapons")]
    [Tooltip("Weapon assets this cockroach can use. Leave empty to use the single-weapon fields below.")]
    public List<WeaponData> weapons = new List<WeaponData>();

    [Header("Projectile (used only if the Weapons list is empty)")]
    [Tooltip("Prefab that has the Projectile script on it.")]
    public GameObject projectilePrefab;

    [Header("Power")]
    [Tooltip("Launch speed when the mouse is clicked and released instantly. (Single-weapon mode only.)")]
    public float minPower = 10f;

    [Tooltip("Launch speed when fully charged. (Single-weapon mode only.)")]
    public float maxPower = 40f;

    [Tooltip("How many seconds of holding it takes to go from min to max power. Shared by all weapons.")]
    public float maxChargeTime = 1.5f;

    [Header("Turn System")]
    [FormerlySerializedAs("endsTurnOnFire")]
    [Tooltip("Whether firing ends the turn. (Single-weapon mode only. With weapons, each weapon has its own setting.)")]
    public bool singleWeaponEndsTurn = true;

    private CockroachAim aim;
    private CockroachMovement movement;
    private Collider2D ownCollider;

    private bool isCharging;
    private float chargeStartTime;

    private int currentWeaponIndex;
    private int[] ammoLeft;

    // --- Read-only state, useful for UI ---

    /// <summary>0 when not charging, 1 when fully charged. For a power bar in step 15.</summary>
    public float ChargeRatio01 { get; private set; }

    /// <summary>True while the mouse is held down charging a shot.</summary>
    public bool IsCharging => isCharging;

    /// <summary>The selected weapon, or null in single-weapon mode.</summary>
    public WeaponData CurrentWeapon =>
        (weapons != null && weapons.Count > 0) ? weapons[Mathf.Clamp(currentWeaponIndex, 0, weapons.Count - 1)] : null;

    public int CurrentWeaponIndex => currentWeaponIndex;

    /// <summary>Whether firing the current weapon ends the turn. TurnManager reads this.</summary>
    public bool EndsTurnOnFire => CurrentWeapon != null ? CurrentWeapon.endsTurnOnFire : singleWeaponEndsTurn;

    /// <summary>Shots left for the weapon at this index. -1 = unlimited.</summary>
    public int GetAmmo(int index)
    {
        if (ammoLeft == null || index < 0 || index >= ammoLeft.Length) return -1;
        return ammoLeft[index];
    }

    /// <summary>Raised when the selected weapon changes (the panel listens to this).</summary>
    public event System.Action OnWeaponChanged;

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

        ammoLeft = new int[weapons.Count];
        for (int i = 0; i < weapons.Count; i++)
        {
            ammoLeft[i] = weapons[i] != null ? weapons[i].ammo : -1;
        }
    }

    /// <summary>
    /// Picks a weapon by index. Ignored while charging a shot or if the index is invalid.
    /// Returns true if the weapon is now selected.
    /// </summary>
    public bool SelectWeapon(int index)
    {
        if (isCharging) return false;
        if (weapons == null || index < 0 || index >= weapons.Count) return false;
        if (weapons[index] == null) return false;

        currentWeaponIndex = index;
        OnWeaponChanged?.Invoke();
        return true;
    }

    private bool HasAmmo()
    {
        if (CurrentWeapon == null) return true;
        int left = GetAmmo(currentWeaponIndex);
        return left < 0 || left > 0;
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
            // A click on UI (the Weapons button, the panel) must not start a shot.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (!overUI && HasAmmo())
            {
                isCharging = true;
                chargeStartTime = Time.time;
            }
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
        WeaponData weapon = CurrentWeapon;
        GameObject prefab = weapon != null ? weapon.projectilePrefab : projectilePrefab;
        float low = weapon != null ? weapon.minPower : minPower;
        float high = weapon != null ? weapon.maxPower : maxPower;

        if (prefab == null)
        {
            Debug.LogError("CockroachShooting: no Projectile Prefab assigned" +
                           (weapon != null ? " on weapon '" + weapon.name + "'." : "."));
            return;
        }

        float power = Mathf.Lerp(low, high, ChargeRatio01);

        GameObject shot = Instantiate(prefab, aim.AimOrigin, Quaternion.identity);

        // Prevent the projectile from colliding with the cockroach that just fired it,
        // regardless of aim direction or how close the spawn point is to the body.
        Collider2D shotCollider = shot.GetComponent<Collider2D>();
        if (shotCollider != null && ownCollider != null)
        {
            Physics2D.IgnoreCollision(shotCollider, ownCollider, true);
        }

        Projectile projectile = shot.GetComponent<Projectile>();
        projectile.Launch(aim.AimDirection, power);

        // Use up one shot of limited ammo, and let the panel refresh.
        if (weapon != null && ammoLeft[currentWeaponIndex] > 0)
        {
            ammoLeft[currentWeaponIndex]--;
            OnWeaponChanged?.Invoke();
        }

        // Tell anyone listening (the TurnManager) that a shot was fired,
        // and which projectile it was, so the camera can follow it.
        OnFired?.Invoke();
        OnProjectileLaunched?.Invoke(projectile);
    }
}
