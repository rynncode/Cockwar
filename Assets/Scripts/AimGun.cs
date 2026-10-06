using UnityEngine;

/// <summary>
/// Step 13c: a gun sprite that follows the crosshair.
/// While the cockroach is actively charging a shot, shows the current weapon's "Held Sprite"
/// (WeaponData) in front of it, rotated to point along the aim direction (at the crosshair).
/// The gun is hidden whenever the cockroach is in the air or dead/dying.
/// Add this ONE component to each cockroach. Weapons with no held sprite show nothing.
/// The gun is drawn by a separate, unparented object because the cockroach flips its scale
/// to face left/right, which would otherwise mirror and fight the rotation.
/// Why LateUpdate: CockroachAim updates the aim direction in Update, so by LateUpdate it is fresh.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
[RequireComponent(typeof(CockroachShooting))]
[RequireComponent(typeof(CockroachAim))]
public class AimGun : MonoBehaviour
{
    [Tooltip("The cockroach's body SpriteRenderer. Used to draw the gun on the same layer, just in front. Leave empty to find it automatically.")]
    public SpriteRenderer bodyRenderer;

    [Tooltip("How far in front of the aim origin the center of the gun sits, in cockroach-sized units (it scales with the cockroach).")]
    public float gunDistance = 0.4f;

    [Tooltip("Extra size multiplier for the gun. 1 = same pixel size as the cockroach.")]
    public float gunScale = 1f;

    [Tooltip("While charging, turn the cockroach to face the side you are aiming at.")]
    public bool faceAimWhileCharging = true;

    private CockroachMovement movement;
    private CockroachShooting shooting;
    private CockroachAim aim;
    private Health health;

    private SpriteRenderer gun;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        shooting = GetComponent<CockroachShooting>();
        aim = GetComponent<CockroachAim>();
        health = GetComponent<Health>();

        if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer == null)
        {
            Debug.LogWarning("AimGun: no SpriteRenderer found on " + name + ". Assign the Body Renderer field.");
        }
    }

    private void LateUpdate()
    {
        WeaponData weapon = shooting.CurrentWeapon;
        Sprite sprite = weapon != null ? weapon.heldSprite : null;
        SpecialAttack special = SpecialAttack.Of(weapon);
        if (sprite == null && special != null) sprite = special.HeldSprite;

        // Targeted and right-click specials have no charge, so they are held the whole turn instead.
        bool holding = shooting.IsCharging || (special != null && special.Activation != SpecialActivation.ChargeShot && !shooting.FiringLocked);

        // Only while actively charging, standing on the ground, and alive.
        // shooting.enabled is false once CockroachDeath starts, which covers the death animation;
        // the Health check also covers the moment between taking lethal damage and that starting.
        bool dead = (health != null && health.IsDead) || !shooting.enabled;
        bool show = sprite != null
                    && !dead
                    && movement.isMyTurn
                    && holding
                    && movement.IsGrounded;

        if (!show)
        {
            Hide();
            return;
        }

        EnsureGun();

        // Turn the body to face the aim side while charging (the art faces right).
        if (faceAimWhileCharging && Mathf.Abs(aim.AimDirection.x) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(aim.AimDirection.x);
            transform.localScale = scale;
        }

        // Match the cockroach's pixel size, so the gun looks like it belongs to it.
        float size = Mathf.Abs(transform.lossyScale.y) * gunScale * (weapon != null ? weapon.heldScale : 1f);

        gun.sprite = sprite;
        gun.transform.localScale = new Vector3(size, size, 1f);
        gun.transform.position = aim.AimOrigin + aim.AimDirection * (gunDistance * Mathf.Abs(transform.lossyScale.y));
        gun.transform.rotation = Quaternion.Euler(0f, 0f, aim.AimAngle);

        // Aiming left would turn the gun upside down, so mirror it vertically to keep it upright.
        gun.flipY = aim.AimDirection.x < 0f;

        if (bodyRenderer != null)
        {
            gun.sortingLayerID = bodyRenderer.sortingLayerID;
            gun.sortingOrder = bodyRenderer.sortingOrder + 1;
        }

        if (!gun.enabled) gun.enabled = true;
    }

    private void EnsureGun()
    {
        if (gun != null) return;

        GameObject go = new GameObject("Held Gun (" + name + ")");
        gun = go.AddComponent<SpriteRenderer>();

        // Use the same material as the body so lighting behaves the same in this URP project.
        if (bodyRenderer != null) gun.sharedMaterial = bodyRenderer.sharedMaterial;
    }

    private void Hide()
    {
        if (gun != null && gun.enabled) gun.enabled = false;
    }

    private void OnDisable()
    {
        // Covers death (CockroachDeath turns this cockroach off): never leave a gun behind.
        Hide();
    }

    private void OnDestroy()
    {
        if (gun != null) Destroy(gun.gameObject);
    }
}
