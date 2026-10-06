using System.Collections;
using UnityEngine;

/// <summary>
/// BLOWTORCH (tool): aim with the mouse, right-click, and it burns a tunnel through the ground in
/// that direction, big enough to walk through. Using it does NOT end the turn (Ends Turn On Fire is
/// off on its WeaponData), so you can walk into the tunnel afterwards. It does no damage.
///
/// Asset: Assets/Weapons/Special/Attacks/Blowtorch Attack.
/// </summary>
[CreateAssetMenu(fileName = "Blowtorch Attack", menuName = "Cockwar/Special Attacks/Blowtorch")]
public class BlowtorchAttack : SpecialAttack
{
    [Tooltip("How long the tunnel is, in world units.")]
    [SerializeField] private float length = 45f;

    [Tooltip("Tunnel radius. A cockroach is about 5 wide and 10 tall, so 6.5 fits one standing up.")]
    [SerializeField] private float radius = 6.5f;

    [Tooltip("Seconds it takes to burn the whole tunnel.")]
    [SerializeField] private float seconds = 1.2f;

    [SerializeField] private AudioClip torchSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Tooltip("Colour of the tunnel preview shown while the Blowtorch is selected.")]
    [SerializeField] private Color previewColor = new Color(1f, 0.55f, 0.15f, 1f);

    public override SpecialActivation Activation => SpecialActivation.RightClick;
    public override bool ShowsCrosshair => true;

    /// <summary>The crosshair sits at the far end of the tunnel, so it shows exactly where the burn stops.</summary>
    public override float CrosshairDistance => length;

    /// <summary>
    /// Shows the exact tunnel: from the body's centre (where the burn starts), at full length and
    /// width, straight through walls. The crosshair marks the far end (see CrosshairDistance).
    /// </summary>
    public override void ShowAimPreview(CockroachShooting shooter, Vector2 aimOrigin, Vector2 aimDirection)
    {
        Collider2D body = shooter.GetComponent<Collider2D>();
        Vector2 start = body != null ? (Vector2)body.bounds.center : aimOrigin;
        PathPreview.ShowPath(start, aimDirection, length, radius * 2f, previewColor, false);
    }

    public override Projectile Begin(AttackContext context)
    {
        if (context.shooter == null) return null;
        if (torchSound != null) Sfx.Play(torchSound, volume);

        GameObject go = new GameObject("Blowtorch");
        BlowtorchDig dig = go.AddComponent<BlowtorchDig>();
        dig.StartCoroutine(dig.Run(this, context.shooter.transform, context.direction));
        return null;
    }

    public float Length => length;
    public float Radius => radius;
    public float Seconds => Mathf.Max(0.1f, seconds);

    protected override Sprite BuildIcon() => WeaponArt.Blowtorch();
    protected override Sprite BuildHeldSprite() => WeaponArt.Blowtorch();
}
