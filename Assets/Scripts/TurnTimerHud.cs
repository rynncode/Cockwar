using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turn timer at the top middle of the screen, drawn in the game's pixel font.
///  - Counts the active player's turn down in whole seconds, with a bar underneath that
///    drains in that player's color (and sweeps full again when the next turn starts).
///  - At Warning Seconds or less the number turns red and pulses on every second: it punches
///    bigger, flashes, and the panel glows red. In the last Panic Seconds it also shakes.
///  - Freezes and dims once the player has fired (the clock stops while the shot flies).
///  - Slides down when the first turn starts and back up when the match ends.
/// Builds its own canvas in code. TurnManager adds one automatically if the scene has none;
/// add this component yourself (for example on the TurnManager object) to tune it in the Inspector.
/// </summary>
public class TurnTimerHud : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to find the TurnManager in the scene automatically.")]
    public TurnManager turnManager;

    [Header("Warning")]
    [Tooltip("At this many seconds or fewer the timer turns red and pulses.")]
    public int warningSeconds = 10;

    [Tooltip("In the last this-many seconds the timer also shakes.")]
    public int panicSeconds = 3;

    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.16f, 0.12f);

    [Header("Sound (optional)")]
    [Tooltip("Played on every second of the warning time, a little higher each time. Leave empty for silence.")]
    public AudioClip tickSound;

    [Range(0f, 1f)]
    public float tickVolume = 0.6f;

    [Header("Look")]
    [Tooltip("Size of one font pixel of the number, in UI units at 1920x1080.")]
    public float digitPixelSize = 8f;

    [Tooltip("Gap between the top of the screen and the timer, in UI units at 1920x1080.")]
    public float topMargin = 16f;

    [Tooltip("Draw order of the timer canvas. Higher = on top of other canvases.")]
    public int canvasSortingOrder = 55;

    // Layout, in UI units at 1920x1080.
    private const float PanelPixelSize = 4f;   // size of one pixel of the panel's outline
    private const float PadSide = 26f;
    private const float PadTop = 12f;
    private const float BarHeight = 10f;
    private const float BarGap = 10f;
    private const float PadBottom = 14f;
    private const float MinPanelWidth = 150f;
    private const float GlowSize = 10f;

    private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.09f, 0.85f);
    private static readonly Color PanelWarningColor = new Color(0.35f, 0.03f, 0.03f, 0.9f);

    private static TurnTimerHud instance;

    /// <summary>
    /// UI units (at 1920x1080) the timer takes at the top of the screen, so other top-of-screen
    /// HUD (DistanceHud, the bubbles' edge clamp) can stay below it. 0 when there is no timer.
    /// </summary>
    public static float ReservedTopSpace =>
        instance != null && instance.isActiveAndEnabled ? instance.reservedTopSpace : 0f;

    private GameObject canvasObject;
    private RectTransform root;
    private CanvasGroup group;
    private Image glow;
    private Image panel;
    private RectTransform digitsRect;
    private Image digits;
    private RectTransform barFill;
    private Image barFillImage;
    private AudioSource audioSource;

    private float panelHeight;
    private float reservedTopSpace;

    private float slideY;
    private float slideVelocity;
    private int shownSeconds = -1;
    private float tickPunch;     // 1 on a warning tick, decays to 0
    private float turnPop;       // 1 when a turn starts, decays to 0
    private float shownFill;
    private Color playerColor = Color.white;

    private void Awake()
    {
        instance = this;

        if (turnManager == null)
            turnManager = GetComponent<TurnManager>();
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        BuildHud();

        // Start hidden above the screen.
        slideY = panelHeight + GlowSize + 20f;
        ApplySlide(0f);
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.OnTurnStarted += HandleTurnStarted;

        if (canvasObject != null)
            canvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnTurnStarted -= HandleTurnStarted;

        if (canvasObject != null)
            canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    // =====================================================================
    // Building
    // =====================================================================

    private void BuildHud()
    {
        canvasObject = new GameObject("TurnTimer_Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        // Same scaling as DistanceHud, so both agree on what a UI unit is.
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Sized for the widest number this timer will ever show.
        int maxSeconds = turnManager != null ? Mathf.CeilToInt(turnManager.turnTimeLimit) : 99;
        int maxDigits = Mathf.Max(2, maxSeconds.ToString().Length);
        float digitsHeight = PixelNumber.HeightInPixels * digitPixelSize;
        float digitsWidth = PixelNumber.WidthInPixels(maxDigits) * digitPixelSize;

        float panelWidth = Mathf.Max(MinPanelWidth, digitsWidth + PadSide * 2f);
        panelHeight = PadTop + digitsHeight + BarGap + BarHeight + PadBottom;
        reservedTopSpace = topMargin + panelHeight + 8f;

        root = NewRect("TurnTimer", canvasObject.transform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(panelWidth, panelHeight);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Red glow behind the panel, only seen in the warning time.
        glow = NewPixelPanel("Glow", root, new Color(1f, 0.1f, 0.05f, 0f));
        glow.sprite = PixelSprites.Block();
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-GlowSize, -GlowSize);
        glowRect.offsetMax = new Vector2(GlowSize, GlowSize);

        panel = NewPixelPanel("Panel", root, PanelColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        // The number. Pivot in its centre so the pulse grows from the middle.
        digitsRect = NewRect("Seconds", panelRect);
        digitsRect.anchorMin = digitsRect.anchorMax = new Vector2(0.5f, 1f);
        digitsRect.pivot = new Vector2(0.5f, 0.5f);
        digitsRect.anchoredPosition = new Vector2(0f, -(PadTop + digitsHeight * 0.5f));
        digits = digitsRect.gameObject.AddComponent<Image>();
        digits.raycastTarget = false;

        // Time bar: a dark track with a fill that shrinks from the right.
        RectTransform barTrack = NewRect("BarTrack", panelRect);
        barTrack.anchorMin = new Vector2(0f, 0f);
        barTrack.anchorMax = new Vector2(1f, 0f);
        barTrack.pivot = new Vector2(0.5f, 0f);
        barTrack.offsetMin = new Vector2(PadSide - 8f, PadBottom);
        barTrack.offsetMax = new Vector2(-(PadSide - 8f), PadBottom + BarHeight);
        Image trackImage = barTrack.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.65f);
        trackImage.raycastTarget = false;

        barFill = NewRect("BarFill", barTrack);
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.offsetMin = new Vector2(2f, 2f);
        barFill.offsetMax = new Vector2(-2f, -2f);
        barFillImage = barFill.gameObject.AddComponent<Image>();
        barFillImage.raycastTarget = false;

        audioSource = canvasObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        SetDigits(maxSeconds);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewPixelPanel(string name, Transform parent, Color color)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = PixelSprites.Panel();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PanelPixelSize;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void SetDigits(int seconds)
    {
        digits.sprite = PixelSprites.Text(seconds.ToString());
        Vector2 size = digits.sprite.rect.size;
        digitsRect.sizeDelta = size * digitPixelSize;
    }

    // =====================================================================
    // Running
    // =====================================================================

    private void HandleTurnStarted(CockroachMovement player)
    {
        PlayerLabel label = player != null ? player.GetComponent<PlayerLabel>() : null;
        int number = label != null ? label.playerNumber : turnManager.players.IndexOf(player) + 1;
        playerColor = PlayerLabel.GetPlayerColor(number);

        shownSeconds = -1;   // redraw the number without counting it as a warning tick
        turnPop = 1f;
        tickPunch = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        bool hasTurn = turnManager != null && turnManager.CurrentPlayer != null && turnManager.turnTimeLimit > 0f;
        bool running = hasTurn && turnManager.IsTurnClockRunning;

        // Slide in while a turn is on, out (up off the screen) otherwise. Dim while the clock is stopped.
        float targetY = hasTurn ? -topMargin : panelHeight + GlowSize + 20f;
        slideY = Mathf.SmoothDamp(slideY, targetY, ref slideVelocity, 0.2f);
        group.alpha = Mathf.MoveTowards(group.alpha, !hasTurn || running ? 1f : 0.5f, dt * 4f);

        tickPunch = Mathf.Max(0f, tickPunch - dt * 2.5f);
        turnPop = Mathf.Max(0f, turnPop - dt * 3f);

        if (!hasTurn)
        {
            ApplySlide(0f);
            return;
        }

        float timeLeft = Mathf.Max(0f, turnManager.TurnTimeLeft);
        int seconds = Mathf.CeilToInt(timeLeft);
        bool warning = seconds <= warningSeconds;

        if (seconds != shownSeconds)
        {
            // A new second inside the warning time (not the first number of a turn): pulse.
            if (running && warning && shownSeconds != -1 && seconds < shownSeconds)
                Tick(seconds);

            shownSeconds = seconds;
            SetDigits(seconds);
        }

        // 0 at the start of the warning, 1 on the last second: everything gets more intense.
        float urgency = warning ? 1f - (seconds - 1f) / Mathf.Max(1f, warningSeconds - 1f) : 0f;
        float punch = tickPunch * tickPunch;   // sharp hit, quick fade
        float pop = turnPop * turnPop;

        // Number: red in the warning time, flashing toward white on every tick.
        digits.color = warning ? Color.Lerp(warningColor, Color.white, punch * 0.8f) : normalColor;
        float scale = 1f + punch * Mathf.Lerp(0.18f, 0.35f, urgency) + pop * 0.2f;
        digitsRect.localScale = new Vector3(scale, scale, 1f);

        // Panel and glow pulse red with the ticks, with a soft breathing glow in between.
        panel.color = warning ? Color.Lerp(PanelColor, PanelWarningColor, 0.3f + 0.7f * punch) : PanelColor;

        float breathing = running && warning ? 0.1f + 0.08f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 12f, urgency)) : 0f;
        float glowAlpha = warning ? Mathf.Clamp01(breathing + punch * 0.75f) : 0f;
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b,
            Mathf.MoveTowards(glow.color.a, glowAlpha, dt * 8f));

        // Bar: drains with the clock, sweeps back up at a new turn.
        float targetFill = Mathf.Clamp01(timeLeft / turnManager.turnTimeLimit);
        shownFill = shownFill < targetFill ? Mathf.MoveTowards(shownFill, targetFill, dt * 2.5f) : targetFill;
        barFill.anchorMax = new Vector2(shownFill, 1f);
        barFillImage.color = warning ? Color.Lerp(playerColor, warningColor, 0.6f + 0.4f * punch) : playerColor;

        // Last few seconds: a small shake on each tick.
        float shake = running && seconds <= panicSeconds ? Mathf.Sin(Time.time * 60f) * 6f * tickPunch : 0f;
        ApplySlide(shake);
    }

    private void Tick(int seconds)
    {
        tickPunch = 1f;

        if (tickSound != null)
        {
            // A little higher each second as time runs out.
            audioSource.pitch = 1f + Mathf.Max(0, warningSeconds - seconds) * 0.04f;
            audioSource.PlayOneShot(tickSound, tickVolume);
        }
    }

    private void ApplySlide(float shakeX)
    {
        root.anchoredPosition = new Vector2(shakeX, slideY);
    }
}
