using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One airstrike: the remote pops up over the caller and its red button is pressed (blinking),
/// then the bomber flies across the whole map, releasing a rocket over each chosen spot.
/// The spots are picked when the strike starts: some near random players (the caller included),
/// the rest anywhere, each with a random miss, so it is never the same twice.
/// The turn waits for the bomber and every rocket (all are AttackRunners).
/// </summary>
public class AirstrikeRun : AttackRunner
{
    private AirstrikeAttack attack;
    private Transform caller;

    public static void Launch(AirstrikeAttack attack, Transform caller)
    {
        GameObject go = new GameObject("Airstrike");
        AirstrikeRun run = go.AddComponent<AirstrikeRun>();
        run.attack = attack;
        run.caller = caller;
        run.StartCoroutine(run.Run());
    }

    private IEnumerator Run()
    {
        yield return PressButton();

        TerrainGenerator terrain = TerrainGenerator.Instance;
        Rect map = terrain != null ? terrain.WorldBounds : new Rect(-250f, -110f, 500f, 220f);
        float height = (terrain != null ? terrain.HighestGroundY() : map.yMax) + attack.PlaneHeightAboveGround;

        // Fly from a random side to the other, starting and ending off the map.
        float direction = Random.value < 0.5f ? 1f : -1f;
        float startX = direction > 0f ? map.xMin - 60f : map.xMax + 60f;
        float endX = direction > 0f ? map.xMax + 60f : map.xMin - 60f;

        // Where the rockets are released, in flight order.
        List<float> dropXs = PickDropSpots(map);
        dropXs.Sort();
        if (direction < 0f) dropXs.Reverse();

        SpriteRenderer plane = WeaponFx.MakeSprite("Airstrike Bomber", attack.PlaneSprite, new Vector2(startX, height), attack.PlaneSize, 46, transform);
        plane.color = new Color(0.75f, 0.72f, 0.7f, 1f); // a darker, meaner look than the supply plane
        plane.flipX = direction < 0f;
        WeaponFx.Follow(plane.transform);

        AudioSource engine = null;
        if (attack.PlaneSound != null)
        {
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = attack.PlaneSound;
            engine.volume = attack.Volume * GameSettings.SfxVolume;
            engine.outputAudioMixerGroup = Sfx.Output;
            engine.Play();
        }

        int next = 0;
        Vector2 planeVelocity = new Vector2(direction * attack.PlaneSpeed, 0f);

        while ((plane.transform.position.x - endX) * direction < 0f)
        {
            Vector3 position = plane.transform.position;
            position.x += planeVelocity.x * Time.deltaTime;
            position.y = height + Mathf.Sin(Time.time * 2f) * 0.8f;
            plane.transform.position = position;

            // Release over each spot. The rocket keeps some of the plane's speed, so it lands a bit
            // past the spot: part of the chaos.
            while (next < dropXs.Count && (position.x - dropXs[next]) * direction >= 0f)
            {
                Vector2 release = (Vector2)position + Vector2.down * attack.PlaneSize * 0.2f;
                AirstrikeRocket.Launch(attack, release, new Vector2(planeVelocity.x * 0.25f + Random.Range(-8f, 8f), -10f));
                attack.PlayRocketLaunch();
                next++;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>The remote pops up above the caller, then the button is pressed and blinks.</summary>
    private IEnumerator PressButton()
    {
        if (caller == null) yield break;

        float size = attack.RocketSize * 2.5f;
        SpriteRenderer remote = WeaponFx.MakeSprite("Airstrike Remote", WeaponArt.Remote(false),
                                                    (Vector2)caller.position + Vector2.up * 14f, size, 60, transform);
        WeaponFx.Look(caller.position);

        float seconds = Mathf.Max(0.2f, attack.ButtonSeconds);
        bool pressed = false;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float pop = Mathf.Min(1f, t / 0.15f);
            remote.transform.position = (Vector2)caller.position + Vector2.up * (10f + 6f * pop);
            WeaponFx.SetWidth(remote, size * (0.5f + 0.5f * pop));

            // Half way through: CLICK. Then the button blinks.
            if (!pressed && t > seconds * 0.4f)
            {
                pressed = true;
                attack.PlayButton();
            }
            remote.sprite = pressed && Mathf.Repeat(t, 0.16f) < 0.08f ? WeaponArt.Remote(true) : WeaponArt.Remote(false);
            yield return null;
        }

        Destroy(remote.gameObject);
    }

    private List<float> PickDropSpots(Rect map)
    {
        List<CockroachMovement> targets = new List<CockroachMovement>(WeaponFx.LivingPlayers());
        List<float> spots = new List<float>();

        for (int i = 0; i < attack.RocketCount; i++)
        {
            float x;
            if (targets.Count > 0 && Random.value < attack.AimedAtPlayersShare)
                x = targets[Random.Range(0, targets.Count)].transform.position.x + Random.Range(-attack.Scatter, attack.Scatter);
            else
                x = Random.Range(map.xMin + 20f, map.xMax - 20f);

            spots.Add(Mathf.Clamp(x, map.xMin, map.xMax));
        }

        return spots;
    }
}
