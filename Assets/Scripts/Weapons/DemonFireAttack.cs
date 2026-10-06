using UnityEngine;

/// <summary>
/// ALL DEMONS ARE HERE!: right-click to raise Satan's pitchfork. Demonic fire sweeps out from the
/// caller across the battlefield's surface. Every player standing on the burning surface takes
/// 5 damage once a second for 6 seconds (30 at most). Players under cover (in a cave or tunnel,
/// under an overhang) are safe, and anyone who gets off the burning ground stops taking damage.
/// It does not change the terrain.
///
/// Asset: Assets/Weapons/Special/Attacks/Demon Fire Attack. The fire itself is DemonFireField.
/// </summary>
[CreateAssetMenu(fileName = "Demon Fire Attack", menuName = "Cockwar/Special Attacks/Demon Fire")]
public class DemonFireAttack : SpecialAttack
{
    [Header("Damage over time")]
    [SerializeField] private int damagePerTick = 5;

    [Tooltip("How many times the damage is applied (one per Tick Interval).")]
    [SerializeField, Min(1)] private int ticks = 6;

    [Tooltip("Seconds between damage ticks.")]
    [SerializeField] private float tickInterval = 1f;

    [Tooltip("Does the caller burn too, if they are standing in it?")]
    [SerializeField] private bool hurtsCaller = true;

    [Header("Area")]
    [Tooltip("On = the whole battlefield surface burns. Off = only within Area Half Width of the caller.")]
    [SerializeField] private bool wholeMap = true;
    [SerializeField] private float areaHalfWidth = 120f;

    [Tooltip("A player counts as standing in the fire if their feet are within this many units above the burning surface.")]
    [SerializeField] private float burnHeight = 8f;

    [Header("Look")]
    [Tooltip("Distance between flames along the ground.")]
    [SerializeField] private float flameSpacing = 12f;

    [Tooltip("Width of one flame frame. The Fire frames have empty space around the flame, so this is bigger than the flame looks.")]
    [SerializeField] private float flameSize = 30f;

    [Tooltip("Width of the pitchfork raised over the caller when the fire is summoned.")]
    [SerializeField] private float pitchforkSize = 30f;

    [Tooltip("How fast the fire spreads out from the caller, in world units per second.")]
    [SerializeField] private float spreadSpeed = 400f;

    [Tooltip("Flame tint. White keeps the art's own colours; the default pushes them toward demonic red.")]
    [SerializeField] private Color flameTint = new Color(1f, 0.45f, 0.4f, 1f);

    [Tooltip("Optional flame frames just for this weapon (empty = the Fire frames in Assets/Resources/WeaponEffectArt).")]
    [SerializeField] private Sprite[] flameFrames;

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip summonSound;
    [SerializeField] private AudioClip fireLoop;
    [SerializeField] private AudioClip burnTickSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    public override SpecialActivation Activation => SpecialActivation.RightClick;

    public int DamagePerTick => damagePerTick;
    public int Ticks => ticks;
    public float TickInterval => Mathf.Max(0.1f, tickInterval);
    public bool HurtsCaller => hurtsCaller;
    public bool WholeMap => wholeMap;
    public float AreaHalfWidth => areaHalfWidth;
    public float BurnHeight => burnHeight;
    public float FlameSpacing => Mathf.Max(1f, flameSpacing);
    public float FlameSize => flameSize;
    public float PitchforkSize => pitchforkSize;
    public float SpreadSpeed => Mathf.Max(1f, spreadSpeed);
    public Color FlameTint => flameTint;
    public AudioClip FireLoop => fireLoop;
    public float Volume => volume;

    /// <summary>Flame frame by index (wraps): the custom Flame Frames above, else the Fire frames from WeaponEffectArt, else built-in pixel flames.</summary>
    public Sprite FlameFrame(int index)
    {
        if (flameFrames != null && flameFrames.Length > 0)
        {
            Sprite custom = flameFrames[Mathf.Abs(index) % flameFrames.Length];
            if (custom != null) return custom;
        }

        WeaponEffectArt art = WeaponEffectArt.Get();
        if (art != null && WeaponEffectArt.Has(art.fire)) return art.fire[Mathf.Abs(index) % art.fire.Length];

        return WeaponArt.Flame(index);
    }

    public override Projectile Begin(AttackContext context)
    {
        CockroachMovement caller = context.shooter != null ? context.shooter.GetComponent<CockroachMovement>() : null;
        DemonFireField.Launch(this, caller);
        return null;
    }

    public void PlaySummon() { if (summonSound != null) Sfx.Play(summonSound, volume); }
    public void PlayTick() { if (burnTickSound != null) Sfx.Play(burnTickSound, volume, 0.1f); }

    protected override Sprite BuildIcon() => WeaponArt.Pitchfork();
    protected override Sprite BuildHeldSprite() => WeaponArt.Pitchfork();
}
