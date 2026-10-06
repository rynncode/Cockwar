using UnityEngine;

/// <summary>
/// CALL AN AIRSTRIKE?!: right-click to press the big red button. A bomber crosses the whole map
/// and drops a string of rockets. Some are aimed (roughly!) at players, the rest land anywhere,
/// so everyone is at risk, including whoever called it. Each rocket: 20 damage, small crater.
///
/// Asset: Assets/Weapons/Special/Attacks/Airstrike Attack. The flight is AirstrikeRun and each
/// rocket is AirstrikeRocket.
/// </summary>
[CreateAssetMenu(fileName = "Airstrike Attack", menuName = "Cockwar/Special Attacks/Airstrike")]
public class AirstrikeAttack : SpecialAttack
{
    [Header("Rockets")]
    [SerializeField, Min(1)] private int rocketCount = 7;

    [Tooltip("Damage at the centre of each rocket's blast. Friendly fire: the attacker can be hit too.")]
    [SerializeField] private int damagePerRocket = 20;

    [Tooltip("Damage at the edge of a blast, as a fraction of the damage above.")]
    [SerializeField, Range(0f, 1f)] private float edgeDamageFraction = 0.5f;

    [SerializeField] private float blastRadius = 12f;
    [SerializeField] private float craterRadius = 11f;
    [SerializeField] private float knockback = 45f;

    [Tooltip("Explosion prefab for the look and sound of each rocket (its numbers are replaced by the ones above).")]
    [SerializeField] private GameObject explosionPrefab;

    [Header("Chaos")]
    [Tooltip("Share of the rockets aimed near a random player (any player, the attacker included). The rest land anywhere on the map.")]
    [SerializeField, Range(0f, 1f)] private float aimedAtPlayersShare = 0.6f;

    [Tooltip("How far an aimed rocket can miss by, in world units.")]
    [SerializeField] private float scatter = 30f;

    [Header("Bomber")]
    [SerializeField] private float planeSpeed = 110f;

    [Tooltip("How high above the highest ground the bomber flies.")]
    [SerializeField] private float planeHeightAboveGround = 45f;

    [SerializeField] private float planeSize = 34f;
    [SerializeField] private float rocketSize = 6f;

    [Tooltip("Gravity on the rockets after they are released.")]
    [SerializeField] private float rocketGravity = 70f;

    [Tooltip("Seconds the remote is shown before the bomber comes (the button-press moment).")]
    [SerializeField] private float buttonSeconds = 0.9f;

    [Header("Art (optional; empty = built-in pixel art)")]
    [SerializeField] private Sprite planeSprite;
    [SerializeField] private Sprite rocketSprite;

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip planeSound;
    [SerializeField] private AudioClip rocketLaunchSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    public override SpecialActivation Activation => SpecialActivation.RightClick;

    public int RocketCount => rocketCount;
    public float AimedAtPlayersShare => aimedAtPlayersShare;
    public float Scatter => scatter;
    public float PlaneSpeed => planeSpeed;
    public float PlaneHeightAboveGround => planeHeightAboveGround;
    public float PlaneSize => planeSize;
    public float RocketSize => rocketSize;
    public float RocketGravity => rocketGravity;
    public float ButtonSeconds => buttonSeconds;
    public Sprite PlaneSprite => planeSprite != null ? planeSprite : CrateArt.Aircraft();
    public Sprite RocketSprite => rocketSprite != null ? rocketSprite : WeaponArt.Rocket();
    public AudioClip PlaneSound => planeSound;
    public float Volume => volume;

    public override Projectile Begin(AttackContext context)
    {
        AirstrikeRun.Launch(this, context.shooter != null ? context.shooter.transform : null);
        return null;
    }

    public void PlayButton() { if (buttonSound != null) Sfx.Play(buttonSound, volume); }
    public void PlayRocketLaunch() { if (rocketLaunchSound != null) Sfx.Play(rocketLaunchSound, volume * 0.7f, 0.15f); }

    /// <summary>Called by a rocket when it hits.</summary>
    public void Impact(Vector2 point)
    {
        // The ground-hugging Explosion frames (WeaponEffectArt > Rocket Blast). Their burst sits a
        // little below the middle of each frame, hence the small lift.
        WeaponEffectArt art = WeaponEffectArt.Get();
        WeaponFx.Blast(explosionPrefab, point, explosion =>
        {
            explosion.maxDamage = damagePerRocket;
            explosion.minDamageFraction = edgeDamageFraction;
            explosion.blastRadius = blastRadius;
            explosion.craterRadius = craterRadius;
            explosion.maxKnockback = knockback;
        }, art != null ? art.rocketBlast : null, blastRadius * 4f, 16f, 0.08f);
    }

    protected override Sprite BuildIcon() => WeaponArt.Remote(false);
    protected override Sprite BuildHeldSprite() => WeaponArt.Remote(false);
}
