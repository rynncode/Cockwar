using System.Collections;
using UnityEngine;

/// <summary>
/// One blowtorch burn: carves a tunnel step by step from the cockroach's body along the aim
/// direction, with flames and smoke at the tip. The terrain merges each frame's carves into one
/// rebuild, so a long tunnel stays cheap. While it burns, no other weapon can be fired.
/// </summary>
public class BlowtorchDig : AttackRunner
{
    public IEnumerator Run(BlowtorchAttack attack, Transform user, Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        Collider2D body = user != null ? user.GetComponent<Collider2D>() : null;
        Vector2 start = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;

        float step = attack.Radius * 0.6f;
        float carved = 0f;
        float puffTimer = 0f;

        for (float t = 0f; t < attack.Seconds; t += Time.deltaTime)
        {
            float reach = attack.Length * (t / attack.Seconds);

            // Carve every few units along the way.
            while (carved <= reach)
            {
                WeaponFx.Carve(start + direction * carved, attack.Radius);
                carved += step;
            }

            Vector2 tip = start + direction * reach;
            puffTimer -= Time.deltaTime;
            if (puffTimer <= 0f)
            {
                puffTimer = 0.08f;
                WeaponFx.FireBurst(tip, attack.Radius * 1.4f, 2);
                FxPuff.Smoke(tip, attack.Radius);
            }

            WeaponFx.Shake(0.3f, 0.1f);
            yield return null;
        }

        WeaponFx.Carve(start + direction * attack.Length, attack.Radius);
        Destroy(gameObject);
    }
}
