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

    /// <summary>
    /// True while retreating after a fused shot: the cockroach can still move, but cannot fire,
    /// aim or switch weapon. Set by TurnManager, cleared when the next turn starts.
    /// </summary>
    public bool FiringLocked { get; set; }

    /// <summary>True while driving a vehicle (the tank): the tank fires instead of this cockroach.</summary>
    public bool InVehicle { get; set; }

    /// <summary>True while this cockroach's own weapons can't be aimed or fired (retreating, or in a vehicle).</summary>
    public bool CannotFire => FiringLocked || InVehicle;

    /// <summary>
    /// A vehicle this cockroach is driving fired a shot: report it exactly like a normal shot, so the
    /// camera follows the shell and the TurnManager ends the turn once it lands.
    /// </summary>
    public void ReportVehicleShot(Projectile shell)
    {
        LastShotEndsTurn = true;
        if (shell != null) OnProjectileLaunched?.Invoke(shell);
        OnFired?.Invoke();
    }

    /// <summary>Whether the shot just fired ends the turn. TurnManager reads this in OnFired.</summary>
    public bool LastShotEndsTurn { get; private set; } = true;

    /// <summary>Shots left for the weapon at this index. -1 = unlimited.</summary>
    public int GetAmmo(int index)
    {
        if (ammoLeft == null || index < 0 || index >= ammoLeft.Length) return -1;
        return ammoLeft[index];
    }

    /// <summary>
    /// The round the match is in (1 = everyone's first turn). TurnManager keeps this up to date;
    /// a weapon with a Round Delay unlocks once enough rounds have passed.
    /// </summary>
    public int Round { get; set; } = 1;

    /// <summary>Rounds until the weapon at this index unlocks. 0 = usable now.</summary>
    public int RoundsUntilAvailable(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Count || weapons[index] == null) return 0;
        if (unlockedByCrate.Contains(weapons[index])) return 0;
        return Mathf.Max(0, weapons[index].roundDelay + 1 - Round);
    }

    // Weapons picked up from a crate are usable straight away, even if their Round Delay has not passed.
    private readonly HashSet<WeaponData> unlockedByCrate = new HashSet<WeaponData>();

    /// <summary>
    /// Adds shots of a weapon, e.g. from a supply crate. A weapon already in the list gets
    /// the extra ammo; a new one is added to the end of the list (and to the weapon panel).
    /// An amount of 0 only adds the weapon to the panel with no ammo. A weapon with unlimited ammo stays unlimited. Returns false if nothing could be given
    /// (no weapon, or this cockroach uses the old single-weapon mode with an empty list).
    /// </summary>
    public bool GiveWeapon(WeaponData weapon, int amount)
    {
        if (weapon == null || weapons == null || weapons.Count == 0 || amount < 0) return false;

        int index = weapons.IndexOf(weapon);
        if (index < 0)
        {
            weapons.Add(weapon);
            System.Array.Resize(ref ammoLeft, weapons.Count);
            index = weapons.Count - 1;
            ammoLeft[index] = 0;
        }

        if (ammoLeft[index] >= 0) ammoLeft[index] += amount;
        if (amount > 0) unlockedByCrate.Add(weapon);

        OnWeaponChanged?.Invoke();
        return true;
    }

    /// <summary>True if the weapon at this index exists, has ammo left and is not locked by its round delay.</summary>
    public bool IsWeaponReady(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Count || weapons[index] == null) return false;
        return GetAmmo(index) != 0 && RoundsUntilAvailable(index) == 0;
    }

    /// <summary>Raised when the selected weapon changes (the panel listens to this).</summary>
    public event System.Action OnWeaponChanged;

    /// <summary>
    /// Raised right after a projectile is fired. The TurnManager listens to this
    /// to end the turn. An event (instead of the TurnManager checking every frame)
    /// keeps this script unaware of the turn system.
    /// </summary>
    public event System.Action OnFired;

    /// <summary>Raised when the player cancels a charge with the right mouse button. Nothing was fired.</summary>
    public event System.Action OnChargeCancelled;

    /// <summary>
    /// Raised once per firing action with the weapon that fired (null in single-weapon mode).
    /// Raised before an emptied weapon is swapped out, so listeners see the weapon actually used.
    /// </summary>
    public event System.Action<WeaponData> OnWeaponFired;

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

    /// <summary>
    /// Selects the next usable weapon in the list, wrapping back to the start
    /// (empty and still-locked weapons are skipped).
    /// Reuses SelectWeapon, so this is blocked while charging and still
    /// raises OnWeaponChanged for the panel to refresh.
    /// </summary>
    private void CycleWeapon()
    {
        if (weapons == null || weapons.Count <= 1) return;

        int next = FindReadyWeaponAfter(currentWeaponIndex);
        if (next >= 0) SelectWeapon(next);
    }

    /// <summary>First usable weapon after the given index, wrapping around. -1 if there is none.</summary>
    private int FindReadyWeaponAfter(int index)
    {
        for (int step = 1; step <= weapons.Count; step++)
        {
            int candidate = (index + step) % weapons.Count;
            if (candidate != index && IsWeaponReady(candidate)) return candidate;
        }

        return -1;
    }

    private bool CanFireCurrentWeapon()
    {
        // Never start a second attack while a meteor, UFO, airstrike... from the last one is still going.
        if (AttackRunner.AnyActive) return false;
        if (CurrentWeapon == null) return true;
        return IsWeaponReady(currentWeaponIndex);
    }

    private void HandleSpecialInput(SpecialAttack special)
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (CanFireCurrentWeapon()) special.ShowAimPreview(this, aim.AimOrigin, aim.AimDirection);

        if (special.Activation == SpecialActivation.ClickTarget)
        {
            Vector2 mouse = WeaponFx.MouseWorld();
            bool valid = special.IsValidTarget(mouse, this);
            TargetMarker.ShowAt(special.TargetMarker, mouse, special.TargetMarkerSize, valid ? Color.white : new Color(1f, 0.25f, 0.2f, 1f));

            if (Input.GetMouseButtonDown(0) && !overUI && valid && CanFireCurrentWeapon())
                FireSpecial(special, mouse, 0f);
        }
        else if (special.Activation == SpecialActivation.RightClick)
        {
            if (Input.GetMouseButtonDown(1) && CanFireCurrentWeapon())
                FireSpecial(special, transform.position, 0f);
        }
    }

    /// <summary>
    /// Runs a special attack, then uses ammo and raises the same events as a normal shot, so the
    /// sounds, the weapon panel and the TurnManager treat it like any other weapon.
    /// </summary>
    private void FireSpecial(SpecialAttack special, Vector2 target, float power)
    {
        AttackContext context = new AttackContext
        {
            shooter = this,
            weapon = CurrentWeapon,
            origin = aim.AimOrigin,
            direction = aim.AimDirection,
            power = power,
            target = target
        };

        Projectile tracked = special.Begin(context);

        // Ignore the shooter, the same as a normal shot does.
        if (tracked != null)
        {
            Collider2D shotCollider = tracked.GetComponent<Collider2D>();
            if (shotCollider != null && ownCollider != null) Physics2D.IgnoreCollision(shotCollider, ownCollider, true);
            OnProjectileLaunched?.Invoke(tracked);
        }

        FinishFiring(CurrentWeapon);
    }

    private void Update()
    {
        // Paused: drop the charge. The mouse release would happen in the menu and never reach us.
        if (GameMenu.IsPaused)
        {
            CancelCharge();
            return;
        }

        // Not our turn, or retreating after a fused shot (we may walk, but not fire or switch).
        if (!movement.isMyTurn || CannotFire)
        {
            // Also reset the charge, so a turn that ends mid-charge
            // does not leave a half-full power bar behind.
            isCharging = false;
            ChargeRatio01 = 0f;
            return;
        }

        // Next Weapon key (set in the settings menu) cycles without opening the panel.
        if (Input.GetKeyDown(GameSettings.Key(GameAction.NextWeapon)) && !isCharging)
        {
            CycleWeapon();
        }

        // Special weapons that are not charged (Satelaser: click a target; Airstrike, Demon Fire,
        // Doom: right-click) have their own input. Charged ones (Reneitor) fall through to the
        // normal hold-and-release below.
        SpecialAttack special = SpecialAttack.Of(CurrentWeapon);
        if (special != null && special.Activation != SpecialActivation.ChargeShot)
        {
            isCharging = false;
            ChargeRatio01 = 0f;
            HandleSpecialInput(special);
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // A click on UI (the Weapons button, the panel) must not start a shot.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (!overUI && CanFireCurrentWeapon())
            {
                isCharging = true;
                chargeStartTime = Time.time;
            }
        }

        // Right mouse button cancels a charge in progress: nothing is fired and no ammo is used.
        // The left button is still held at this point; releasing it later does nothing because
        // the charge is already over (a new shot needs a fresh click).
        if (isCharging && Input.GetMouseButtonDown(1))
        {
            CancelCharge();
        }

        if (isCharging)
        {
            float heldSeconds = Time.time - chargeStartTime;
            ChargeRatio01 = Mathf.Clamp01(heldSeconds / maxChargeTime);
        }

        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            // No longer charging before Fire runs, so Fire can switch weapon if this one runs out.
            isCharging = false;
            Fire();
            ChargeRatio01 = 0f;
        }
    }

    /// <summary>Stops a charge without firing. Safe to call when not charging.</summary>
    public void CancelCharge()
    {
        if (!isCharging) return;

        isCharging = false;
        ChargeRatio01 = 0f;
        OnChargeCancelled?.Invoke();
    }

    private void Fire()
    {
        WeaponData weapon = CurrentWeapon;
        GameObject prefab = weapon != null ? weapon.projectilePrefab : projectilePrefab;
        float low = weapon != null ? weapon.minPower : minPower;
        float high = weapon != null ? weapon.maxPower : maxPower;

        // A charged special attack (Reneitor) launches its own projectile with this power.
        SpecialAttack special = SpecialAttack.Of(weapon);
        if (special != null)
        {
            FireSpecial(special, aim.AimOrigin + aim.AimDirection, Mathf.Lerp(low, high, ChargeRatio01));
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("CockroachShooting: no Projectile Prefab assigned" +
                           (weapon != null ? " on weapon '" + weapon.name + "'." : "."));
            return;
        }

        float power = Mathf.Lerp(low, high, ChargeRatio01);

        // Step 13: Multi-Shot. A weapon can fire more than one projectile per
        // shot, fanned out across an angle centred on the aim direction.
        // Everything else (a single shot) is just this loop running once.
        int shotCount = weapon != null ? Mathf.Max(1, weapon.multiShotCount) : 1;
        float spread = weapon != null ? weapon.multiShotSpreadDegrees : 0f;

        // Only one projectile per shot is reported to the camera/turn system
        // (the middle one), so a 5-shot weapon does not need five separate
        // "wait for landing" trackers. The others still fly, hit and explode
        // normally — they are just not individually followed or waited on.
        int trackedIndex = shotCount / 2;

        // All of a multi-shot volley spawns at the exact same point in the
        // same frame, so their colliders start out overlapping each other —
        // physics would otherwise shove them violently apart the instant it
        // next runs, which can easily fling one straight back into the
        // shooter. Every pellet in this volley ignores collision with every
        // other pellet already spawned in it, the same way each already
        // ignores collision with the shooter itself.
        List<Collider2D> volleyColliders = new List<Collider2D>();

        for (int i = 0; i < shotCount; i++)
        {
            Vector2 direction = aim.AimDirection;

            if (shotCount > 1)
            {
                float t = (float)i / (shotCount - 1); // 0 to 1 across the fan
                float angleOffset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                direction = Quaternion.Euler(0f, 0f, angleOffset) * direction;
            }

            GameObject shot = Instantiate(prefab, aim.AimOrigin, Quaternion.identity);
            Collider2D shotCollider = shot.GetComponent<Collider2D>();

            // Prevent the projectile from colliding with the cockroach that just fired it,
            // regardless of aim direction or how close the spawn point is to the body.
            if (shotCollider != null && ownCollider != null)
            {
                Physics2D.IgnoreCollision(shotCollider, ownCollider, true);
            }

            if (shotCollider != null)
            {
                foreach (Collider2D sibling in volleyColliders)
                {
                    Physics2D.IgnoreCollision(shotCollider, sibling, true);
                }
                volleyColliders.Add(shotCollider);
            }

            Projectile projectile = shot.GetComponent<Projectile>();
            projectile.Launch(direction, power);

            if (i == trackedIndex)
            {
                // Tell anyone listening (the TurnManager) which projectile to
                // follow with the camera and wait on for landing.
                OnProjectileLaunched?.Invoke(projectile);
            }
        }

        FinishFiring(weapon);
    }

    /// <summary>After any firing action: the fire sound event, one ammo used, then OnFired for the TurnManager.</summary>
    private void FinishFiring(WeaponData weapon)
    {
        // Decided now, from the weapon that actually fired: using the last shot switches to
        // another weapon below, and that one must not decide whether this shot ends the turn.
        LastShotEndsTurn = weapon != null ? weapon.endsTurnOnFire : singleWeaponEndsTurn;

        OnWeaponFired?.Invoke(weapon);

        // Use up one shot of limited ammo, and let the panel refresh.
        if (weapon != null && ammoLeft[currentWeaponIndex] > 0)
        {
            ammoLeft[currentWeaponIndex]--;
            OnWeaponChanged?.Invoke();

            // Just used the last one: have something usable in hand next turn.
            if (ammoLeft[currentWeaponIndex] == 0)
            {
                int next = FindReadyWeaponAfter(currentWeaponIndex);
                if (next >= 0) SelectWeapon(next);
            }
        }

        // One firing action, even if it launched several projectiles.
        OnFired?.Invoke();
    }
}
