using UnityEngine;

/// <summary>
/// GRAPPLING HOOK (tool): aim with the mouse. A line shows where the hook will go: green if it
/// will catch the ground within reach, red if not (clicking red does nothing, so no use is wasted).
/// Left-click fires it; the cockroach then hangs from the rope:
///   W / Up arrow     climb (shorten the rope)
///   S / Down arrow   lower (lengthen the rope)
///   A / D            swing
///   Jump (Space)     let go, keeping your speed
/// Firing again while hanging moves the hook to a new spot. Each shot is one use (the weapon
/// asset gives 2). It does NOT end the turn. The rope also lets go when the turn ends or the
/// ground it is hooked into is blown away. The rope itself is GrappleRope.
///
/// Asset: Assets/Weapons/Special/Attacks/Grappling Hook Attack.
/// </summary>
[CreateAssetMenu(fileName = "Grappling Hook Attack", menuName = "Cockwar/Special Attacks/Grappling Hook")]
public class GrappleHookAttack : SpecialAttack
{
    [Header("Rope")]
    [Tooltip("Furthest the hook can reach, in world units.")]
    [SerializeField] private float maxLength = 70f;

    [Tooltip("Shortest the rope can be climbed to.")]
    [SerializeField] private float minLength = 4f;

    [Tooltip("How fast W / S climb and lower, in world units per second.")]
    [SerializeField] private float climbSpeed = 30f;

    [Tooltip("How hard A / D swing you.")]
    [SerializeField] private float swingForce = 45f;

    [Header("Look")]
    [SerializeField] private Color ropeColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    [SerializeField] private float ropeWidth = 0.5f;
    [SerializeField] private float clawSize = 3f;

    [Tooltip("Optional art for the claw at the end of the rope (empty = built-in).")]
    [SerializeField] private Sprite clawSprite;

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip releaseSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private static readonly Color CanHook = new Color(0.35f, 1f, 0.45f, 1f);
    private static readonly Color CannotHook = new Color(1f, 0.3f, 0.25f, 1f);

    public override SpecialActivation Activation => SpecialActivation.ClickTarget;
    public override bool ShowsCrosshair => true;

    public float MaxLength => maxLength;
    public float MinLength => Mathf.Max(1f, minLength);
    public float ClimbSpeed => climbSpeed;
    public float SwingForce => swingForce;
    public Color RopeColor => ropeColor;
    public float RopeWidth => ropeWidth;
    public float ClawSize => clawSize;
    public Sprite ClawSprite => clawSprite != null ? clawSprite : WeaponArt.GrappleClaw();

    /// <summary>Only spots where the hook will catch can be clicked.</summary>
    public override bool IsValidTarget(Vector2 target, CockroachShooting shooter) => Cast(shooter, target, out _);

    public override void ShowAimPreview(CockroachShooting shooter, Vector2 aimOrigin, Vector2 aimDirection)
    {
        Vector2 mouse = WeaponFx.MouseWorld();
        bool hits = Cast(shooter, mouse, out RaycastHit2D hit);
        float length = hits ? hit.distance : maxLength;
        PathPreview.ShowPath(aimOrigin, mouse - aimOrigin, length, 1.2f, hits ? CanHook : CannotHook);
    }

    public override Projectile Begin(AttackContext context)
    {
        if (context.shooter == null || !Cast(context.shooter, context.target, out RaycastHit2D hit)) return null;

        GrappleRope rope = context.shooter.GetComponent<GrappleRope>();
        if (rope == null) rope = context.shooter.gameObject.AddComponent<GrappleRope>();
        rope.Attach(this, hit.point, hit.normal);

        if (fireSound != null) Sfx.Play(fireSound, volume);
        return null;
    }

    public void PlayRelease() { if (releaseSound != null) Sfx.Play(releaseSound, volume); }

    /// <summary>
    /// Throws the hook from the aim origin toward the mouse and finds the first ground it would
    /// catch, within reach. Only the ground counts (not cockroaches or crates).
    /// </summary>
    private bool Cast(CockroachShooting shooter, Vector2 target, out RaycastHit2D hit)
    {
        hit = default;
        if (shooter == null) return false;

        CockroachAim aim = shooter.GetComponent<CockroachAim>();
        Vector2 origin = aim != null ? aim.AimOrigin : (Vector2)shooter.transform.position;
        Vector2 direction = target - origin;
        if (direction.sqrMagnitude < 0.0001f) return false;

        CockroachMovement movement = shooter.GetComponent<CockroachMovement>();
        int groundMask = movement != null && movement.groundLayer.value != 0 ? movement.groundLayer.value : LayerMask.GetMask("Ground");

        hit = Physics2D.Raycast(origin, direction.normalized, maxLength, groundMask);
        return hit.collider != null && !hit.collider.isTrigger;
    }

    protected override Sprite BuildIcon() => WeaponArt.GrappleGun();
    protected override Sprite BuildHeldSprite() => WeaponArt.GrappleGun();
}
