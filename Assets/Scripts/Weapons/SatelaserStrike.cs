using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Satelaser's UFO: flies in above the locked target, charges (a growing green glow), fires a
/// vertical laser that burns down through the ground to below the target, then zooms away.
/// Every living player inside the beam takes the damage once, wherever they are in the column
/// (the beam burns through cover). The turn waits for it (it is an AttackRunner).
/// Made in code by SatelaserAttack; nothing to set up.
/// </summary>
public class SatelaserStrike : AttackRunner
{
    private SatelaserAttack attack;
    private Vector2 target;
    private SpriteRenderer ufo;
    private SpriteRenderer marker;
    private SpriteRenderer glow;
    private SpriteRenderer core;
    private readonly HashSet<CockroachMovement> alreadyHit = new HashSet<CockroachMovement>();

    public static void Launch(SatelaserAttack attack, Vector2 target)
    {
        GameObject go = new GameObject("Satelaser Strike");
        SatelaserStrike strike = go.AddComponent<SatelaserStrike>();
        strike.attack = attack;
        strike.target = target;
        strike.StartCoroutine(strike.Run());
    }

    private IEnumerator Run()
    {
        // The locked target stays marked (it no longer follows the mouse).
        marker = WeaponFx.MakeSprite("Locked Target", attack.TargetMarker, target, attack.TargetMarkerSize, 55, transform);

        // Hover above the target, or above the ground over it if that is higher (target in a cave).
        float groundTop = WeaponFx.SurfaceY(target.x, target.y);
        Vector2 hover = new Vector2(target.x, Mathf.Max(target.y, groundTop) + attack.HoverHeight);
        Vector2 entry = hover + new Vector2(-80f, 90f);

        ufo = WeaponFx.MakeSprite("UFO", attack.UfoSprite, entry, attack.UfoSize, 46, transform);
        WeaponFx.Look((hover + target) * 0.5f);
        attack.PlaySound(0);

        // Fly in, easing to a stop over the target.
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, attack.EntrySeconds))
        {
            float eased = 1f - (1f - t) * (1f - t);
            ufo.transform.position = Vector2.Lerp(entry, hover, eased);
            ufo.transform.rotation = Quaternion.Euler(0f, 0f, (1f - eased) * -15f);
            yield return null;
        }
        ufo.transform.position = hover;
        ufo.transform.rotation = Quaternion.identity;

        // Charge: Lightning_spot sparks (WeaponEffectArt > Beam Spark) crackle and grow under the UFO.
        attack.PlaySound(1);
        WeaponEffectArt art = WeaponEffectArt.Get();
        bool lightning = art != null && WeaponEffectArt.Has(art.beamLoop);
        Vector2 emitter = hover + Vector2.down * attack.UfoSize * 0.15f;

        FxAnim chargeSpark = art != null ? FxAnim.Play(art.beamSpark, emitter, attack.BeamWidth, 14f, true, Color.white, 47, false, transform) : null;
        if (chargeSpark == null)
        {
            glow = WeaponFx.MakeSprite("Laser Glow", WeaponArt.Puff(), emitter, 1f, 47, transform);
            glow.color = attack.BeamGlow;
        }

        for (float t = 0f; t < attack.ChargeSeconds; t += Time.deltaTime)
        {
            float k = t / attack.ChargeSeconds;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 40f);
            if (chargeSpark != null) WeaponFx.SetWidth(chargeSpark.Renderer, attack.BeamWidth * (0.8f + 2.4f * k) * pulse);
            else WeaponFx.SetWidth(glow, attack.BeamWidth * (0.3f + 1.2f * k) * pulse);
            Bob(hover);
            yield return null;
        }

        if (chargeSpark != null) chargeSpark.Stop();
        if (glow != null) glow.enabled = false;

        // Fire: a lightning bolt (Beam Strike frames, then Beam Loop) from the UFO to below the
        // target, over a soft blue haze. It burns everything in its column on the way down.
        attack.PlaySound(2);
        ScreenFx.Flash(new Color(0.6f, 0.85f, 1f, 0.35f), 0.25f);
        float bottomY = target.y - attack.BeamDepth;

        GameObject beam = new GameObject("Laser Beam");
        beam.transform.SetParent(transform, false);
        SpriteRenderer haze = WeaponFx.MakeSprite("Haze", WeaponArt.Pixel(), emitter, 1f, 47, beam.transform);
        haze.color = attack.BeamGlow;
        core = WeaponFx.MakeSprite(lightning ? "Lightning" : "Core", lightning ? art.beamLoop[0] : WeaponArt.Pixel(), emitter, 1f, 48, beam.transform);
        core.color = lightning ? Color.white : attack.BeamCore;
        FxAnim tipSpark = lightning ? FxAnim.Play(art.beamSpark, emitter, attack.BeamWidth * 2.4f, 14f, true, Color.white, 49, false, transform) : null;

        float burnTime = attack.BeamSeconds * 0.4f;   // how long the tip takes to reach the bottom
        float lastCarveY = emitter.y;
        float puffTimer = 0f;
        bool hasStrike = lightning && WeaponEffectArt.Has(art.beamStrike);

        for (float t = 0f; t < attack.BeamSeconds; t += Time.deltaTime)
        {
            float tipY = Mathf.Lerp(emitter.y, bottomY, Mathf.Clamp01(t / burnTime));
            float flicker = 1f + 0.2f * Mathf.Sin(Time.time * 55f);
            StretchBeam(haze, emitter.y, tipY, attack.BeamWidth * 1.3f * flicker);

            if (lightning)
            {
                // The strike frames draw the bolt growing downward by themselves, so the sprite always
                // spans the whole beam; after that the crackling loop takes over.
                bool striking = hasStrike && t < burnTime;
                core.sprite = striking
                    ? art.beamStrike[Mathf.Min(art.beamStrike.Length - 1, Mathf.FloorToInt(t / burnTime * art.beamStrike.Length))]
                    : art.beamLoop[Mathf.FloorToInt(t * 14f) % art.beamLoop.Length];
                StretchBeam(core, emitter.y, striking ? bottomY : tipY, attack.BeamWidth * 2.6f);
            }
            else
            {
                StretchBeam(core, emitter.y, tipY, attack.BeamWidth * 0.35f * flicker);
            }

            if (tipSpark != null) tipSpark.transform.position = new Vector3(target.x, tipY, 0f);

            // Burn the ground in steps as the tip goes down (each carve rebuilds the terrain collider,
            // so it is done every few units instead of every frame).
            while (lastCarveY - tipY >= attack.HoleRadius)
            {
                lastCarveY -= attack.HoleRadius;
                WeaponFx.Carve(new Vector2(target.x, lastCarveY), attack.HoleRadius);
            }

            HitPlayersInBeam(tipY, emitter.y);
            WeaponFx.Shake(0.6f, 0.1f);

            puffTimer -= Time.deltaTime;
            if (puffTimer <= 0f)
            {
                puffTimer = 0.12f;
                FxPuff.Smoke(new Vector2(target.x + Random.Range(-1f, 1f) * attack.BeamWidth * 0.5f, tipY), attack.BeamWidth);
            }

            yield return null;
        }

        WeaponFx.Carve(new Vector2(target.x, bottomY), attack.HoleRadius);
        attack.Impact(new Vector2(target.x, bottomY));
        if (tipSpark != null) tipSpark.Stop();
        Destroy(marker.gameObject);

        // The bolt fades out (Beam End frames).
        if (lightning && WeaponEffectArt.Has(art.beamEnd))
        {
            haze.enabled = false;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                core.sprite = art.beamEnd[Mathf.Min(art.beamEnd.Length - 1, Mathf.FloorToInt(t / 0.3f * art.beamEnd.Length))];
                StretchBeam(core, emitter.y, bottomY, attack.BeamWidth * 2.6f);
                yield return null;
            }
        }
        Destroy(beam);

        // Zoom away.
        Vector2 exit = hover + new Vector2(90f, 160f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            ufo.transform.position = Vector2.Lerp(hover, exit, t * t);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void Bob(Vector2 hover)
    {
        ufo.transform.position = hover + Vector2.up * Mathf.Sin(Time.time * 3f) * 0.5f;
    }

    /// <summary>Stretches a sprite (any size) to fill the column from topY down to bottomY, this many units wide.</summary>
    private static void StretchBeam(SpriteRenderer part, float topY, float bottomY, float width)
    {
        Vector2 size = part.sprite != null ? (Vector2)part.sprite.bounds.size : Vector2.one;
        float height = Mathf.Max(0.01f, topY - bottomY);
        part.transform.position = new Vector3(part.transform.position.x, (topY + bottomY) * 0.5f, 0f);
        part.transform.localScale = new Vector3(width / Mathf.Max(0.0001f, size.x), height / Mathf.Max(0.0001f, size.y), 1f);
    }

    /// <summary>Every living player whose body touches the beam's column (between the tip and the UFO) is hit once.</summary>
    private void HitPlayersInBeam(float tipY, float topY)
    {
        float halfWidth = attack.BeamWidth * 0.5f;

        foreach (CockroachMovement player in WeaponFx.LivingPlayers())
        {
            if (alreadyHit.Contains(player)) continue;

            Collider2D body = player.GetComponent<Collider2D>();
            Bounds bounds = body != null ? body.bounds : new Bounds(player.transform.position, Vector3.one);

            bool insideX = bounds.max.x >= target.x - halfWidth && bounds.min.x <= target.x + halfWidth;
            bool insideY = bounds.max.y >= tipY && bounds.min.y <= topY;
            if (!insideX || !insideY) continue;

            alreadyHit.Add(player);
            WeaponFx.Damage(player, attack.Damage);
        }
    }
}
