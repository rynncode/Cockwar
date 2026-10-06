using UnityEngine;

/// <summary>
/// One airstrike rocket: falls in an arc with a smoke trail, nose pointing where it is going,
/// and explodes on the first solid thing it touches (ground, a player, a crate).
/// Rockets that fall off the map are simply removed.
/// </summary>
public class AirstrikeRocket : AttackRunner
{
    private AirstrikeAttack attack;
    private Vector2 velocity;
    private float trailTimer;
    private float bottomY;

    public static void Launch(AirstrikeAttack attack, Vector2 position, Vector2 velocity)
    {
        SpriteRenderer art = WeaponFx.MakeSprite("Airstrike Rocket", attack.RocketSprite, position, attack.RocketSize, 44);
        AirstrikeRocket rocket = art.gameObject.AddComponent<AirstrikeRocket>();
        rocket.attack = attack;
        rocket.velocity = velocity;

        TerrainGenerator terrain = TerrainGenerator.Instance;
        rocket.bottomY = terrain != null ? terrain.WorldBounds.yMin - 40f : position.y - 400f;
    }

    private void Update()
    {
        velocity += Vector2.down * attack.RocketGravity * Time.deltaTime;

        Vector2 from = transform.position;
        Vector2 to = from + velocity * Time.deltaTime;

        if (WeaponFx.SolidHit(from, to, out RaycastHit2D hit))
        {
            attack.Impact(hit.point);
            Destroy(gameObject);
            return;
        }

        if (to.y < bottomY)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = to;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            // Smoke wisps (WeaponEffectArt > Smoke) left hanging in the air behind the rocket.
            trailTimer = 0.06f;
            Vector2 tail = to - velocity.normalized * attack.RocketSize * 0.6f;
            FxPuff.Smoke(tail, attack.RocketSize * 0.9f, 0.9f);
        }
    }
}
