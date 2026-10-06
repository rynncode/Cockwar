using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Movie-style rolling credits. Put this on the "Panel" under your Credits Panel.
/// The credits build themselves at runtime, scroll upward, and finish when they
/// reach the end OR when the player presses any key. Edit the text in the Inspector.
/// </summary>
public class CreditsScroller : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string role;                       // leave empty for names-only lines
        public List<string> names = new List<string>();
    }

    [System.Serializable]
    public class Section
    {
        public string heading;
        public List<Entry> entries = new List<Entry>();
    }

    [Header("Title block")]
    public string gameTitle = "COCKWARS";
    public string subtitle = "A Game By";
    public string studio = "UM STUDENTS :)))";

    [Header("Credits")]
    public List<Section> sections = DefaultSections();
    [TextArea] public string closingMessage = "To everyone who played and supported our game!";
    [TextArea] public string footer = "\u00A9 2026 Cockroach Wars\nAll Rights Reserved.";

    [Header("Scrolling")]
    [Tooltip("Pixels per second (at 1080p).")]
    public float scrollSpeed = 90f;
    [Tooltip("Where the last line stops, as a fraction down from the top. 0.5 = middle of the panel.")]
    [Range(0f, 1f)] public float stopFraction = 0.5f;
    [Tooltip("Seconds to hold on the final screen before finishing.")]
    public float endHoldTime = 2.5f;
    [Tooltip("Ignore key presses for this long after opening (so the click that opened the panel doesn't skip it).")]
    public float skipGrace = 0.4f;
    [Range(0.3f, 1f)] public float widthFraction = 0.8f;
    public string skipHint = "Press any key to skip";

    [Header("Layout")]
    [Tooltip("Stretch the object this script is on to fill its parent, so the credits use the whole panel.")]
    public bool fillParent = true;
    [Tooltip("Optional: colour the panel behind the credits (e.g. dark) so text is readable. Needs an Image on this object.")]
    public bool applyBackdropColor = true;
    public Color backdropColor = new Color(0.02f, 0.05f, 0.04f, 0.92f);

    [Header("Look")]
    [Tooltip("Easiest: drag a .ttf pixel font here (e.g. PressStart2P-Regular). A TMP font is created automatically.")]
    public Font pixelFont;
    [Tooltip("Optional: an existing TMP Font Asset. Overrides Pixel Font if set.")]
    public TMP_FontAsset font;
    [Tooltip("Multiplies all text sizes. Pixel fonts are wide, so ~0.75 usually fits better.")]
    [Range(0.4f, 1.5f)] public float textScale = 0.75f;
    public Color titleColor = new Color32(140, 230, 70, 255);
    public Color headingColor = new Color32(140, 230, 70, 255);
    public Color roleColor = new Color32(185, 200, 190, 255);
    public Color nameColor = Color.white;

    [Header("Events")]
    [Tooltip("Fires when credits end or are skipped. Close the Credits Panel and show the Main Menu here.")]
    public UnityEvent onFinished;

    RectTransform viewport;
    RectTransform content;
    bool built;

    // ---------- Default credits ----------

    static Entry E(string role, params string[] names)
    {
        return new Entry { role = role, names = new List<string>(names) };
    }

    static Section S(string heading, params Entry[] entries)
    {
        return new Section { heading = heading, entries = new List<Entry>(entries) };
    }

    static List<Section> DefaultSections()
    {
        return new List<Section>
        {
            S("GAME DEVELOPMENT",
                E("Game Designer", "Neilsen Ilaga", "Jhustine Caballero"),
                E("Programmer", "Jhustine Caballero", "Neilsen Ilaga")),
            S("ART & ANIMATION",
                E("Character Artist", "Neilsen Ilaga"),
                E("Environment Artist", "Khent Orongan"),
                E("UI / UX Designer", "Neilsen Ilaga", "Isabel Francisco")),
            S("AUDIO & MUSIC",
                E("Background Music", "Aaron Lindo"),
                E("Sound Effects (SFX)", "Neilsen Ilaga", "Jhustine Caballero")),
            S("QUALITY ASSURANCE",
                E("Lead Playtester", "Kim Mendoza"),
                E("Playtesters", "Isabel Francisco", "Aaron Lindo", "Khent Orongan")),
            S("TOOLS & THIRD-PARTY ASSETS",
                E("Game Engine", "Unity"),
                E("Sprite Editor", "Aseprite"),
                E("Audio Software", "Software Name")),
            S("SPECIAL THANKS",
                E("", "Maam. Marife Macanlay", "Sir. Nickler Sarana", "Jenny Aredondua")),
        };
    }

    // ---------- Lifecycle ----------

    void OnEnable()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Build();
        content.anchoredPosition = new Vector2(0f, -100000f); // hidden while layout settles
        yield return null;

        Canvas.ForceUpdateCanvases();
        float vw = viewport.rect.width;
        float vh = viewport.rect.height;
        content.sizeDelta = new Vector2(vw * widthFraction, 0f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentHeight = content.rect.height;
        float y = -vh;                                   // start just below the panel
        float endY = contentHeight - vh * stopFraction;  // last line stops here
        float elapsed = 0f;
        bool skipped = false;

        // Scroll
        while (y < endY)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            y += scrollSpeed * dt;
            content.anchoredPosition = new Vector2(0f, y);

            if (elapsed > skipGrace && AnyKeyPressed()) { skipped = true; break; }
            yield return null;
        }

        // Hold on the last screen
        float hold = 0f;
        while (!skipped && hold < endHoldTime)
        {
            hold += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > skipGrace && AnyKeyPressed()) break;
            yield return null;
        }

        onFinished?.Invoke();
    }

    static bool AnyKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.anyKey.wasPressedThisFrame;
#else
        return Input.anyKeyDown;
#endif
    }

    // ---------- Building the UI ----------

    void Build()
    {
        if (built) return;
        built = true;

        if (fillParent && transform is RectTransform self)
        {
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;
        }
        if (applyBackdropColor && TryGetComponent<Image>(out var bg))
            bg.color = backdropColor;

        // Build a TMP font from the .ttf if no TMP asset was given
        if (!font && pixelFont)
            font = TMP_FontAsset.CreateFontAsset(pixelFont);

        // Masked viewport that fills this panel
        var vpGO = new GameObject("CreditsViewport", typeof(RectTransform), typeof(RectMask2D));
        vpGO.transform.SetParent(transform, false);
        viewport = vpGO.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;

        // Content that scrolls upward
        var cGO = new GameObject("CreditsContent", typeof(RectTransform),
                                 typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cGO.transform.SetParent(viewport, false);
        content = cGO.GetComponent<RectTransform>();
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        var vlg = cGO.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0f;

        var fit = cGO.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title block
        AddText(gameTitle, 130, titleColor, FontStyles.Bold);
        AddSpace(40);
        AddText(subtitle, 34, roleColor);
        AddSpace(8);
        AddText(studio, 56, nameColor, FontStyles.Bold);

        // Sections
        foreach (var sec in sections)
        {
            AddSpace(110);
            AddText(sec.heading, 50, headingColor, FontStyles.Bold);
            AddSpace(30);

            foreach (var entry in sec.entries)
            {
                if (!string.IsNullOrEmpty(entry.role))
                {
                    AddText(entry.role, 28, roleColor);
                    AddSpace(4);
                }
                if (entry.names.Count > 0)
                    AddText(string.Join("\n", entry.names), 38, nameColor);
                AddSpace(28);
            }
        }

        // Closing + footer
        if (!string.IsNullOrEmpty(closingMessage))
        {
            AddSpace(40);
            AddText(closingMessage, 34, roleColor, FontStyles.Italic);
        }
        AddSpace(160);
        AddText(footer, 28, roleColor);

        // "Press any key" hint (stays put, doesn't scroll)
        if (!string.IsNullOrEmpty(skipHint))
        {
            var hGO = new GameObject("SkipHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hGO.transform.SetParent(viewport, false);
            var hrt = hGO.GetComponent<RectTransform>();
            hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-30f, 20f);
            hrt.sizeDelta = new Vector2(600f, 40f);
            var ht = hGO.GetComponent<TextMeshProUGUI>();
            if (font) ht.font = font;
            ht.text = skipHint;
            ht.fontSize = 24 * textScale;
            ht.color = new Color(roleColor.r, roleColor.g, roleColor.b, 0.7f);
            ht.alignment = TextAlignmentOptions.BottomRight;
            ht.raycastTarget = false;
        }
    }

    TextMeshProUGUI AddText(string text, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(content, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size * textScale;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    void AddSpace(float height)
    {
        var go = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }
}