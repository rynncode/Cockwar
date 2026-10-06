using UnityEngine;

/// <summary>
/// Plays a list of animation frames on a sprite in the world, then removes itself (or loops until
/// Stop is called). Used for every special-weapon effect drawn with the frames from
/// WeaponEffectArt: explosions, flames, smoke, lightning sparks.
/// Runs on Time.deltaTime, so it freezes with the pause menu.
/// </summary>
public class FxAnim : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float fps;
    private bool loop;
    private float time;
    private Vector2 drift;
    private float fadeOut;     // seconds of fade at the end (one-shot only)
    private float baseAlpha = 1f;

    public SpriteRenderer Renderer => spriteRenderer;

    /// <summary>
    /// Starts an animation. width = world width of one frame. bottomAnchored = the frame's bottom
    /// edge sits on position (flames, smoke, ground blasts); otherwise it is centred on it.
    /// </summary>
    public static FxAnim Play(Sprite[] frames, Vector2 position, float width, float fps, bool loop, Color tint,
                              int sortingOrder, bool bottomAnchored = false, Transform parent = null)
    {
        if (!WeaponEffectArt.Has(frames)) return null;

        SpriteRenderer spriteRenderer = WeaponFx.MakeSprite("Fx " + frames[0].name, frames[0], position, width, sortingOrder, parent);
        spriteRenderer.color = tint;

        FxAnim anim = spriteRenderer.gameObject.AddComponent<FxAnim>();
        anim.spriteRenderer = spriteRenderer;
        anim.frames = frames;
        anim.fps = Mathf.Max(1f, fps);
        anim.loop = loop;
        anim.baseAlpha = tint.a;

        if (bottomAnchored) anim.SetBottomAt(position);
        return anim;
    }

    /// <summary>Moves the sprite so the bottom edge of its frame is at this point.</summary>
    public void SetBottomAt(Vector2 point) => WeaponFx.PlaceBottom(spriteRenderer, point);

    /// <summary>Slowly moves the effect (rising smoke, drifting sparks).</summary>
    public FxAnim WithDrift(Vector2 velocity)
    {
        drift = velocity;
        return this;
    }

    /// <summary>Fades the last part of a one-shot animation.</summary>
    public FxAnim WithFade(float seconds)
    {
        fadeOut = seconds;
        return this;
    }

    public float Length => frames.Length / fps;

    /// <summary>Ends a looping animation now.</summary>
    public void Stop() => Destroy(gameObject);

    private void Update()
    {
        time += Time.deltaTime;
        int index = Mathf.FloorToInt(time * fps);

        if (!loop && index >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer.sprite = frames[index % frames.Length];
        transform.position += (Vector3)(drift * Time.deltaTime);

        if (!loop && fadeOut > 0f)
        {
            Color c = spriteRenderer.color;
            c.a = baseAlpha * Mathf.Clamp01((Length - time) / fadeOut);
            spriteRenderer.color = c;
        }
    }
}
