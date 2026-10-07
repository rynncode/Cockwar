using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The team display (Worms W.M.D. style), bottom-left of the screen. Replaces the old turn timer.
///
/// Team list, one row per team:
///   - the team's portrait in its colour, its name, and a health bar in the team colour showing the
///     TOTAL health of all its living cockroaches (with the number next to the name);
///   - sorted by remaining health, not by turn order, so the leader is always on top (with a crown)
///     and the team closest to elimination at the bottom. Rows slide to their new place when the
///     order changes, and a hit drains the bar with a pale trail behind it.
///   - the team whose turn it is has a brighter panel and a flashing portrait frame; an eliminated team is greyed out.
/// Under the list:
///   - the turn timer: the active player's seconds, big. Red and pulsing in the last seconds, dimmed
///     while the clock is stopped (after firing);
///   - the match clock: how long the match has been going (minutes:seconds).
///
/// Builds its own canvas in code. TurnManager adds one automatically; add this component yourself
/// (for example on the TurnManager object) to tune it in the Inspector.
/// </summary>
public class TeamHud : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to find the TurnManager in the scene automatically.")]
    public TurnManager turnManager;

    [Header("Turn timer warning")]
    [Tooltip("At this many seconds or fewer the turn timer turns red and pulses.")]
    public int warningSeconds = 10;

    public Color warningColor = new Color(1f, 0.16f, 0.12f);

    [Tooltip("Played on every second of the warning time, a little higher each time. Leave empty for silence.")]
    public AudioClip tickSound;
    [Range(0f, 1f)] public float tickVolume = 0.6f;

    [Header("Look")]
    [Tooltip("Gap between the display and the bottom-left corner of the screen, in UI units at 1920x1080.")]
    public float margin = 24f;

    [Tooltip("Draw order of the display's canvas.")]
    public int canvasSortingOrder = 55;

    [Tooltip("How fast a team's bar drains after a hit (fraction of a full bar per second). The pale trail follows slower.")]
    public float drainSpeed = 0.8f;

    // Layout, in UI units at 1920x1080.
    private const float Width = 440f;
    private const float RowHeight = 62f;
    private const float RowGap = 22f;       // the portraits are taller than the rows, so leave room for them
    private const float AvatarSize = 74f;
    private const float TimerHeight = 104f;
    private const float TimerGap = 14f;
    private const float PanelPixel = 4f;      // size of one pixel of the panels' outline
    private const float NamePixel = 3.5f;     // size of one font pixel of the team names
    private const float ClockPixel = 6f;

    private static readonly Color Teal = new Color(0.37f, 0.67f, 0.75f, 0.95f);
    private static readonly Color TealDark = new Color(0.17f, 0.38f, 0.45f, 1f);
    private static readonly Color TrackColor = new Color(0.07f, 0.13f, 0.17f, 0.95f);
    private static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.75f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.2f, 1f);

    private class Row
    {
        public MatchTeam team;
        public RectTransform root;
        public CanvasGroup group;
        public Image panel;
        public Image avatarFrame;
        public Image crown;
        public Image total;
        public RectTransform fill;
        public RectTransform trail;
        public float shownFill = 1f;
        public float shownTrail = 1f;
        public float shownTotal = -1f;
        public int drawnTotal = -1;
        public float y = float.NaN;
        public float yVelocity;
        public float activeBlend;
    }

    private readonly List<Row> rows = new List<Row>();
    private GameObject canvasObject;
    private RectTransform root;
    private CanvasGroup rootGroup;
    private RectTransform rowsRoot;
    private Image timerBox;
    private RectTransform turnDigitsRect;
    private Image turnDigits;
    private Image clockDigits;
    private AudioSource audioSource;

    private bool built;
    private float slide;            // 0 = hidden below the screen, 1 = in place
    private float matchTime;
    private int shownSeconds = -1;
    private int shownClock = -1;
    private float tickPunch;
    private CockroachMovement lastPlayer;

    private void Awake()
    {
        if (turnManager == null) turnManager = GetComponent<TurnManager>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    // =====================================================================
    // Building
    // =====================================================================

    /// <summary>Built once the teams exist (after the CHOOSE MATCH screen).</summary>
    private void Build()
    {
        built = true;

        canvasObject = new GameObject("TeamHud_Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        audioSource = canvasObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.outputAudioMixerGroup = Sfx.Output;

        List<MatchTeam> teams = new List<MatchTeam>(turnManager.Teams);
        if (teams.Count == 0) teams = TeamsFromPlayers();

        float listHeight = teams.Count * RowHeight + Mathf.Max(0, teams.Count - 1) * RowGap;
        float totalHeight = listHeight + TimerGap + TimerHeight;

        root = NewRect("TeamHud", canvasObject.transform);
        root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
        root.sizeDelta = new Vector2(Width, totalHeight);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        BuildTimer();

        rowsRoot = NewRect("Teams", root);
        rowsRoot.anchorMin = rowsRoot.anchorMax = rowsRoot.pivot = Vector2.zero;
        rowsRoot.anchoredPosition = new Vector2(0f, TimerHeight + TimerGap);
        rowsRoot.sizeDelta = new Vector2(Width, listHeight);

        foreach (MatchTeam team in teams) rows.Add(BuildRow(team));
        ApplySlide();
    }

    /// <summary>No teams (a scene set up without MatchSetup): one "team" per player.</summary>
    private List<MatchTeam> TeamsFromPlayers()
    {
        List<MatchTeam> list = new List<MatchTeam>();
        for (int i = 0; i < turnManager.players.Count; i++)
        {
            CockroachMovement player = turnManager.players[i];
            if (player == null) continue;

            PlayerLabel label = player.GetComponent<PlayerLabel>();
            int number = label != null ? label.playerNumber : i + 1;
            Health health = player.GetComponent<Health>();
            SpriteRenderer body = player.GetComponentInChildren<SpriteRenderer>();

            MatchTeam team = new MatchTeam
            {
                name = "P" + number,
                color = PlayerLabel.GetPlayerColor(number),
                icon = body != null ? body.sprite : null,
                startingHealth = health != null ? health.maxHealth : 100,
            };
            team.members.Add(player);
            list.Add(team);
        }
        return list;
    }

    private Row BuildRow(MatchTeam team)
    {
        Row row = new Row { team = team };

        row.root = NewRect(team.name, rowsRoot);
        row.root.anchorMin = row.root.anchorMax = row.root.pivot = Vector2.zero;
        row.root.sizeDelta = new Vector2(Width, RowHeight);
        row.group = row.root.gameObject.AddComponent<CanvasGroup>();

        // The bar panel, starting under the middle of the portrait.
        float panelLeft = AvatarSize * 0.5f;
        row.panel = NewPanel("Panel", row.root, Teal);
        Place(row.panel.rectTransform, panelLeft, 0f, Width - panelLeft, RowHeight);

        // Portrait: team-coloured frame, light inside, the cockroach on top.
        float avatarY = (RowHeight - AvatarSize) * 0.5f;
        row.avatarFrame = NewPanel("Avatar", row.root, team.color);
        Place(row.avatarFrame.rectTransform, 0f, avatarY, AvatarSize, AvatarSize);

        Image inner = NewPanel("Inner", row.avatarFrame.rectTransform, new Color(0.93f, 0.95f, 0.96f, 1f));
        Stretch(inner.rectTransform, 8f);

        if (team.icon != null)
        {
            Image icon = NewImage("Icon", inner.rectTransform, team.icon, Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 4f);
        }

        // Crown for the leader, tilted on the portrait's top-left corner.
        row.crown = NewImage("Crown", row.root, PixelSprites.Crown(), Gold);
        row.crown.rectTransform.anchorMin = row.crown.rectTransform.anchorMax = Vector2.zero;
        row.crown.rectTransform.sizeDelta = PixelSprites.Crown().rect.size * 4f;
        row.crown.rectTransform.anchoredPosition = new Vector2(AvatarSize * 0.18f, avatarY + AvatarSize - 4f);
        row.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        row.crown.enabled = false;

        float textLeft = AvatarSize + 14f;

        // Team name, top-left of the panel.
        Image name = NewImage("Name", row.root, null, Color.white);
        SetPixelText(name, team.name, NamePixel);
        name.rectTransform.anchorMin = name.rectTransform.anchorMax = Vector2.zero;
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.anchoredPosition = new Vector2(textLeft, RowHeight - 9f);

        // Total health, top-right.
        row.total = NewImage("Total", row.root, null, Color.white);
        row.total.rectTransform.anchorMin = row.total.rectTransform.anchorMax = Vector2.zero;
        row.total.rectTransform.pivot = new Vector2(1f, 1f);
        row.total.rectTransform.anchoredPosition = new Vector2(Width - 14f, RowHeight - 9f);

        // Health bar: dark track, pale trail (recent damage), team-coloured fill.
        RectTransform track = NewImage("Track", row.root, null, TrackColor).rectTransform;
        Place(track, textLeft, 10f, Width - 14f - textLeft, 16f);

        row.trail = NewImage("Trail", track, null, TrailColor).rectTransform;
        Stretch(row.trail, 3f);
        row.fill = NewImage("Fill", track, null, team.color).rectTransform;
        Stretch(row.fill, 3f);

        return row;
    }

    private void BuildTimer()
    {
        // Turn seconds: a big square box.
        timerBox = NewPanel("TurnTimer", root, Teal);
        Place(timerBox.rectTransform, 0f, 0f, TimerHeight, TimerHeight);

        Image inner = NewPanel("Inner", timerBox.rectTransform, TealDark);
        Stretch(inner.rectTransform, 10f);

        turnDigits = NewImage("Seconds", inner.rectTransform, null, Color.white);
        turnDigitsRect = turnDigits.rectTransform;
        turnDigitsRect.anchoredPosition = Vector2.zero;

        // Match clock: the wide box to the right.
        float left = TimerHeight + 12f;
        Image clockBox = NewPanel("MatchClock", root, Teal);
        Place(clockBox.rectTransform, left, 0f, Width - left, TimerHeight);

        Image clockInner = NewPanel("Inner", clockBox.rectTransform, TealDark);
        Stretch(clockInner.rectTransform, 10f);

        clockDigits = NewImage("Clock", clockInner.rectTransform, null, Color.white);
        SetPixelText(clockDigits, "00:00", ClockPixel);
    }

    // =====================================================================
    // Running
    // =====================================================================

    private void Update()
    {
        if (turnManager == null) return;
        if (!built)
        {
            if (!MatchSetup.IsReady) return;
            Build();
        }

        float dt = Time.deltaTime;
        CockroachMovement current = turnManager.CurrentPlayer;

        // Slide up from below the screen once the first turn starts, back down when the match ends.
        slide = Mathf.MoveTowards(slide, current != null ? 1f : 0f, dt * 3f);
        ApplySlide();

        if (current != null) matchTime += dt;

        UpdateRows(current, dt);
        UpdateTurnTimer(current, dt);
        UpdateClock();
    }

    private void UpdateRows(CockroachMovement current, float dt)
    {
        // Rank by remaining health (most first); ties keep team order.
        List<Row> ranked = new List<Row>(rows);
        ranked.Sort((a, b) =>
        {
            int byHealth = b.team.TotalHealth.CompareTo(a.team.TotalHealth);
            return byHealth != 0 ? byHealth : rows.IndexOf(a).CompareTo(rows.IndexOf(b));
        });

        bool clearLeader = ranked.Count > 1 && ranked[0].team.TotalHealth > ranked[1].team.TotalHealth;

        for (int rank = 0; rank < ranked.Count; rank++)
        {
            Row row = ranked[rank];
            int total = row.team.TotalHealth;
            bool alive = row.team.HasLivingMember;

            // Slide to the place for this rank (top = leader).
            float targetY = (ranked.Count - 1 - rank) * (RowHeight + RowGap);
            if (float.IsNaN(row.y)) row.y = targetY;
            row.y = Mathf.SmoothDamp(row.y, targetY, ref row.yVelocity, 0.25f, Mathf.Infinity, dt);

            // The team whose turn it is.
            bool active = current != null && row.team.Contains(current);
            row.root.anchoredPosition = new Vector2(0f, row.y);

            // Same size and position for every row; the active team is marked by colour instead:
            // a brighter panel, and its portrait frame flashing between the team colour and white.
            row.activeBlend = Mathf.MoveTowards(row.activeBlend, active ? 1f : 0f, dt * 6f);
            row.panel.color = Color.Lerp(Teal, Color.white, 0.22f * row.activeBlend);
            float flash = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            row.avatarFrame.color = Color.Lerp(row.team.color, Color.white, flash * 0.75f * row.activeBlend);

            row.crown.enabled = rank == 0 && clearLeader && alive;
            row.group.alpha = Mathf.MoveTowards(row.group.alpha, alive ? 1f : 0.45f, dt * 2f);

            // Bar: drains toward the real total; the pale trail follows behind it.
            float target = Mathf.Clamp01(total / (float)row.team.MaxHealth);
            row.shownFill = row.shownFill > target ? Mathf.MoveTowards(row.shownFill, target, dt * drainSpeed) : target;
            row.shownTrail = row.shownTrail > row.shownFill ? Mathf.MoveTowards(row.shownTrail, row.shownFill, dt * drainSpeed * 0.35f) : row.shownFill;
            row.fill.anchorMax = new Vector2(row.shownFill, 1f);
            row.trail.anchorMax = new Vector2(row.shownTrail, 1f);

            // Number counts toward the total.
            if (row.shownTotal < 0f) row.shownTotal = total;
            float speed = Mathf.Max(40f, Mathf.Abs(total - row.shownTotal) * 3f);
            row.shownTotal = Mathf.MoveTowards(row.shownTotal, total, dt * speed);
            int drawn = Mathf.RoundToInt(row.shownTotal);
            if (drawn != row.drawnTotal)
            {
                row.drawnTotal = drawn;
                SetPixelText(row.total, drawn.ToString(), NamePixel);
            }
        }
    }

    private void UpdateTurnTimer(CockroachMovement current, float dt)
    {
        tickPunch = Mathf.Max(0f, tickPunch - dt * 2.5f);

        if (current != lastPlayer)
        {
            lastPlayer = current;
            shownSeconds = -1;   // a new turn: redraw without counting it as a warning tick
        }

        bool running = turnManager.IsTurnClockRunning;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(turnManager.TurnTimeLeft));
        bool warning = running && seconds <= warningSeconds;

        if (seconds != shownSeconds)
        {
            if (warning && shownSeconds != -1 && seconds < shownSeconds) Tick(seconds);
            shownSeconds = seconds;

            string text = seconds.ToString("00");
            float pixel = Mathf.Min(9f, (TimerHeight - 34f) / Mathf.Max(1f, PixelNumber.MeasureWidthInPixels(text)));
            SetPixelText(turnDigits, text, pixel);
        }

        float punch = tickPunch * tickPunch;
        Color digitColor = warning ? Color.Lerp(warningColor, Color.white, punch * 0.8f) : Color.white;
        digitColor.a = running ? 1f : 0.45f;   // dimmed while the clock is stopped
        turnDigits.color = digitColor;

        float scale = 1f + punch * 0.3f;
        turnDigitsRect.localScale = new Vector3(scale, scale, 1f);
        timerBox.color = warning ? Color.Lerp(Teal, warningColor, 0.35f + 0.5f * punch) : Teal;
    }

    private void UpdateClock()
    {
        int totalSeconds = Mathf.FloorToInt(matchTime);
        if (totalSeconds == shownClock) return;

        shownClock = totalSeconds;
        int minutes = Mathf.Min(99, totalSeconds / 60);
        SetPixelText(clockDigits, minutes.ToString("00") + ":" + (totalSeconds % 60).ToString("00"), ClockPixel);
    }

    private void Tick(int seconds)
    {
        tickPunch = 1f;
        if (tickSound == null) return;

        audioSource.pitch = 1f + Mathf.Max(0, warningSeconds - seconds) * 0.04f;
        audioSource.PlayOneShot(tickSound, tickVolume * GameSettings.SfxVolume);
    }

    private void ApplySlide()
    {
        if (root == null) return;

        // Eased slide from below the screen.
        float eased = 1f - (1f - slide) * (1f - slide);
        float hiddenY = -root.sizeDelta.y - 40f;
        root.anchoredPosition = new Vector2(margin, Mathf.Lerp(hiddenY, margin, eased));
        rootGroup.alpha = slide > 0f ? 1f : 0f;
    }

    // =====================================================================
    // Small UI helpers
    // =====================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>A pixel panel with a dark outline and cut corners, in the given colour, any size.</summary>
    private static Image NewPanel(string name, Transform parent, Color color)
    {
        Image image = NewImage(name, parent, PixelSprites.Panel(), color);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PanelPixel;
        return image;
    }

    /// <summary>Pixel-font text at its natural size (one font pixel = pixelSize UI units).</summary>
    private static void SetPixelText(Image image, string text, float pixelSize)
    {
        image.sprite = PixelSprites.Text(text);
        image.rectTransform.sizeDelta = image.sprite.rect.size * pixelSize;
    }

    /// <summary>Places a rect by its bottom-left corner inside its parent.</summary>
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
