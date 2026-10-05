using TMPro;
using UnityEngine;

/// <summary>
/// Countdown visuals for a fused projectile (the grenade).
/// Projectile adds this component by itself when a fuse starts burning, so nothing
/// needs to be set up on the prefab. Turn it off with "Show Fuse Visuals" on the Projectile.
///
/// The timer badge (always upright, floating above the grenade):
///  - a dark disc with the seconds left as a big number (it pops each time the number changes);
///  - a ring around the disc that unwinds clockwise as the fuse burns;
///  - the number and ring shift white, then yellow, then red as time runs out.
///
/// The pulse: the grenade's own sprite flashes toward a warning color, faster and
/// stronger as the fuse gets shorter.
///
/// The badge is a separate, unparented object so it does not spin with the grenade, and its
/// size follows the camera zoom so it stays readable from far away.
/// </summary>
[RequireComponent(typeof(Projectile))]
public class FuseVisual : MonoBehaviour
{
    private const int SortingOrder = 70;
    private const int RingSegments = 48;
    private const float PulseSlowHz = 2f;    // pulses per second when the fuse has just started
    private const float PulseFastHz = 10f;   // pulses per second right before the blast

    private Projectile projectile;
    private SpriteRenderer bodyRenderer;
    private Color bodyBaseColor = Color.white;
    private Collider2D bodyCollider;

    private GameObject badgeRoot;
    private SpriteRenderer disc;
    private LineRenderer ring;
    private TextMeshPro label;

    private Texture2D discTexture;
    private Sprite discSprite;
    private Material ringMaterial;

    private float pulsePhase;
    private int shownSeconds = -1;
    private float numberPunch;   // 1 right after the number changes, fades to 0

    private void Awake()
    {
        projectile = GetComponent<Projectile>();
        bodyCollider = GetComponent<Collider2D>();

        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null) bodyBaseColor = bodyRenderer.color;

        BuildBadge();
    }

    private void BuildBadge()
    {
        badgeRoot = new GameObject("Fuse Timer");

        // Dark disc behind the number, so it can be read against any background.
        GameObject discObject = new GameObject("Disc");
        discObject.transform.SetParent(badgeRoot.transform, false);
        disc = discObject.AddComponent<SpriteRenderer>();
        disc.sprite = MakeDiscSprite();
        disc.color = new Color(0f, 0f, 0f, 0.7f);
        disc.sortingOrder = SortingOrder;

        // Ring that unwinds as time runs out.
        GameObject ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(badgeRoot.transform, false);
        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.numCapVertices = 2;
        ring.sortingOrder = SortingOrder + 1;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            ringMaterial = new Material(shader);
            ring.sharedMaterial = ringMaterial;
        }

        // The number.
        GameObject numberObject = new GameObject("Number");
        numberObject.transform.SetParent(badgeRoot.transform, false);
        label = numberObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.sortingOrder = SortingOrder + 2;
    }

    /// <summary>A soft-edged white circle, 1 world unit wide at scale 1.</summary>
    private Sprite MakeDiscSprite()
    {
        const int size = 64;
        discTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        discTexture.filterMode = FilterMode.Bilinear;

        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                float alpha = Mathf.Clamp01(radius - distance);   // 1 inside, fades over the last pixel
                discTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        discTexture.Apply();

        discSprite = Sprite.Create(discTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return discSprite;
    }

    private void LateUpdate()
    {
        if (projectile == null || badgeRoot == null) return;

        float total = Mathf.Max(0.01f, projectile.FuseTotal);
        float left = Mathf.Clamp(projectile.FuseTimeLeft, 0f, total);
        float fraction = left / total;   // 1 at the start, 0 at the blast

        Color timerColor = TimerColor(fraction);

        UpdateBadge(left, fraction, timerColor);
        UpdatePulse(fraction);
    }

    // ---------------------------------------------------------------------
    // Timer badge
    // ---------------------------------------------------------------------

    private void UpdateBadge(float secondsLeft, float fraction, Color color)
    {
        // Size follows the camera, so the badge looks the same at any zoom.
        Camera cam = Camera.main;
        float halfHeight = cam != null ? cam.orthographicSize : 20f;
        float radius = halfHeight * projectile.fuseBadgeSize;

        // Float above the grenade: above its body, by the badge's own size.
        float bodySize = bodyCollider != null ? Mathf.Max(bodyCollider.bounds.size.x, bodyCollider.bounds.size.y) : radius;
        Vector3 center = transform.position + Vector3.up * (bodySize * 0.5f + radius * 1.6f);
        badgeRoot.transform.position = center;
        badgeRoot.transform.rotation = Quaternion.identity;   // never turns with the grenade

        disc.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        // Ring: an arc that shrinks clockwise from the top.
        if (fraction <= 0.001f)
        {
            ring.enabled = false;
        }
        else
        {
            ring.enabled = true;
            ring.startColor = color;
            ring.endColor = color;
            ring.startWidth = ring.endWidth = radius * 0.18f;

            int points = Mathf.Max(2, Mathf.CeilToInt(RingSegments * fraction) + 1);
            ring.positionCount = points;
            float ringRadius = radius * 0.9f;
            for (int i = 0; i < points; i++)
            {
                float angle = (90f - 360f * fraction * i / (points - 1)) * Mathf.Deg2Rad;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius);
            }
        }

        // Number: whole seconds left, popping slightly each time it changes.
        int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            label.text = seconds.ToString();
            numberPunch = 1f;
        }
        numberPunch = Mathf.MoveTowards(numberPunch, 0f, Time.deltaTime / 0.25f);

        // In TextMeshPro 3D text, font size 10 is about 1 world unit tall.
        label.fontSize = radius * 11f * (1f + 0.35f * numberPunch);
        label.color = color;
        label.rectTransform.sizeDelta = new Vector2(radius * 4f, radius * 2f);
        label.rectTransform.position = center;
    }

    /// <summary>White when the fuse is fresh, yellow in the middle, red at the end.</summary>
    private static Color TimerColor(float fraction)
    {
        Color yellow = new Color(1f, 0.9f, 0.2f, 1f);
        Color red = new Color(1f, 0.2f, 0.15f, 1f);

        return fraction > 0.5f
            ? Color.Lerp(yellow, Color.white, (fraction - 0.5f) * 2f)
            : Color.Lerp(red, yellow, fraction * 2f);
    }

    // ---------------------------------------------------------------------
    // Pulse color on the grenade itself
    // ---------------------------------------------------------------------

    private void UpdatePulse(float fraction)
    {
        if (bodyRenderer == null) return;

        float progress = 1f - fraction;                                  // 0 at the start, 1 at the blast
        float hz = Mathf.Lerp(PulseSlowHz, PulseFastHz, progress * progress);   // speeds up near the end
        pulsePhase += hz * 2f * Mathf.PI * Time.deltaTime;

        float wave = 0.5f + 0.5f * Mathf.Sin(pulsePhase);                // 0..1
        float strength = Mathf.Lerp(0.25f, 1f, progress);                // pulses harder near the end

        bodyRenderer.color = Color.Lerp(bodyBaseColor, projectile.fusePulseColor, wave * strength);
    }

    private void OnDestroy()
    {
        // The badge is a separate object, so it must be cleaned up with the grenade.
        if (badgeRoot != null) Destroy(badgeRoot);
        if (ringMaterial != null) Destroy(ringMaterial);
        if (discSprite != null) Destroy(discSprite);
        if (discTexture != null) Destroy(discTexture);
    }
}
