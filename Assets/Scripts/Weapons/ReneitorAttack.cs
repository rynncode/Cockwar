using UnityEngine;

/// <summary>
/// RENEITOR: fire a basketball like a normal charged shot. If it hits a player's body, a huge
/// flaming meteor falls on the spot where they were hit (40 damage, big crater). If it hits
/// anything else it just bounces off in a puff of dust, and no meteor comes.
///
/// Asset: Assets/Weapons/Special/Attacks/Reneitor Attack. The WeaponData (Assets/Weapons/Special/Reneitor)
/// sets the throw power and ammo like any other weapon. The meteor itself is ReneitorMeteor.
/// </summary>
[CreateAssetMenu(fileName = "Reneitor Attack", menuName = "Cockwar/Special Attacks/Reneitor")]
public class ReneitorAttack : SpecialAttack
{
    [Header("Basketball")]
    [Tooltip("Width of the basketball, in world units.")]
    [SerializeField] private float ballSize = 3f;

    [Tooltip("Gravity on the basketball. Normal weapons use about 3.")]
    [SerializeField] private float ballGravity = 3f;

    [Tooltip("Optional art for the ball in flight (empty = built-in pixel basketball).")]
    [SerializeField] private Sprite ballSprite;

    [Header("Meteor")]
    [Tooltip("How long the red warning marker shows on the target before the meteor appears.")]
    [SerializeField] private float warningSeconds = 0.7f;

    [Tooltip("How far above the target the meteor appears.")]
    [SerializeField] private float spawnHeight = 140f;

    [Tooltip("Starting fall speed (world units per second). It speeds up as it falls.")]
    [SerializeField] private float fallSpeed = 70f;

    [Tooltip("How quickly the fall speeds up.")]
    [SerializeField] private float fallAcceleration = 160f;

    [Tooltip("Width of the meteor, in world units.")]
    [SerializeField] private float meteorSize = 16f;

    [Tooltip("Optional art for the meteor (empty = built-in pixel meteor).")]
    [SerializeField] private Sprite meteorSprite;

    [Tooltip("Optional whistle/roar played while the meteor falls.")]
    [SerializeField] private AudioClip fallSound;
    [SerializeField, Range(0f, 1f)] private float fallVolume = 0.8f;

    [Header("Impact")]
    [Tooltip("Explosion prefab used for the look and sound of the impact (its damage numbers are replaced by the ones below).")]
    [SerializeField] private GameObject explosionPrefab;

    [Tooltip("Damage at the centre of the impact.")]
    [SerializeField] private int damage = 40;

    [Tooltip("Damage at the edge of the blast, as a fraction of Damage.")]
    [SerializeField, Range(0f, 1f)] private float edgeDamageFraction = 0.5f;

    [Tooltip("How far the blast reaches (damage and knockback).")]
    [SerializeField] private float blastRadius = 22f;

    [Tooltip("Radius of the crater carved into the ground.")]
    [SerializeField] private float craterRadius = 24f;

    [Tooltip("Push strength at the centre of the blast.")]
    [SerializeField] private float knockback = 80f;

    public override SpecialActivation Activation => SpecialActivation.ChargeShot;

    public float WarningSeconds => warningSeconds;
    public float SpawnHeight => spawnHeight;
    public float FallSpeed => fallSpeed;
    public float FallAcceleration => fallAcceleration;
    public float MeteorSize => meteorSize;
    public Sprite MeteorSprite => meteorSprite != null ? meteorSprite : WeaponArt.Meteor();
    public AudioClip FallSound => fallSound;
    public float FallVolume => fallVolume;

    public override Projectile Begin(AttackContext context)
    {
        // The basketball is an ordinary Projectile (so the camera follows it and the turn waits
        // for it to land), with no explosion of its own.
        SpriteRenderer art = WeaponFx.MakeSprite("Reneitor Basketball", ballSprite != null ? ballSprite : WeaponArt.Basketball(),
                                                 context.origin, ballSize, 15);
        GameObject ball = art.gameObject;

        Rigidbody2D body = ball.AddComponent<Rigidbody2D>();
        body.gravityScale = ballGravity;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D circle = ball.AddComponent<CircleCollider2D>();
        circle.radius = art.sprite.bounds.extents.x * 0.9f;

        Projectile projectile = ball.AddComponent<Projectile>();
        projectile.explodeOnImpact = true;
        projectile.explosionPrefab = null;
        projectile.rotation = ProjectileRotation.Spin;
        projectile.referenceSize = ballSize;

        CockroachMovement shooter = context.shooter != null ? context.shooter.GetComponent<CockroachMovement>() : null;
        projectile.OnImpact += (hitCollider, point) => HandleBallImpact(hitCollider, point, shooter);
        projectile.Launch(context.direction, context.power);
        return projectile;
    }

    private void HandleBallImpact(Collider2D hitCollider, Vector2 point, CockroachMovement shooter)
    {
        CockroachMovement victim = hitCollider != null ? hitCollider.GetComponentInParent<CockroachMovement>() : null;

        if (victim == null || !WeaponFx.IsAlive(victim))
        {
            // Missed: a sad bounce of dust, no meteor.
            for (int i = 0; i < 4; i++) FxPuff.Smoke(point, ballSize);
            return;
        }

        // Hit a player: record where they were standing, and send the meteor there.
        Vector2 target = new Vector2(WeaponFx.BodyCenter(victim).x, victim.transform.position.y);
        ReneitorMeteor.Launch(this, target);
    }

    /// <summary>Called by the meteor when it lands.</summary>
    public void Impact(Vector2 point)
    {
        // The big fiery Circle_explosion (WeaponEffectArt > Meteor Impact). The frames' blast fills
        // about 45% of the image, so the art is drawn ~4.5x the blast radius to match the real blast.
        WeaponEffectArt art = WeaponEffectArt.Get();
        WeaponFx.Blast(explosionPrefab, point, explosion =>
        {
            explosion.maxDamage = damage;
            explosion.minDamageFraction = edgeDamageFraction;
            explosion.blastRadius = blastRadius;
            explosion.craterRadius = craterRadius;
            explosion.maxKnockback = knockback;
        }, art != null ? art.meteorImpact : null, blastRadius * 4.5f, 14f);

        WeaponFx.FireBurst(point, meteorSize, 8);
        for (int i = 0; i < 4; i++) FxPuff.Smoke(point + Random.insideUnitCircle * blastRadius * 0.6f, meteorSize * 0.8f, 1.6f);
    }

    protected override Sprite BuildIcon() => WeaponArt.Basketball();
    protected override Sprite BuildHeldSprite() => WeaponArt.Basketball();
}
