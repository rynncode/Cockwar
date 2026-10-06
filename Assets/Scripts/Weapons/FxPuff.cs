using UnityEngine;

/// <summary>
/// One short-lived effect sprite: a smoke puff, a spark, a flame lick. It drifts, grows (or shrinks)
/// and fades out, then removes itself. Used for trails and impact clouds by the special weapons.
/// </summary>
public class FxPuff : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color color;
    private Vector2 velocity;
    private float startSize;
    private float endSize;
    private float life;
    private float age;

    public static FxPuff Spawn(Sprite sprite, Vector2 position, float startSize, float endSize, Color color, float life, Vector2 velocity, int sortingOrder)
    {
        SpriteRenderer spriteRenderer = WeaponFx.MakeSprite("Fx", sprite, position, startSize, sortingOrder);
        spriteRenderer.color = color;
        spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f); // pixel-safe variety

        FxPuff puff = spriteRenderer.gameObject.AddComponent<FxPuff>();
        puff.spriteRenderer = spriteRenderer;
        puff.color = color;
        puff.velocity = velocity;
        puff.startSize = startSize;
        puff.endSize = endSize;
        puff.life = Mathf.Max(0.05f, life);
        return puff;
    }

    /// <summary>A smoke wisp that rises a little (the Smoke frames from WeaponEffectArt, or a grey blob without them).</summary>
    public static void Smoke(Vector2 position, float size, float life = 0.9f)
    {
        WeaponEffectArt art = WeaponEffectArt.Get();
        if (art != null && WeaponEffectArt.Has(art.smoke))
        {
            float fps = art.smoke.Length / Mathf.Max(0.2f, life);
            FxAnim smoke = FxAnim.Play(art.smoke, position, size * 1.4f, fps, false, new Color(1f, 1f, 1f, 0.9f), 22, true);
            smoke.WithDrift(new Vector2(Random.Range(-1f, 1f), Random.Range(1f, 3f))).WithFade(life * 0.4f);
            if (Random.value < 0.5f) smoke.Renderer.flipX = true;
            return;
        }

        float grey = Random.Range(0.45f, 0.7f);
        Spawn(WeaponArt.Puff(), position, size * 0.6f, size * 1.6f, new Color(grey, grey, grey, 0.85f), life,
              new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 4f)), 22);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(velocity * Time.deltaTime);
        WeaponFx.SetWidth(spriteRenderer, Mathf.Lerp(startSize, endSize, t));

        Color c = color;
        c.a = color.a * (1f - t * t);
        spriteRenderer.color = c;
    }
}
