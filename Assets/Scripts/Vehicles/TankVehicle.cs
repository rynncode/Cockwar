using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tank. Dropped by the supply plane (CrateDropManager), never more than one on the map.
///
///  - Get in: walk up to it on your turn and press the Tank key (F). A "PRESS F" prompt shows.
///  - Drive with A / D, aim the cannon with the mouse, hold and release the left mouse button to
///    fire a heavy shell (that ends the turn, like any shot). Press F again to climb out.
///  - The driver is armoured: explosions, lasers and fire hurt the tank, not them. The tank's
///    health floats above it. The driver stays inside between turns (other players can't get in).
///  - At 0 health the tank explodes and throws the driver out (with some damage). It also sinks
///    and is lost in the acid. Then the plane may bring a new one.
///
/// Made in code by CrateDropManager; all settings are in the Tank section of CrateSettings.
/// </summary>
public class TankVehicle : MonoBehaviour
{
    /// <summary>The tank on the map, or null.</summary>
    public static TankVehicle Current { get; private set; }

    private const int SortingOrder = 16;

    private CrateSettings settings;
    private TurnManager turnManager;
    private float acidY;
    private float width;
    private float height;

    private Rigidbody2D body;
    private BoxCollider2D box;
    private Health health;
    private SpriteRenderer hull;
    private Transform barrelPivot;
    private SpriteRenderer flag;
    private GameObject parachute;
    private PixelNumber healthText;
    private PixelNumber prompt;
    private int shownHealth = -1;

    private CockroachMovement driver;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Collider2D> triggeredColliders = new List<Collider2D>();
    private RigidbodyType2D driverBodyType;

    private float driveInput;
    private float aimAngle = 30f;
    private bool charging;
    private float chargeStart;
    private bool destroyed;

    /// <summary>True once the tank has touched down.</summary>
    public bool HasLanded { get; private set; }

    /// <summary>The cockroach inside, or null.</summary>
    public CockroachMovement Driver => driver;

    /// <summary>True if this cockroach is sitting in the tank (explosions and damage skip it).</summary>
    public static bool IsInside(CockroachMovement player) => player != null && Current != null && Current.driver == player;

    public static TankVehicle Create(CrateDropManager manager, CrateSettings settings, Vector2 position, bool withParachute, float acidY)
    {
        // Built inactive, so Health starts with the tank's health instead of its default 100.
        GameObject go = new GameObject("Tank");
        go.SetActive(false);
        go.transform.position = position;

        TankVehicle tank = go.AddComponent<TankVehicle>();
        tank.settings = settings;
        tank.acidY = acidY;
        tank.Build(withParachute);

        go.SetActive(true);
        Current = tank;
        return tank;
    }

    private void Build(bool withParachute)
    {
        width = Mathf.Max(4f, settings.tankWidth);

        // Hull.
        GameObject hullObject = new GameObject("Hull");
        hullObject.transform.SetParent(transform, false);
        hull = hullObject.AddComponent<SpriteRenderer>();
        hull.sprite = settings.tankHullSprite != null ? settings.tankHullSprite : WeaponArt.TankHull();
        hull.sortingOrder = SortingOrder;
        WeaponFx.SetWidth(hull, width);
        // From the sprite, not the renderer: the tank is still switched off here, so renderer bounds are empty.
        height = hull.sprite.bounds.size.y * Mathf.Abs(hull.transform.localScale.y);

        // Barrel, turning on the turret.
        barrelPivot = new GameObject("Barrel Pivot").transform;
        barrelPivot.SetParent(transform, false);
        barrelPivot.localPosition = new Vector3(0f, height * 0.32f, 0f);
        SpriteRenderer barrel = WeaponFx.MakeSprite("Barrel", settings.tankBarrelSprite != null ? settings.tankBarrelSprite : WeaponArt.TankBarrel(),
                                                    barrelPivot.position, width * 0.48f, SortingOrder - 1, barrelPivot);
        barrel.transform.localPosition = Vector3.zero;

        // Flag in the driver's team colour, only while someone is inside.
        flag = WeaponFx.MakeSprite("Flag", WeaponArt.Flag(), Vector2.zero, width * 0.16f, SortingOrder + 1, transform);
        flag.transform.localPosition = new Vector3(-width * 0.3f, height * 0.2f, 0f);
        if (flag != null) flag.enabled = false;

        // Physics: heavy, upright.
        body = gameObject.AddComponent<Rigidbody2D>();
        body.mass = 12f;
        body.gravityScale = 4f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width * 0.92f, height * 0.75f);
        box.offset = new Vector2(0f, -height * 0.1f);

        health = gameObject.AddComponent<Health>();
        health.maxHealth = settings.tankHealth;
        health.OnDeath += HandleDestroyed;

        healthText = new PixelNumber(null, "Tank Health", 0, 120, 0.35f);
        healthText.SetColor(Color.white);
        prompt = new PixelNumber(null, "Tank Prompt", 0, 121, 0.35f);
        prompt.SetColor(new Color(1f, 0.9f, 0.3f, 1f));
        prompt.SetActive(false);

        if (withParachute)
        {
            parachute = new GameObject("Parachute");
            parachute.transform.SetParent(transform, false);
            parachute.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
            SpriteRenderer chute = parachute.AddComponent<SpriteRenderer>();
            chute.sprite = settings.parachuteSprite != null ? settings.parachuteSprite : CrateArt.Parachute();
            chute.sortingOrder = SortingOrder - 2;
            WeaponFx.SetWidth(chute, width * 1.2f);
        }
        else
        {
            HasLanded = true;
        }
    }

    // =====================================================================
    // Every frame
    // =====================================================================

    private void Update()
    {
        if (destroyed) return;

        // Sank into the acid: lost (the driver is thrown out into it).
        if (transform.position.y < acidY)
        {
            Explode(false);
            return;
        }

        if (driver != null)
        {
            if (!MatchTeam.IsAlive(driver))
            {
                Eject(0);
            }
            else if (driver.isMyTurn && !GameMenu.IsPaused)
            {
                DriverControls();
            }
            else
            {
                driveInput = 0f;
                charging = false;
            }
        }
        else
        {
            driveInput = 0f;
            UpdateEnterPrompt();
        }

        UpdateHealthText();
    }

    private void FixedUpdate()
    {
        if (destroyed) return;

        // Parachute: float down slowly.
        if (parachute != null)
        {
            Vector2 v = body.linearVelocity;
            v.y = Mathf.Max(v.y, -settings.parachuteFallSpeed);
            v.x = 0f;
            body.linearVelocity = v;
        }
        else
        {
            // Driving (or rolling to a stop).
            Vector2 v = body.linearVelocity;
            v.x = driveInput != 0f ? driveInput * settings.tankSpeed : Mathf.MoveTowards(v.x, 0f, 30f * Time.fixedDeltaTime);
            body.linearVelocity = v;
        }

        // The driver rides inside.
        if (driver != null)
        {
            Rigidbody2D driverBody = driver.GetComponent<Rigidbody2D>();
            Vector2 seat = body.position + Vector2.up * height * 0.1f;
            if (driverBody != null) driverBody.position = seat;
            driver.transform.position = seat;
        }
    }

    private void LateUpdate()
    {
        if (destroyed) return;

        barrelPivot.localRotation = Quaternion.Euler(0f, 0f, aimAngle);
        if (parachute != null)
            parachute.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.6f) * -5f);

        Vector3 top = transform.position + Vector3.up * (height * 0.75f);
        healthText.Transform.position = top;
        prompt.Transform.position = top + Vector3.up * 2.6f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (HasLanded || parachute == null) return;

        HasLanded = true;
        Destroy(parachute);
        parachute = null;
        if (settings.landSound != null) Sfx.Play(settings.landSound, settings.crateVolume);
        WeaponFx.Shake(2f, 0.3f);
        for (int i = 0; i < 6; i++) FxPuff.Smoke((Vector2)transform.position + Vector2.down * height * 0.4f + Random.insideUnitCircle * width * 0.4f, width * 0.2f);
    }

    // =====================================================================
    // Driving and shooting
    // =====================================================================

    private void DriverControls()
    {
        KeyCode vehicleKey = GameSettings.Key(GameAction.Vehicle);
        if (Input.GetKeyDown(vehicleKey) && !charging)
        {
            Eject(0);
            return;
        }

        // Drive.
        driveInput = 0f;
        if (HasLanded)
        {
            if (Input.GetKey(GameSettings.Key(GameAction.MoveLeft))) driveInput -= 1f;
            if (Input.GetKey(GameSettings.Key(GameAction.MoveRight))) driveInput += 1f;
        }
        if (driveInput != 0f) hull.flipX = driveInput < 0f;

        // Aim at the mouse, upward half only (the cannon can't point into its own treads).
        Vector2 toMouse = WeaponFx.MouseWorld() - (Vector2)barrelPivot.position;
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        if (angle < -15f && angle >= -90f) angle = -15f;
        else if (angle < -90f) angle = 195f;
        aimAngle = angle;

        // Charge and fire.
        if (Input.GetMouseButtonDown(0) && HasLanded) { charging = true; chargeStart = Time.time; }
        if (charging && Input.GetMouseButtonDown(1)) charging = false;   // right-click cancels

        if (charging)
        {
            float charge = Mathf.Clamp01((Time.time - chargeStart) / Mathf.Max(0.1f, settings.tankChargeSeconds));
            PathPreview.ShowPath(BarrelTip(), AimDirection(), 3f + charge * 22f, 1.4f,
                                 Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.25f, 0.05f), charge), false);

            if (Input.GetMouseButtonUp(0))
            {
                charging = false;
                Fire(Mathf.Lerp(settings.tankMinPower, settings.tankMaxPower, charge));
            }
        }
    }

    private Vector2 AimDirection() => new Vector2(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad));

    private Vector2 BarrelTip() => (Vector2)barrelPivot.position + AimDirection() * width * 0.5f;

    private void Fire(float power)
    {
        if (settings.tankShellPrefab == null)
        {
            Debug.LogWarning("TankVehicle: no Tank Shell Prefab in CrateSettings, so the cannon can't fire.");
            return;
        }

        GameObject shellObject = Instantiate(settings.tankShellPrefab, BarrelTip(), Quaternion.identity);
        Projectile shell = shellObject.GetComponent<Projectile>();
        Collider2D shellCollider = shellObject.GetComponent<Collider2D>();
        if (shellCollider != null) Physics2D.IgnoreCollision(shellCollider, box, true);
        if (shell != null) shell.Launch(AimDirection(), power);

        // Recoil and muzzle smoke.
        body.AddForce(-AimDirection() * power * body.mass * 0.04f, ForceMode2D.Impulse);
        FxPuff.Smoke(BarrelTip(), width * 0.25f);
        WeaponFx.Shake(1.5f, 0.2f);
        if (settings.tankFireSound != null) Sfx.Play(settings.tankFireSound, settings.crateVolume);

        // Reported like a normal shot: the camera follows the shell, and the turn ends when it lands.
        CockroachShooting shooting = driver.GetComponent<CockroachShooting>();
        if (shooting != null) shooting.ReportVehicleShot(shell);
    }

    // =====================================================================
    // Getting in and out
    // =====================================================================

    private void UpdateEnterPrompt()
    {
        CockroachMovement candidate = NearbyActivePlayer();
        prompt.SetActive(candidate != null);
        if (candidate == null) return;

        prompt.SetText("PRESS " + GameSettings.Key(GameAction.Vehicle).ToString().ToUpperInvariant());
        if (Input.GetKeyDown(GameSettings.Key(GameAction.Vehicle)) && !GameMenu.IsPaused) Enter(candidate);
    }

    /// <summary>The player whose turn it is, if they are standing next to the tank and could get in.</summary>
    private CockroachMovement NearbyActivePlayer()
    {
        if (!HasLanded) return null;

        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        CockroachMovement player = turnManager != null ? turnManager.CurrentPlayer : null;
        if (player == null || !player.isMyTurn || !MatchTeam.IsAlive(player)) return null;

        CockroachShooting shooting = player.GetComponent<CockroachShooting>();
        if (shooting != null && shooting.CannotFire) return null;

        Collider2D playerBody = player.GetComponent<Collider2D>();
        Vector2 point = playerBody != null ? (Vector2)playerBody.bounds.center : (Vector2)player.transform.position;
        Bounds area = box.bounds;
        area.Expand(settings.tankEnterRange * 2f);
        return area.Contains(new Vector3(point.x, point.y, area.center.z)) ? player : null;
    }

    private void Enter(CockroachMovement player)
    {
        driver = player;
        prompt.SetActive(false);

        CockroachShooting shooting = player.GetComponent<CockroachShooting>();
        if (shooting != null)
        {
            shooting.CancelCharge();
            shooting.InVehicle = true;
        }

        // Hide the cockroach and take it out of the physics: it rides along inside the tank.
        player.ExternalControl = true;
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            driverBodyType = playerBody.bodyType;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.bodyType = RigidbodyType2D.Kinematic;
        }

        hiddenRenderers.Clear();
        foreach (Renderer part in player.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled) continue;
            part.enabled = false;
            hiddenRenderers.Add(part);
        }

        triggeredColliders.Clear();
        foreach (Collider2D col in player.GetComponentsInChildren<Collider2D>())
        {
            if (col.isTrigger) continue;
            col.isTrigger = true;   // no bumping into the tank from inside
            triggeredColliders.Add(col);
        }

        flag.color = TeamColour(player);
        flag.enabled = true;
        if (settings.tankEnterSound != null) Sfx.Play(settings.tankEnterSound, settings.crateVolume);
    }

    /// <summary>Puts the driver back outside, on top of the tank, optionally hurting them.</summary>
    private void Eject(int damage)
    {
        if (driver == null) return;
        CockroachMovement player = driver;
        driver = null;
        if (flag != null) flag.enabled = false;

        foreach (Renderer part in hiddenRenderers) if (part != null) part.enabled = true;
        foreach (Collider2D col in triggeredColliders) if (col != null) col.isTrigger = false;
        hiddenRenderers.Clear();
        triggeredColliders.Clear();

        Vector2 outside = (Vector2)transform.position + Vector2.up * (height * 0.9f);
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.bodyType = driverBodyType;
            playerBody.position = outside;
            playerBody.linearVelocity = Vector2.zero;
        }
        player.transform.position = outside;
        player.ExternalControl = false;
        player.BeginFreeFall();

        CockroachShooting shooting = player.GetComponent<CockroachShooting>();
        if (shooting != null) shooting.InVehicle = false;

        if (damage > 0) WeaponFx.Damage(player, damage);
    }

    private static Color TeamColour(CockroachMovement player)
    {
        PlayerLabel label = player.GetComponent<PlayerLabel>();
        return PlayerLabel.GetPlayerColor(label != null ? label.playerNumber : 1);
    }

    // =====================================================================
    // Health and destruction
    // =====================================================================

    private void UpdateHealthText()
    {
        int value = Mathf.Max(0, health.CurrentHealth);
        if (value == shownHealth) return;
        shownHealth = value;
        healthText.SetText(value.ToString());
    }

    private void HandleDestroyed() => Explode(true);

    private void Explode(bool withBlast)
    {
        if (destroyed) return;
        destroyed = true;

        Vector2 point = transform.position;
        Eject(withBlast ? settings.tankEjectDamage : 0);

        if (withBlast)
        {
            Explosion.Spawn(settings.tankExplosionPrefab, point, explosion =>
            {
                explosion.maxDamage = settings.tankExplosionDamage;
                explosion.blastRadius = settings.tankExplosionRadius;
                explosion.craterRadius = settings.tankExplosionRadius * 0.8f;
                explosion.maxKnockback = 60f;
            });
            WeaponFx.FireBurst(point, width * 0.6f, 10);
            if (settings.tankDestroyedSound != null) Sfx.Play(settings.tankDestroyedSound, settings.crateVolume);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (health != null) health.OnDeath -= HandleDestroyed;
        healthText?.Destroy();
        prompt?.Destroy();
        Eject(0);
    }
}
