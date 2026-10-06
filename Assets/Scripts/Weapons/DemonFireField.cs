using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The burning surface from ALL DEMONS ARE HERE!.
///   0s: the pitchfork is raised over the caller, the screen flashes red, fire spreads over the surface.
///   1s, 2s ... 6s: every player standing on the burning surface takes 5 damage (through Health,
///   so the health bar and "-5" labels show it).
/// Who counts is checked again at every tick, so moving out of the fire (or dying) stops the damage.
/// The turn waits until the fire has gone out (it is an AttackRunner).
/// </summary>
public class DemonFireField : AttackRunner
{
    private class Flame
    {
        public SpriteRenderer sprite;
        public float x;
        public float delay;   // seconds until it lights (the fire spreads outward)
        public int frameOffset;
        public float groundY;  // top of the ground under it (follows craters)
    }

    private DemonFireAttack attack;
    private CockroachMovement caller;
    private readonly List<Flame> flames = new List<Flame>();
    private float minX;
    private float maxX;
    private float fade = 1f;

    public static void Launch(DemonFireAttack attack, CockroachMovement caller)
    {
        GameObject go = new GameObject("Demon Fire");
        DemonFireField field = go.AddComponent<DemonFireField>();
        field.attack = attack;
        field.caller = caller;
        field.StartCoroutine(field.Run());
    }

    private IEnumerator Run()
    {
        Vector2 origin = caller != null ? (Vector2)caller.transform.position : Vector2.zero;

        // 1. The pitchfork rises over the caller.
        attack.PlaySummon();
        ScreenFx.Flash(new Color(0.9f, 0.05f, 0f, 0.5f), 0.8f);
        ScreenFx.SetWarningTint(0.18f);
        WeaponFx.Shake(2f, 0.6f);
        WeaponFx.Look(origin);

        SpriteRenderer fork = WeaponFx.MakeSprite("Pitchfork", attack.HeldSprite, origin + Vector2.up * 10f, attack.PitchforkSize, 60, transform);
        fork.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // prongs up
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            fork.transform.position = origin + Vector2.up * (attack.PitchforkSize * 0.4f + attack.PitchforkSize * 0.3f * Mathf.Min(1f, t / 0.3f));
            fork.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.3f), Mathf.PingPong(t * 6f, 1f));
            yield return null;
        }
        Destroy(fork.gameObject);

        // 2. Light the surface.
        BuildFlames(origin.x);

        AudioSource loop = null;
        if (attack.FireLoop != null)
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.clip = attack.FireLoop;
            loop.loop = true;
            loop.volume = attack.Volume * GameSettings.SfxVolume;
            loop.outputAudioMixerGroup = Sfx.Output;
            loop.Play();
        }

        // 3. Damage ticks: 1s, 2s ... after the fire starts.
        for (int tick = 0; tick < attack.Ticks; tick++)
        {
            yield return new WaitForSeconds(attack.TickInterval);
            BurnPlayers();
        }

        // 4. Die down.
        for (fade = 1f; fade > 0f; fade -= Time.deltaTime / 0.6f)
        {
            if (loop != null) loop.volume = attack.Volume * GameSettings.SfxVolume * fade;
            yield return null;
        }

        ScreenFx.SetWarningTint(0f);
        Destroy(gameObject);
    }

    private void BuildFlames(float originX)
    {
        TerrainGenerator terrain = TerrainGenerator.Instance;
        Rect map = terrain != null ? terrain.WorldBounds : new Rect(originX - 250f, -110f, 500f, 220f);

        minX = attack.WholeMap ? map.xMin : Mathf.Max(map.xMin, originX - attack.AreaHalfWidth);
        maxX = attack.WholeMap ? map.xMax : Mathf.Min(map.xMax, originX + attack.AreaHalfWidth);

        for (float x = minX; x <= maxX; x += attack.FlameSpacing)
        {
            // Only where there is ground (no flames hanging over the acid or a gap).
            if (terrain == null || !terrain.TryFindSurface(x, 0f, 9999f, out Vector2 surface)) continue;

            Flame flame = new Flame
            {
                x = x,
                delay = Mathf.Abs(x - originX) / attack.SpreadSpeed,
                frameOffset = Random.Range(0, 6),
                groundY = surface.y,
            };
            flame.sprite = WeaponFx.MakeSprite("Flame", attack.FlameFrame(0), surface, attack.FlameSize, 19, transform);
            flame.sprite.color = attack.FlameTint;
            flame.sprite.enabled = false;
            flame.sprite.flipX = Random.value < 0.5f; // so neighbouring flames don't look identical
            flames.Add(flame);
        }
    }

    private void Update()
    {
        if (flames.Count == 0) return;

        float now = Time.time;
        foreach (Flame flame in flames)
        {
            if (flame.delay > 0f)
            {
                flame.delay -= Time.deltaTime;
                if (flame.delay > 0f) continue;
                FxPuff.Smoke(flame.sprite.transform.position, attack.FlameSize * 0.8f);
            }

            // Flicker through the frames and keep the flame sitting on the (possibly blasted) ground.
            flame.sprite.enabled = true;
            flame.sprite.sprite = attack.FlameFrame(Mathf.FloorToInt(now * 12f) + flame.frameOffset);
            float flicker = 1f + 0.1f * Mathf.Sin(now * 13f + flame.x);
            WeaponFx.SetWidth(flame.sprite, attack.FlameSize * flicker * Mathf.Max(0.05f, fade));
            flame.groundY = WeaponFx.SurfaceY(flame.x, flame.groundY);
            WeaponFx.PlaceBottom(flame.sprite, new Vector2(flame.x, flame.groundY - 0.5f));
        }

        // Smoke curling up off the burning ground now and then.
        if (fade > 0.5f && Random.value < 0.12f)
        {
            Flame source = flames[Random.Range(0, flames.Count)];
            if (source.delay <= 0f) FxPuff.Smoke(new Vector2(source.x, source.groundY + attack.FlameSize * 0.4f), attack.FlameSize * 0.8f, 1.6f);
        }

        // Occasional embers.
        if (Random.value < 0.3f)
        {
            Flame source = flames[Random.Range(0, flames.Count)];
            if (source.delay <= 0f)
                FxPuff.Spawn(WeaponArt.Puff(), source.sprite.transform.position + Vector3.up * attack.FlameSize * 0.6f,
                             attack.FlameSize * 0.25f, 0.2f, new Color(1f, 0.4f, 0.1f, 1f), 1f,
                             new Vector2(Random.Range(-3f, 3f), Random.Range(6f, 12f)), 20);
        }
    }

    /// <summary>One damage tick: every living player standing on the burning surface.</summary>
    private void BurnPlayers()
    {
        bool anyHit = false;

        foreach (CockroachMovement player in WeaponFx.LivingPlayers())
        {
            if (player == caller && !attack.HurtsCaller) continue;

            Vector2 feet = player.transform.position;
            if (feet.x < minX || feet.x > maxX) continue;

            // Standing on (or just above) the top surface = in the fire. Below it = under cover.
            float surface = WeaponFx.SurfaceY(feet.x, float.NegativeInfinity);
            if (float.IsNegativeInfinity(surface)) continue;

            float aboveSurface = feet.y - surface;
            if (aboveSurface < -3f || aboveSurface > attack.BurnHeight) continue;

            WeaponFx.Damage(player, attack.DamagePerTick);
            WeaponFx.FireBurst(WeaponFx.BodyCenter(player), attack.FlameSize * 0.6f, 6);
            anyHit = true;
        }

        if (anyHit) attack.PlayTick();
    }

    private void OnDestroy()
    {
        ScreenFx.SetWarningTint(0f);
    }
}
