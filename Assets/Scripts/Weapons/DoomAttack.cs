using UnityEngine;

/// <summary>
/// DOOOOOOOOOM!: right-click to call it. A nuke drops from the sky next to the caller, and only then
/// is the 1% roll made:
///   - 1%: a 10-second countdown fills the screen (10, 9 ... 1, DOOOOOOOOOM!), then the explosion
///     annihilates the map and kills EVERY player (100,000 damage each), ending the round the normal way.
///   - 99%: it ticks once, then sputters out: "DUD". No damage. The turn ends and the shot is used up.
///
/// Asset: Assets/Weapons/Special/Attacks/Doom Attack. The bomb and countdown are DoomBomb.
/// </summary>
[CreateAssetMenu(fileName = "Doom Attack", menuName = "Cockwar/Special Attacks/Doom")]
public class DoomAttack : SpecialAttack
{
    [Header("The roll")]
    [Tooltip("Chance the nuke actually goes off. 0.01 = 1%. Set to 1 to test the explosion.")]
    [SerializeField, Range(0f, 1f)] private float nukeChance = 0.01f;

    [Header("Nuke")]
    [Tooltip("Countdown length in seconds (shown as 10, 9 ... 1).")]
    [SerializeField, Min(1)] private int countdownSeconds = 10;

    [Tooltip("Damage given to every player in the match, wherever they are.")]
    [SerializeField] private int damage = 100000;

    [Tooltip("Radius of the crater, in world units. The map is 500 wide, so 320 wipes out nearly all of it.")]
    [SerializeField] private float craterRadius = 320f;

    [Tooltip("Explosion prefab for the blast sound, crater and debris (its damage is replaced; the kill is done for every player separately). Its own art is hidden while the Nuke Cloud frames are used.")]
    [SerializeField] private GameObject explosionPrefab;

    [Tooltip("Width of the mushroom cloud, in world units (the map is 500 wide).")]
    [SerializeField] private float cloudWidth = 560f;

    [Tooltip("Only used if the Nuke Cloud frames in WeaponEffectArt are missing: how much bigger than normal the prefab's own explosion is drawn instead.")]
    [SerializeField] private float explosionScale = 7f;

    [Tooltip("Seconds the mushroom cloud is held before the round moves on.")]
    [SerializeField] private float aftermathSeconds = 3f;

    [Header("Bomb")]
    [SerializeField] private float bombSize = 16f;

    [Tooltip("How far beside the caller the bomb lands.")]
    [SerializeField] private float landingOffset = 12f;

    [Tooltip("Optional art (empty = built-in pixel nuke).")]
    [SerializeField] private Sprite bombSprite;

    [Header("Sounds (optional)")]
    [SerializeField] private AudioClip landSound;
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip alarmLoop;
    [SerializeField] private AudioClip boomSound;
    [SerializeField] private AudioClip dudSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    public override SpecialActivation Activation => SpecialActivation.RightClick;

    public int CountdownSeconds => countdownSeconds;
    public int Damage => damage;
    public float CraterRadius => craterRadius;
    public float ExplosionScale => explosionScale;
    public float AftermathSeconds => aftermathSeconds;
    public float BombSize => bombSize;
    public float LandingOffset => landingOffset;
    public Sprite BombSprite => bombSprite != null ? bombSprite : WeaponArt.Nuke();
    public AudioClip AlarmLoop => alarmLoop;
    public float Volume => volume;

    public override Projectile Begin(AttackContext context)
    {
        bool goesOff = Random.value < nukeChance;
        Debug.Log("DOOOOOOOOOM: rolled " + (goesOff ? "THE NUKE." : "a dud (" + Mathf.RoundToInt((1f - nukeChance) * 100f) + "% chance)."));

        Transform caller = context.shooter != null ? context.shooter.transform : null;
        DoomBomb.Launch(this, caller, goesOff);
        return null;
    }

    public void Play(AudioClip clip) { if (clip != null) Sfx.Play(clip, volume); }
    public void PlayLand() => Play(landSound);
    public void PlayTick() => Play(tickSound);
    public void PlayBoom() => Play(boomSound);
    public void PlayDud() => Play(dudSound);

    /// <summary>The big one: crater, mushroom cloud, and every player in the match takes the damage.</summary>
    public void Detonate(Vector2 point)
    {
        // The Nuclear_explosion frames (WeaponEffectArt > Nuke Cloud), drawn across the whole map and
        // played over the aftermath. The cloud's base sits about a quarter of the way up each frame.
        WeaponEffectArt art = WeaponEffectArt.Get();
        Sprite[] cloud = art != null ? art.nukeCloud : null;
        float fps = WeaponEffectArt.Has(cloud) ? cloud.Length / Mathf.Max(1f, aftermathSeconds * 0.9f) : 1f;

        WeaponFx.Blast(explosionPrefab, point, explosion =>
        {
            explosion.maxDamage = 0;                 // everyone is killed below, not just whoever is near
            explosion.blastRadius = craterRadius;
            explosion.craterRadius = craterRadius;
            explosion.maxKnockback = 0f;
            explosion.shakeMultiplier = 0f;          // the shake is done by DoomBomb
            if (!WeaponEffectArt.Has(cloud)) explosion.transform.localScale *= explosionScale;
        }, cloud, cloudWidth, fps, 0.25f);

        foreach (CockroachMovement player in WeaponFx.AllPlayers())
            WeaponFx.Damage(player, damage);
    }

    protected override Sprite BuildIcon() => WeaponArt.Nuke();
    protected override Sprite BuildHeldSprite() => WeaponArt.Nuke();
}
