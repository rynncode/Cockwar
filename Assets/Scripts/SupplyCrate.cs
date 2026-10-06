using UnityEngine;

/// <summary>
/// One supply crate lying on (or falling onto) the battlefield. Made in code by CrateDropManager.
///
/// Its kind and contents are rolled (weighted random, see CrateSettings) when it is created, and the
/// crate looks like its kind. The first living cockroach to touch it opens it: a weapon or tool goes
/// into its weapon list (CockroachShooting.GiveWeapon), health or shield goes to its Health, the
/// result floats up as text, and the crate disappears. It opens
/// only once, even if two cockroaches touch it in the same moment.
///
/// It is a normal physics object, so it falls again when an explosion digs the ground out from
/// under it, and it is lost if it sinks into the acid. Shots hit it like they hit the ground.
/// </summary>
public class SupplyCrate : MonoBehaviour
{
    // Above the cockroaches and the terrain, below the acid (50), so a sinking crate is covered.
    private const int SortingOrder = 20;

    /// <summary>True once the crate has touched down (the parachute is gone).</summary>
    public bool HasLanded { get; private set; }

    /// <summary>Turns this crate has been on the map, for crates that expire.</summary>
    public int TurnsAlive { get; set; }

    /// <summary>What is inside: the kind, and the weapon or the health amount.</summary>
    public CrateContents Contents { get; private set; }

    /// <summary>The weapon or tool inside, or null for health and shield crates.</summary>
    public CrateLoot Loot => Contents.loot;

    private CrateDropManager manager;
    private CrateSettings settings;
    private Rigidbody2D body;
    private SpriteRenderer crateRenderer;
    private GameObject parachute;
    private float acidY;
    private float swayPhase;
    private bool opened;
    private float fadeOut = -1f;   // counts down from 1 while expiring
    private float glintTimer;

    /// <summary>Creates a crate at a world position. withParachute = it floats down (plane drops).</summary>
    public static SupplyCrate Create(CrateDropManager manager, CrateSettings settings, Vector2 position, bool withParachute, float acidY)
    {
        GameObject crateObject = new GameObject("Supply Crate");
        crateObject.transform.position = position;

        SupplyCrate crate = crateObject.AddComponent<SupplyCrate>();
        crate.Setup(manager, settings, withParachute, acidY);
        return crate;
    }

    private void Setup(CrateDropManager owner, CrateSettings crateSettings, bool withParachute, float acidSurfaceY)
    {
        manager = owner;
        settings = crateSettings;
        acidY = acidSurfaceY;
        Contents = settings.RollContents();
        swayPhase = Random.value * 10f;

        float size = Mathf.Max(0.5f, settings.crateSize);

        // Art, scaled so the crate is `size` units wide whatever sprite is used.
        GameObject art = new GameObject("Art");
        art.transform.SetParent(transform, false);
        crateRenderer = art.AddComponent<SpriteRenderer>();
        crateRenderer.sprite = CrateSpriteFor(Contents.kind);
        crateRenderer.sortingOrder = SortingOrder;
        ScaleToWidth(crateRenderer, size);

        // Solid box: rests on the ground and stops shots.
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(size, size) * 0.95f;

        // Slightly bigger trigger: the pickup area, so a cockroach opens it before bumping into it.
        GameObject pickupArea = new GameObject("Pickup Area");
        pickupArea.transform.SetParent(transform, false);
        BoxCollider2D trigger = pickupArea.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(size, size) * 1.25f;

        body = gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 4f;            // same as the cockroaches
        body.mass = 3f;
        body.freezeRotation = true;        // stays upright, so it always reads as a crate
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // Never sleeps, so it notices at once when an explosion removes the ground under it.
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (withParachute)
        {
            parachute = new GameObject("Parachute");
            parachute.transform.SetParent(transform, false);
            parachute.transform.localPosition = new Vector3(0f, size * 0.45f, 0f);

            SpriteRenderer chute = parachute.AddComponent<SpriteRenderer>();
            chute.sprite = settings.parachuteSprite != null ? settings.parachuteSprite : CrateArt.Parachute();
            chute.sortingOrder = SortingOrder - 1;
            ScaleToWidth(chute, size * 1.6f);
        }
        else
        {
            HasLanded = true;
        }
    }

    private static void ScaleToWidth(SpriteRenderer spriteRenderer, float width)
    {
        float spriteWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        float scale = width / Mathf.Max(0.0001f, spriteWidth);
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void FixedUpdate()
    {
        if (parachute == null || body == null) return;

        // Parachute open: fall slowly and sway a little.
        Vector2 velocity = body.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, -settings.parachuteFallSpeed);
        velocity.x = Mathf.Sin((Time.time + swayPhase) * 1.6f) * 2f;
        body.linearVelocity = velocity;
    }

    private void Update()
    {
        if (parachute != null)
            parachute.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + swayPhase) * 1.6f) * -6f);

        // Sank into the acid (or fell off the map): gone.
        if (!opened && transform.position.y < acidY)
        {
            opened = true;
            manager?.Forget(this);
            Destroy(gameObject, 0.5f);
        }

        // Special crates glint now and then, so a rare one stands out.
        if (Contents.kind == CrateKind.Special && !opened && fadeOut < 0f)
        {
            glintTimer -= Time.deltaTime;
            if (glintTimer <= 0f)
            {
                glintTimer = Random.Range(0.25f, 0.6f);
                Vector2 spot = (Vector2)transform.position + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)) * settings.crateSize;
                FxPuff.Spawn(WeaponArt.Puff(), spot, settings.crateSize * 0.12f, settings.crateSize * 0.32f,
                             new Color(1f, 0.85f, 0.3f, 1f), 0.4f, Vector2.up * 1.5f, SortingOrder + 2);
            }
        }

        if (fadeOut >= 0f)
        {
            fadeOut -= Time.deltaTime / 0.6f;
            Color color = crateRenderer.color;
            color.a = Mathf.Clamp01(fadeOut);
            crateRenderer.color = color;
            if (fadeOut <= 0f) Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (HasLanded || parachute == null) return;

        // A cockroach under a falling crate gets it through the trigger instead.
        if (collision.collider.GetComponentInParent<CockroachMovement>() != null) return;

        HasLanded = true;
        Destroy(parachute);
        parachute = null;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

        if (settings.landSound != null) Sfx.Play(settings.landSound, settings.crateVolume, 0.08f);
    }

    // Stay as well as Enter, so a cockroach already standing in the pickup area when the crate
    // arrives (or one that could not take it a moment ago) still gets it.
    private void OnTriggerEnter2D(Collider2D other) => TryOpen(other);
    private void OnTriggerStay2D(Collider2D other) => TryOpen(other);

    private void TryOpen(Collider2D other)
    {
        if (opened || fadeOut >= 0f) return;

        CockroachShooting collector = other.GetComponentInParent<CockroachShooting>();
        if (collector == null) return;

        Health health = collector.GetComponent<Health>();
        if (health != null && health.IsDead) return;

        // Hand over the contents. A crate that cannot be used by this cockroach stays for someone else.
        string text;
        switch (Contents.kind)
        {
            case CrateKind.Health:
                if (health == null) return;
                int healed = health.Heal(Contents.amount);
                text = healed > 0 ? "+" + healed + " HEALTH" : "FULL HEALTH";
                break;

            case CrateKind.Shield:
                if (health == null) return;
                int added = health.AddShield(Contents.amount, settings.maxShieldBonus);
                text = added > 0 ? "+" + added + " SHIELD" : "SHIELD FULL";
                break;

            default:
                // Old single-weapon cockroaches have no weapon list to add to.
                if (Loot != null && !collector.GiveWeapon(Loot.weapon, Loot.ammo)) return;
                text = Loot != null ? "+" + Loot.ammo + " " + Loot.weapon.displayName : "EMPTY";
                break;
        }

        opened = true;
        manager?.Forget(this);

        if (settings.pickupSound != null) Sfx.Play(settings.pickupSound, settings.crateVolume);

        if (settings.showPickupText)
            CratePickupText.Show(text, transform.position + Vector3.up * settings.crateSize, settings.pickupTextPixelSize, collector);

        Debug.Log("SupplyCrate: " + collector.name + " opened a " + Contents.kind + " crate: " + text + ".");

        Destroy(gameObject);
    }

    /// <summary>The crate art for a kind: the sprite from CrateSettings if set, else the built-in pixel crate.</summary>
    private Sprite CrateSpriteFor(CrateKind kind)
    {
        Sprite custom = null;
        switch (kind)
        {
            case CrateKind.Weapon: custom = settings.crateSprite; break;
            case CrateKind.Special: custom = settings.specialCrateSprite; break;
            case CrateKind.Health: custom = settings.healthCrateSprite; break;
            case CrateKind.Shield: custom = settings.shieldCrateSprite; break;
            case CrateKind.Tool: custom = settings.toolCrateSprite; break;
        }
        return custom != null ? custom : CrateArt.Crate(kind);
    }

    /// <summary>Fades the crate out and removes it (crates with a lifetime).</summary>
    public void Expire()
    {
        if (opened || fadeOut >= 0f) return;

        fadeOut = 1f;
        manager?.Forget(this);

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
        if (body != null) body.simulated = false;
    }
}
