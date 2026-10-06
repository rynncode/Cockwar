using UnityEngine;

/// <summary>
/// The marker that follows the mouse while a click-to-target weapon (the Satelaser) is selected.
/// CockroachShooting calls ShowAt every frame it should be visible; on any frame nobody does, the
/// marker hides itself, so it can never be left behind after a turn ends or the weapon changes.
/// </summary>
public class TargetMarker : MonoBehaviour
{
    private static TargetMarker instance;

    private SpriteRenderer spriteRenderer;
    private int shownFrame = -1;

    public static void ShowAt(Sprite sprite, Vector2 position, float width) => ShowAt(sprite, position, width, Color.white);

    /// <summary>Same, tinted (the Teleporter shows red over spots it cannot use).</summary>
    public static void ShowAt(Sprite sprite, Vector2 position, float width, Color tint)
    {
        if (sprite == null) return;

        if (instance == null)
        {
            SpriteRenderer created = WeaponFx.MakeSprite("Target Marker", sprite, position, width, 60);
            instance = created.gameObject.AddComponent<TargetMarker>();
            instance.spriteRenderer = created;
        }

        instance.spriteRenderer.enabled = true;
        instance.spriteRenderer.sprite = sprite;
        instance.spriteRenderer.color = tint;
        instance.transform.position = position;

        // A slow spin and a little pulse so it reads as "locking on".
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 8f);
        WeaponFx.SetWidth(instance.spriteRenderer, width * pulse);
        instance.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * -40f);
        instance.shownFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        if (shownFrame < Time.frameCount - 1) spriteRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
