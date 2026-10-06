using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen effects for the special weapons: a colour flash (the nuke's white-out, the laser's
/// glare), a pulsing red warning tint, and big pixel-font text in the middle of the screen (the Doom
/// countdown). Builds its own canvas the first time it is used, above the HUD and below the pause menu.
/// </summary>
public class ScreenFx : MonoBehaviour
{
    private static ScreenFx instance;

    private Image flash;
    private Image tint;
    private Image text;
    private Color flashColor;
    private float flashTime;
    private float flashLength;
    private float tintAlpha;
    private float textPop;
    private float textPixelSize;

    private static ScreenFx Get()
    {
        if (instance != null) return instance;

        GameObject canvasObject = new GameObject("Screen Fx");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        instance = canvasObject.AddComponent<ScreenFx>();
        instance.tint = instance.FullScreenImage("Tint");
        instance.flash = instance.FullScreenImage("Flash");

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        instance.text = textObject.AddComponent<Image>();
        instance.text.raycastTarget = false;
        instance.text.enabled = false;
        return instance;
    }

    private Image FullScreenImage(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    /// <summary>Fills the screen with a colour that fades out over the given seconds.</summary>
    public static void Flash(Color color, float seconds)
    {
        ScreenFx fx = Get();
        fx.flashColor = color;
        fx.flashLength = Mathf.Max(0.05f, seconds);
        fx.flashTime = fx.flashLength;
    }

    /// <summary>Red warning tint, 0 = off. Pulses gently while on.</summary>
    public static void SetWarningTint(float alpha)
    {
        // Turning it off never needs to create the overlay (also safe while a scene is unloading).
        if (alpha <= 0f && instance == null) return;
        Get().tintAlpha = Mathf.Clamp01(alpha);
    }

    /// <summary>Big pixel-font text in the middle of the screen. Each new text pops in.</summary>
    public static void ShowText(string message, Color color, float pixelSize = 16f)
    {
        ScreenFx fx = Get();
        fx.text.enabled = true;
        fx.text.sprite = PixelSprites.Text(message);
        fx.text.color = color;
        fx.textPixelSize = pixelSize;
        fx.textPop = 1f;
    }

    public static void HideText()
    {
        if (instance != null) instance.text.enabled = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (flashTime > 0f)
        {
            flashTime -= dt;
            Color c = flashColor;
            c.a *= Mathf.Clamp01(flashTime / flashLength);
            flash.color = c;
        }
        else if (flash.color.a > 0f)
        {
            flash.color = Color.clear;
        }

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
        tint.color = new Color(0.8f, 0f, 0f, tintAlpha * pulse);

        if (text.enabled && text.sprite != null)
        {
            textPop = Mathf.MoveTowards(textPop, 0f, dt / 0.25f);
            text.rectTransform.sizeDelta = text.sprite.rect.size * textPixelSize * (1f + 0.4f * textPop);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
