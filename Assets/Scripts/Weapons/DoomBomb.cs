using System.Collections;
using UnityEngine;

/// <summary>
/// The DOOOOOOOOOM! bomb. Drops from the sky beside the caller and thuds into the ground, then:
///   nuke: the camera pulls all the way out, the screen pulses red, the countdown runs
///         10, 9 ... 1 in huge pixel numbers, "DOOOOOOOOOM!", a white-out, and DoomAttack.Detonate.
///   dud:  "10" appears, the bomb splutters, "DUD" and a sad puff of smoke.
/// Nobody can act during it: the turn has already ended when it was fired, and the TurnManager waits
/// for this runner before moving on (or ending the match, if everyone died).
/// </summary>
public class DoomBomb : AttackRunner
{
    private static readonly Color CountdownStart = new Color(1f, 0.9f, 0.3f, 1f);
    private static readonly Color CountdownEnd = new Color(1f, 0.15f, 0.1f, 1f);

    private DoomAttack attack;
    private Transform caller;
    private bool goesOff;
    private SpriteRenderer bomb;

    public static void Launch(DoomAttack attack, Transform caller, bool goesOff)
    {
        GameObject go = new GameObject("DOOOOOOOOOM Bomb");
        DoomBomb doom = go.AddComponent<DoomBomb>();
        doom.attack = attack;
        doom.caller = caller;
        doom.goesOff = goesOff;
        doom.StartCoroutine(doom.Run());
    }

    private IEnumerator Run()
    {
        yield return DropBomb();

        if (goesOff) yield return Nuke();
        else yield return Dud();

        ScreenFx.HideText();
        ScreenFx.SetWarningTint(0f);
        Destroy(gameObject);
    }

    private IEnumerator DropBomb()
    {
        Vector2 origin = caller != null ? (Vector2)caller.position : Vector2.zero;
        float side = caller != null && caller.lossyScale.x < 0f ? -1f : 1f;
        float x = origin.x + side * attack.LandingOffset;

        Vector2 position = new Vector2(x, origin.y + 90f);
        bomb = WeaponFx.MakeSprite("Nuke", attack.BombSprite, position, attack.BombSize, 30, transform);
        bomb.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // nose down while falling
        WeaponFx.Look(new Vector2(x, origin.y));

        float speed = 30f;
        for (int guard = 0; guard < 2000; guard++)
        {
            speed += 140f * Time.deltaTime;
            Vector2 next = position + Vector2.down * speed * Time.deltaTime;

            if (WeaponFx.SolidHit(position, next, out RaycastHit2D hit))
            {
                position = hit.point + Vector2.up * attack.BombSize * 0.25f;
                break;
            }

            position = next;
            bomb.transform.position = position;
            if (position.y < origin.y - 200f) break; // fell into a hole: just stop
            yield return null;
        }

        bomb.transform.position = position;
        bomb.transform.rotation = Quaternion.Euler(0f, 0f, side < 0f ? 180f : 0f); // lies on its side
        bomb.flipY = side < 0f;
        attack.PlayLand();
        WeaponFx.Shake(3f, 0.4f);
        for (int i = 0; i < 6; i++) FxPuff.Smoke(position, attack.BombSize * 0.5f);

        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator Nuke()
    {
        Vector2 point = bomb.transform.position;

        // Pull the camera all the way out so everyone sees what is coming.
        CameraController cam = CameraController.Instance;
        if (cam != null)
        {
            cam.SetZoom(cam.maxZoom);
            cam.SetTargetPosition(point);
        }

        ScreenFx.SetWarningTint(0.3f);
        AudioSource alarm = null;
        if (attack.AlarmLoop != null)
        {
            alarm = gameObject.AddComponent<AudioSource>();
            alarm.clip = attack.AlarmLoop;
            alarm.loop = true;
            alarm.volume = attack.Volume * GameSettings.SfxVolume;
            alarm.outputAudioMixerGroup = Sfx.Output;
            alarm.Play();
        }

        for (int n = attack.CountdownSeconds; n >= 1; n--)
        {
            float k = 1f - (n - 1f) / Mathf.Max(1f, attack.CountdownSeconds - 1f);
            ScreenFx.ShowText(n.ToString(), Color.Lerp(CountdownStart, CountdownEnd, k), 22f);
            attack.PlayTick();
            WeaponFx.Shake(0.5f + 2.5f * k, 0.3f);

            // The bomb blinks faster as it gets closer.
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                bomb.color = Mathf.Repeat(t * (2f + 6f * k), 1f) < 0.5f ? Color.white : new Color(1f, 0.4f, 0.4f);
                yield return null;
            }
        }

        if (alarm != null) alarm.Stop();
        ScreenFx.SetWarningTint(0f);
        ScreenFx.ShowText("DOOOOOOOOOM!", Color.white, 18f);
        ScreenFx.Flash(Color.white, 2.5f);
        WeaponFx.Shake(12f, 2.5f);
        attack.PlayBoom();

        bomb.enabled = false;
        attack.Detonate(point);

        // Fire everywhere for good measure.
        for (int i = 0; i < 30; i++)
            WeaponFx.FireBurst(point + Random.insideUnitCircle * attack.CraterRadius * 0.5f, 20f, 3);

        yield return new WaitForSeconds(1.2f);
        ScreenFx.HideText();
        yield return new WaitForSeconds(Mathf.Max(0f, attack.AftermathSeconds - 1.2f));
    }

    private IEnumerator Dud()
    {
        Vector2 point = bomb.transform.position;

        // A moment of panic...
        ScreenFx.ShowText(attack.CountdownSeconds.ToString(), CountdownStart, 22f);
        attack.PlayTick();
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            bomb.color = Mathf.Repeat(t * 2f, 1f) < 0.5f ? Color.white : new Color(1f, 0.4f, 0.4f);
            yield return null;
        }

        // ...then it splutters out.
        bomb.color = Color.white;
        attack.PlayDud();
        ScreenFx.ShowText("DUD", new Color(0.7f, 0.7f, 0.7f, 1f), 22f);
        for (int i = 0; i < 3; i++)
        {
            FxPuff.Smoke(point + Vector2.up * attack.BombSize * 0.3f, attack.BombSize * 0.5f, 1.4f);
            bomb.transform.rotation *= Quaternion.Euler(0f, 0f, i % 2 == 0 ? 6f : -6f);
            yield return new WaitForSeconds(0.25f);
        }

        yield return new WaitForSeconds(1f);
        ScreenFx.HideText();

        // The dud fades away.
        for (float a = 1f; a > 0f; a -= Time.deltaTime / 0.5f)
        {
            bomb.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        ScreenFx.SetWarningTint(0f);
        ScreenFx.HideText();
    }
}
