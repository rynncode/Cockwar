using UnityEngine;

/// <summary>
/// SATELASER: not a projectile. While it is selected, an alien target marker follows the mouse;
/// left-click locks the spot. A UFO flies in above it, charges up and fires a vertical laser that
/// burns down through the ground, hitting every player in the beam (50 damage each, once).
///
/// Asset: Assets/Weapons/Special/Attacks/Satelaser Attack. The UFO itself is SatelaserStrike.
/// </summary>
[CreateAssetMenu(fileName = "Satelaser Attack", menuName = "Cockwar/Special Attacks/Satelaser")]
public class SatelaserAttack : SpecialAttack
{
    [Header("Damage")]
    [Tooltip("Damage to each player caught in the beam (each player is hit once).")]
    [SerializeField] private int damage = 50;

    [Tooltip("Width of the beam, in world units. Players touching it are hit.")]
    [SerializeField] private float beamWidth = 8f;

    [Header("Terrain")]
    [Tooltip("How far below the clicked spot the beam burns into the ground.")]
    [SerializeField] private float beamDepth = 25f;

    [Tooltip("Radius of the hole the beam burns, along its whole length.")]
    [SerializeField] private float holeRadius = 6f;

    [Tooltip("Small explosion at the bottom of the beam (look and sound only; it does no damage, but it does push players a little).")]
    [SerializeField] private GameObject impactExplosionPrefab;

    [Tooltip("Push strength of that small explosion.")]
    [SerializeField] private float impactKnockback = 30f;

    [Header("UFO")]
    [Tooltip("How high above the target (or the ground over it, if that is higher) the UFO hovers.")]
    [SerializeField] private float hoverHeight = 45f;

    [Tooltip("Width of the UFO, in world units.")]
    [SerializeField] private float ufoSize = 28f;

    [SerializeField] private float entrySeconds = 1.1f;
    [SerializeField] private float chargeSeconds = 0.8f;
    [SerializeField] private float beamSeconds = 1.3f;

    [Tooltip("Optional art (empty = built-in pixel art).")]
    [SerializeField] private Sprite ufoSprite;
    [SerializeField] private Sprite targetSprite;

    [Tooltip("Beam colours: the soft haze behind the lightning, and the core (the core is only used if the lightning frames are missing).")]
    [SerializeField] private Color beamGlow = new Color(0.35f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color beamCore = new Color(0.9f, 1f, 0.9f, 1f);

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip arriveSound;
    [SerializeField] private AudioClip chargeSound;
    [SerializeField] private AudioClip laserSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    public override SpecialActivation Activation => SpecialActivation.ClickTarget;
    public override Sprite TargetMarker => targetSprite != null ? targetSprite : WeaponArt.AlienTarget();
    public override float TargetMarkerSize => Mathf.Max(beamWidth * 1.6f, 8f);

    public int Damage => damage;
    public float BeamWidth => beamWidth;
    public float BeamDepth => beamDepth;
    public float HoleRadius => holeRadius;
    public float HoverHeight => hoverHeight;
    public float UfoSize => ufoSize;
    public float EntrySeconds => entrySeconds;
    public float ChargeSeconds => chargeSeconds;
    public float BeamSeconds => beamSeconds;
    public Sprite UfoSprite => ufoSprite != null ? ufoSprite : WeaponArt.Ufo();
    public Color BeamGlow => beamGlow;
    public Color BeamCore => beamCore;

    public override Projectile Begin(AttackContext context)
    {
        SatelaserStrike.Launch(this, context.target);
        return null;
    }

    public void PlaySound(int which)
    {
        AudioClip clip = which == 0 ? arriveSound : which == 1 ? chargeSound : laserSound;
        if (clip != null) Sfx.Play(clip, volume);
    }

    /// <summary>Called by the UFO when the beam finishes: the blue burst (WeaponEffectArt > Laser Impact).</summary>
    public void Impact(Vector2 point)
    {
        WeaponEffectArt art = WeaponEffectArt.Get();
        WeaponFx.Blast(impactExplosionPrefab, point, explosion =>
        {
            explosion.maxDamage = 0;          // the beam already did the damage
            explosion.blastRadius = beamWidth * 1.5f;
            explosion.craterRadius = holeRadius * 1.6f;
            explosion.maxKnockback = impactKnockback;
        }, art != null ? art.laserImpact : null, beamWidth * 6f, 16f);
    }

    protected override Sprite BuildIcon() => WeaponArt.Ufo();
    protected override Sprite BuildHeldSprite() => WeaponArt.Ufo();
}
