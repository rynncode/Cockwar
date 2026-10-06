using UnityEngine;

/// <summary>
/// The Reneitor's meteor. First a red warning marker blinks on the recorded target spot, then a
/// flaming meteor drops straight down from the sky onto it, trailing fire and smoke. It explodes on
/// the first solid thing it touches (ground, a player) or when it reaches the target spot (if the
/// ground there has already been blown away). The turn waits for it (it is an AttackRunner).
/// Made in code by ReneitorAttack; nothing to set up.
/// </summary>
public class ReneitorMeteor : AttackRunner
{
    private ReneitorAttack attack;
    private Vector2 target;
    private SpriteRenderer warning;
    private SpriteRenderer meteor;
    private FxAnim flame;
    private AudioSource fallAudio;
    private float warningLeft;
    private float speed;
    private float trailTimer;

    public static void Launch(ReneitorAttack attack, Vector2 target)
    {
        GameObject go = new GameObject("Reneitor Meteor");
        ReneitorMeteor runner = go.AddComponent<ReneitorMeteor>();
        runner.attack = attack;
        runner.target = target;
        runner.warningLeft = attack.WarningSeconds;

        runner.warning = WeaponFx.MakeSprite("Meteor Warning", WeaponArt.AlienTarget(), target + Vector2.up * 2f,
                                             attack.MeteorSize * 1.2f, 55, go.transform);
        runner.warning.color = new Color(1f, 0.25f, 0.2f, 1f);
        WeaponFx.Look(target);
    }

    private void Update()
    {
        if (warningLeft > 0f)
        {
            warningLeft -= Time.deltaTime;
            warning.enabled = Mathf.Repeat(warningLeft, 0.2f) > 0.08f; // blink
            if (warningLeft <= 0f) SpawnMeteor();
            return;
        }

        if (meteor == null) return;

        speed += attack.FallAcceleration * Time.deltaTime;
        Vector2 from = meteor.transform.position;
        Vector2 to = from + Vector2.down * speed * Time.deltaTime;

        // Hit something solid on the way, or reached the target spot: boom.
        if (WeaponFx.SolidHit(from, to, out RaycastHit2D hit))
        {
            Land(hit.point);
            return;
        }

        if (to.y <= target.y)
        {
            Land(target);
            return;
        }

        meteor.transform.position = to;
        meteor.transform.Rotate(0f, 0f, 240f * Time.deltaTime);

        // The big flame rides on top of the rock: falling down, its flames stream up behind it.
        if (flame != null) flame.SetBottomAt(to - Vector2.up * attack.MeteorSize * 0.35f);

        // Smoke left behind in the air.
        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.06f;
            FxPuff.Smoke(to + Vector2.up * attack.MeteorSize * 1.2f, attack.MeteorSize * 0.7f, 1.4f);
            if (flame == null) WeaponFx.FireBurst(to + Vector2.up * attack.MeteorSize * 0.4f, attack.MeteorSize * 0.8f, 2);
        }
    }

    private void SpawnMeteor()
    {
        warning.enabled = true;
        speed = attack.FallSpeed;

        Vector2 start = target + Vector2.up * attack.SpawnHeight;
        meteor = WeaponFx.MakeSprite("Meteor", attack.MeteorSprite, start, attack.MeteorSize, 45, transform);
        WeaponFx.Follow(meteor.transform);

        // Looping Fire frames (WeaponEffectArt > Fire), behind the rock and twice as wide.
        WeaponEffectArt art = WeaponEffectArt.Get();
        if (art != null)
        {
            flame = FxAnim.Play(art.fire, start, attack.MeteorSize * 2.2f, 14f, true, Color.white, 44, true, transform);
            if (flame != null) flame.SetBottomAt(start - Vector2.up * attack.MeteorSize * 0.35f);
        }

        if (attack.FallSound != null)
        {
            fallAudio = gameObject.AddComponent<AudioSource>();
            fallAudio.clip = attack.FallSound;
            fallAudio.volume = attack.FallVolume * GameSettings.SfxVolume;
            fallAudio.outputAudioMixerGroup = Sfx.Output;
            fallAudio.Play();
        }
    }

    private void Land(Vector2 point)
    {
        attack.Impact(point);
        WeaponFx.Look(point);
        Destroy(gameObject);
    }
}
