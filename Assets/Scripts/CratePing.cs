using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows where the supply crates are for a few seconds at the start of each turn, so the player
/// does not have to zoom out to find them: a pulsing ring around every crate, and for a crate
/// that is off-screen a yellow arrow at the screen edge pointing toward it.
/// Made in code by CrateDropManager, which calls Begin. Seconds and on/off are in CrateSettings.
/// </summary>
public class CratePing : MonoBehaviour
{
    private const float RingPeriod = 1f;      // seconds per ring pulse
    private const float FadeSeconds = 0.5f;   // fade-out at the end
    private const float ArrowPixelSize = 5f;  // UI units per font pixel
    private const float EdgePadding = 70f;    // UI units in from the screen edge

    private class Marker
    {
        public SupplyCrate crate;
        public SpriteRenderer ring;
        public RectTransform arrow;
        public Image arrowImage;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private CrateSettings settings;
    private Camera cam;
    private Canvas canvas;
    private Sprite ringSprite;
    private float timeLeft;
    private float total;

    public void Init(CrateSettings crateSettings)
    {
        settings = crateSettings;

        GameObject canvasObject = new GameObject("Crate Ping Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>Starts (or restarts) the ping on the given crates.</summary>
    public void Begin(IReadOnlyList<SupplyCrate> crates)
    {
        Clear();
        if (settings == null || settings.pingSeconds <= 0f || crates.Count == 0) return;

        if (cam == null) cam = Camera.main;
        total = timeLeft = settings.pingSeconds;

        foreach (SupplyCrate crate in crates)
        {
            if (crate == null) continue;
            markers.Add(CreateMarker(crate));
        }
    }

    private Marker CreateMarker(SupplyCrate crate)
    {
        Marker marker = new Marker { crate = crate };

        GameObject ringObject = new GameObject("Crate Ping Ring");
        ringObject.transform.SetParent(transform, false);
        marker.ring = ringObject.AddComponent<SpriteRenderer>();
        marker.ring.sprite = RingSprite();
        marker.ring.sortingOrder = 30;

        GameObject arrowObject = new GameObject("Crate Ping Arrow");
        arrowObject.transform.SetParent(canvas.transform, false);
        marker.arrowImage = arrowObject.AddComponent<Image>();
        marker.arrowImage.sprite = PixelSprites.ArrowRight();
        marker.arrowImage.raycastTarget = false;
        marker.arrow = marker.arrowImage.rectTransform;
        marker.arrow.sizeDelta = marker.arrowImage.sprite.rect.size * ArrowPixelSize;
        // Anchored to a point in the screen's 0-1 space, so no pixel conversion is needed.
        marker.arrow.anchorMin = marker.arrow.anchorMax = Vector2.zero;

        return marker;
    }

    private void Clear()
    {
        foreach (Marker marker in markers)
        {
            if (marker.ring != null) Destroy(marker.ring.gameObject);
            if (marker.arrow != null) Destroy(marker.arrow.gameObject);
        }

        markers.Clear();
        timeLeft = 0f;
    }

    private void LateUpdate()
    {
        if (markers.Count == 0) return;
        if (GameMenu.IsPaused) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f || cam == null)
        {
            Clear();
            return;
        }

        float elapsed = total - timeLeft;
        float fade = Mathf.Clamp01(timeLeft / FadeSeconds);
        float pulse = (elapsed % RingPeriod) / RingPeriod;   // 0 -> 1, then repeats
        Color yellow = new Color(1f, 0.9f, 0.2f, 1f);

        foreach (Marker marker in markers)
        {
            if (marker.crate == null)
            {
                marker.ring.enabled = false;
                marker.arrowImage.enabled = false;
                continue;
            }

            Vector3 world = marker.crate.transform.position;
            Vector3 view = cam.WorldToViewportPoint(world);
            bool onScreen = view.z > 0f && view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f;

            // Ring: grows out from the crate and fades as it grows.
            float size = settings.crateSize * Mathf.Lerp(1.2f, 4f, pulse);
            marker.ring.enabled = true;
            marker.ring.transform.position = world;
            marker.ring.transform.localScale = new Vector3(size, size, 1f);
            Color ringColor = yellow;
            ringColor.a = (1f - pulse) * 0.9f * fade;
            marker.ring.color = ringColor;

            // Arrow: only while the crate is off-screen, on the screen edge.
            marker.arrowImage.enabled = !onScreen;
            if (onScreen) continue;

            if (view.z < 0f)
            {
                view.x = 1f - view.x;
                view.y = 1f - view.y;
            }

            Vector2 dir = ((Vector2)view - new Vector2(0.5f, 0.5f)).normalized;
            float padX = EdgePadding / 1920f;
            float padY = EdgePadding / 1080f;
            float scaleX = Mathf.Abs(dir.x) > 0.0001f ? (0.5f - padX) / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dir.y) > 0.0001f ? (0.5f - padY) / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 edge = new Vector2(0.5f, 0.5f) + dir * Mathf.Min(scaleX, scaleY);

            marker.arrow.anchorMin = marker.arrow.anchorMax = edge;
            marker.arrow.anchoredPosition = Vector2.zero;
            marker.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            // Gentle throb so it catches the eye.
            float throb = 1f + 0.15f * Mathf.Sin(elapsed * 10f);
            marker.arrow.localScale = new Vector3(throb, throb, 1f);
            Color arrowColor = yellow;
            arrowColor.a = fade;
            marker.arrowImage.color = arrowColor;
        }
    }

    private void OnDestroy()
    {
        if (ringSprite != null) Destroy(ringSprite.texture);
        if (ringSprite != null) Destroy(ringSprite);
    }

    /// <summary>A thin white ring, 1 world unit across at scale 1.</summary>
    private Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.08f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return ringSprite;
    }
}
