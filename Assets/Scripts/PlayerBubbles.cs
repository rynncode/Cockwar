using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Head bubbles: a small pixel speech bubble ("P1", "P2" ...) in each player's (team) color, floating
/// above their head (above any bars), so you can always see who is where. Worms-style, the bubble
/// also shows that cockroach's health number under the name (Show Health), counting down after a hit.
///  - The player whose turn it is gets a full-size bubble that bobs gently and pops in when the
///    turn starts (right after the big P1 / P2 label from PlayerLabel fades, so they don't double up).
///  - Everyone else gets a smaller, slightly faded bubble.
///  - A player who is off-screen keeps their bubble pinned to the edge of the screen, with an
///    arrow pointing toward them. This replaces the old OffscreenIndicator (see Replace Offscreen Indicator).
///  - Bubbles hide during the intro, for dead players, and when the match ends.
/// Builds its own canvas in code. TurnManager adds one automatically if the scene has none;
/// add this component yourself (for example on the TurnManager object) to tune it in the Inspector.
/// </summary>
[DefaultExecutionOrder(100)] // after the camera and the overhead bars have moved this frame
public class PlayerBubbles : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to find the TurnManager in the scene automatically.")]
    public TurnManager turnManager;

    [Tooltip("Leave empty to use the main camera.")]
    public Camera cam;

    [Header("Who")]
    [Tooltip("On = a bubble over every living player. Off = only over the player whose turn it is.")]
    public bool showOtherPlayers = true;

    [Tooltip("Worms-style: each bubble also shows that cockroach's health number, always visible, and the health bar over the head is hidden.")]
    public bool showHealth = true;

    [Header("Off-screen")]
    [Tooltip("Keep an off-screen player's bubble at the edge of the screen, with an arrow pointing at them.")]
    public bool clampToScreenEdge = true;

    [Tooltip("Gap between a pinned bubble and the screen edge, in UI units at 1920x1080.")]
    public float edgePadding = 36f;

    [Tooltip("The pinned bubbles do the old off-screen arrow's job (and also say who it is), so that one is switched off to avoid two markers for one cockroach.")]
    public bool replaceOffscreenIndicator = true;

    [Header("Look")]
    [Tooltip("Size of one pixel of the bubble, in UI units at 1920x1080.")]
    public float pixelSize = 4f;

    [Tooltip("World units between the top of the head (or the highest bar) and the tip of the bubble.")]
    public float gapAboveHead = 1f;

    [Tooltip("Size of the other players' bubbles compared to the active player's.")]
    [Range(0.3f, 1f)]
    public float otherPlayersScale = 0.72f;

    [Tooltip("Opacity of the other players' bubbles.")]
    [Range(0f, 1f)]
    public float otherPlayersAlpha = 0.85f;

    [Tooltip("How far the active bubble bobs up and down, in UI units.")]
    public float bobHeight = 6f;

    [Tooltip("Bobs per second.")]
    public float bobSpeed = 1.4f;

    [Tooltip("Draw order of the bubble canvas. Below the timer and distance HUD.")]
    public int canvasSortingOrder = 45;

    // Bubble layout in bubble pixels. Text is 7 pixels tall; the tail is 4 tall and its
    // top row overlaps the panel's bottom outline.
    private const int PadX = 3;
    private const int PadY = 2;
    private const int TailHeight = 4;
    private const int TailWidth = 7;

    private class Bubble
    {
        public CockroachMovement player;
        public PlayerLabel label;
        public OverheadStack stack;
        public Collider2D body;

        public RectTransform root;
        public CanvasGroup group;
        public RectTransform tail;
        public RectTransform arrow;

        public Vector2 panelSize;     // unscaled UI units
        public float panelCenterY;    // unscaled, from the tail tip

        public Health health;
        public HealthBar healthBar;
        public Image healthText;
        public int shownHealth;

        public float scale;
        public float scaleVelocity;
        public float lift = float.NaN;   // world units from the cockroach's position to the tip
        public float liftVelocity;
        public float activeBlend;
        public bool wasActive;
    }

    private readonly List<Bubble> bubbles = new List<Bubble>();
    private bool bubblesBuilt;

    private static PlayerBubbles instance;

    /// <summary>True while the bubbles show the health numbers (HealthBar then hides its own bar).</summary>
    public static bool ShowsHealth => instance != null && instance.isActiveAndEnabled && instance.showHealth;
    private GameObject canvasObject;
    private RectTransform canvasRect;

    private void Awake()
    {
        instance = this;

        if (turnManager == null)
            turnManager = GetComponent<TurnManager>();
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void Start()
    {
        if (cam == null)
            cam = Camera.main;

        if (turnManager == null)
        {
            Debug.LogWarning("PlayerBubbles: no TurnManager in the scene, so there are no players to show.");
            enabled = false;
            return;
        }

        if (clampToScreenEdge && replaceOffscreenIndicator && turnManager.opponentIndicator != null)
            turnManager.opponentIndicator.gameObject.SetActive(false);

        BuildCanvas();
    }

    /// <summary>
    /// One bubble per player, made once the CHOOSE MATCH screen has set the line-up (2 VS 2 adds
    /// two cockroaches after the scene has started).
    /// </summary>
    private void BuildBubbles()
    {
        bubblesBuilt = true;
        for (int i = 0; i < turnManager.players.Count; i++)
        {
            if (turnManager.players[i] != null)
                bubbles.Add(BuildBubble(turnManager.players[i], i));
        }
    }

    private void OnEnable()
    {
        if (canvasObject != null)
            canvasObject.SetActive(true);
    }

    private void OnDisable()
    {
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

    private void BuildCanvas()
    {
        canvasObject = new GameObject("PlayerBubbles_Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    private Bubble BuildBubble(CockroachMovement player, int index)
    {
        PlayerLabel label = player.GetComponent<PlayerLabel>();
        int number = label != null ? label.playerNumber : index + 1;
        Color color = PlayerLabel.GetPlayerColor(number);

        Bubble b = new Bubble
        {
            player = player,
            label = label,
            stack = OverheadStack.For(player.gameObject),
            body = player.GetComponent<Collider2D>(),
        };

        b.root = NewRect("Bubble_P" + number, canvasRect);
        b.root.anchorMin = b.root.anchorMax = new Vector2(0.5f, 0.5f);
        b.root.sizeDelta = Vector2.zero;
        b.group = b.root.gameObject.AddComponent<CanvasGroup>();
        b.group.alpha = 0f;
        b.group.blocksRaycasts = false;
        b.group.interactable = false;

        Sprite textSprite = PixelSprites.Text("P" + number);
        Vector2 textPixels = textSprite.rect.size;

        // Worms-style: a second line with the health number, under the name. The panel is sized
        // for three digits so it doesn't jump around as the number changes.
        Vector2 healthPixels = showHealth ? PixelSprites.Text("888").rect.size : Vector2.zero;
        Vector2 contentPixels = showHealth
            ? new Vector2(Mathf.Max(textPixels.x, healthPixels.x), textPixels.y + healthPixels.y - 1f) // outlines overlap by a pixel
            : textPixels;
        Vector2 panelPixels = contentPixels + new Vector2(PadX * 2, PadY * 2);

        // Root sits at the tip of the tail; the panel is above it.
        b.panelSize = panelPixels * pixelSize;
        b.panelCenterY = (TailHeight - 1) * pixelSize + b.panelSize.y * 0.5f;

        Image panel = NewImage("Panel", b.root, PixelSprites.Panel(), color);
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 1f / pixelSize;
        panel.rectTransform.sizeDelta = b.panelSize;
        panel.rectTransform.anchoredPosition = new Vector2(0f, b.panelCenterY);

        Image text = NewImage("Text", panel.rectTransform, textSprite, Color.white);
        text.rectTransform.sizeDelta = textPixels * pixelSize;
        text.rectTransform.anchoredPosition = showHealth
            ? new Vector2(0f, (contentPixels.y - textPixels.y) * 0.5f * pixelSize)
            : Vector2.zero;

        if (showHealth)
        {
            b.health = player.GetComponent<Health>();
            b.healthBar = player.GetComponent<HealthBar>();
            b.healthText = NewImage("Health", panel.rectTransform, null, Color.white);
            b.healthText.rectTransform.anchoredPosition = new Vector2(0f, -(contentPixels.y - healthPixels.y) * 0.5f * pixelSize);
            b.shownHealth = int.MinValue;
            UpdateHealthText(b);
        }

        // Drawn after the panel so its top row covers the panel's bottom outline and the two join.
        Image tail = NewImage("Tail", b.root, PixelSprites.TailDown(), color);
        b.tail = tail.rectTransform;
        b.tail.pivot = new Vector2(0.5f, 0f);
        b.tail.sizeDelta = new Vector2(TailWidth, TailHeight) * pixelSize;
        b.tail.anchoredPosition = Vector2.zero;

        Image arrow = NewImage("Arrow", b.root, PixelSprites.ArrowRight(), color);
        b.arrow = arrow.rectTransform;
        b.arrow.sizeDelta = arrow.sprite.rect.size * pixelSize;
        b.arrow.gameObject.SetActive(false);

        b.scale = otherPlayersScale;
        return b;
    }

    /// <summary>Shows the health number: the one the health bar is counting down, so hits tick down Worms-style.</summary>
    private void UpdateHealthText(Bubble b)
    {
        if (b.healthText == null) return;

        int value = b.healthBar != null ? b.healthBar.DisplayedHealth : (b.health != null ? b.health.CurrentHealth : 0);
        value = Mathf.Max(0, value);
        if (value == b.shownHealth) return;

        b.shownHealth = value;
        b.healthText.sprite = PixelSprites.Text(value.ToString());
        b.healthText.rectTransform.sizeDelta = b.healthText.sprite.rect.size * pixelSize;
    }

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

    // =====================================================================
    // Running
    // =====================================================================

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null || canvasRect == null)
            return;

        if (!bubblesBuilt)
        {
            if (!MatchSetup.IsReady) return;
            BuildBubbles();
        }

        // Very long frames would make the springs jump; cap the step.
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        CockroachMovement current = turnManager.CurrentPlayer;

        foreach (Bubble b in bubbles)
            UpdateBubble(b, current, dt);
    }

    private void UpdateBubble(Bubble b, CockroachMovement current, float dt)
    {
        if (b.player == null)
        {
            b.root.gameObject.SetActive(false);
            return;
        }

        bool alive = IsAlive(b.player);
        UpdateHealthText(b);

        // "Active" = in control right now. Once they've fired, the camera is on the shot,
        // so their bubble steps back to the normal size.
        bool isActive = alive && b.player == current && turnManager.IsTurnClockRunning;

        // --- Where the tip of the tail goes: just above the head, or above the highest bar ---
        Vector3 playerPos = b.player.transform.position;
        float targetLift = b.stack.TopOfStackY - playerPos.y + gapAboveHead;
        if (float.IsNaN(b.lift))
            b.lift = targetLift;
        b.lift = Mathf.SmoothDamp(b.lift, targetLift, ref b.liftVelocity, 0.12f, Mathf.Infinity, dt);

        Vector2 tip = WorldToCanvas(new Vector3(b.stack.CenterX, playerPos.y + b.lift, playerPos.z));
        Vector2 bodyPoint = WorldToCanvas(b.body != null ? b.body.bounds.center : playerPos);

        // --- Size: springs toward full size for the active player, with a little overshoot ---
        float targetScale = isActive ? 1f : otherPlayersScale;
        if (isActive && !b.wasActive)
        {
            b.scaleVelocity += 6f;
            b.root.SetAsLastSibling(); // active bubble on top of the others
        }
        b.wasActive = isActive;

        b.scaleVelocity += (targetScale - b.scale) * 260f * dt;
        b.scaleVelocity *= Mathf.Exp(-16f * dt);
        b.scale += b.scaleVelocity * dt;

        b.activeBlend = Mathf.MoveTowards(b.activeBlend, isActive ? 1f : 0f, dt * 4f);
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f * bobSpeed) * bobHeight * b.activeBlend;

        // --- Pin to the screen edge when the bubble would be off-screen ---
        Vector2 center = tip + new Vector2(0f, (b.panelCenterY + bob) * b.scale);
        Vector2 pinned = center;
        bool isPinned = false;

        if (clampToScreenEdge)
        {
            Rect area = canvasRect.rect;
            float arrowRoom = b.arrow.sizeDelta.x + pixelSize;
            float halfW = (b.panelSize.x * 0.5f + arrowRoom) * b.scale;
            float halfH = (b.panelSize.y * 0.5f + arrowRoom) * b.scale;
            float topSpace = TurnTimerHud.ReservedTopSpace;

            pinned.x = Mathf.Clamp(center.x, area.xMin + edgePadding + halfW, area.xMax - edgePadding - halfW);
            pinned.y = Mathf.Clamp(center.y, area.yMin + edgePadding + halfH, area.yMax - edgePadding - topSpace - halfH);

            // Had to move to stay on screen (or under the timer): it points at the player instead.
            isPinned = (pinned - center).sqrMagnitude > 1f;
        }

        // --- Fade: hidden in the intro / game over and for the dead, and while the big
        //     P1 / P2 label is showing over this player (it's doing the same job then) ---
        bool labelShowing = b.label != null && b.label.IsShowing && !isPinned;
        float targetAlpha;
        if (current == null || !alive || labelShowing)
            targetAlpha = 0f;
        else if (isActive)
            targetAlpha = 1f;
        else
            targetAlpha = showOtherPlayers ? otherPlayersAlpha : 0f;

        // Popping in from hidden: start small so the spring gives it a little bounce.
        if (b.group.alpha < 0.05f && targetAlpha > 0f)
            b.scale = targetScale * 0.5f;

        b.group.alpha = Mathf.MoveTowards(b.group.alpha, targetAlpha, dt * 6f);
        b.root.gameObject.SetActive(b.group.alpha > 0f || targetAlpha > 0f);

        // --- Apply ---
        b.root.localScale = new Vector3(b.scale, b.scale, 1f);

        if (!isPinned)
        {
            b.root.anchoredPosition = tip + new Vector2(0f, bob * b.scale);
            b.tail.gameObject.SetActive(true);
            b.arrow.gameObject.SetActive(false);
            return;
        }

        // Pinned: no tail; an arrow on the side facing the player instead.
        b.root.anchoredPosition = pinned - new Vector2(0f, b.panelCenterY * b.scale);
        b.tail.gameObject.SetActive(false);
        b.arrow.gameObject.SetActive(true);

        Vector2 direction = bodyPoint - pinned;
        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.down;
        direction.Normalize();

        // Distance from the panel centre to its edge in this direction, plus room for the arrow.
        float reach = Mathf.Min(
            Mathf.Abs(direction.x) > 0.001f ? b.panelSize.x * 0.5f / Mathf.Abs(direction.x) : float.MaxValue,
            Mathf.Abs(direction.y) > 0.001f ? b.panelSize.y * 0.5f / Mathf.Abs(direction.y) : float.MaxValue);
        reach += b.arrow.sizeDelta.x * 0.5f;

        b.arrow.anchoredPosition = new Vector2(0f, b.panelCenterY) + direction * reach;
        b.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private Vector2 WorldToCanvas(Vector3 worldPosition)
    {
        Vector2 screen = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }

    /// <summary>Alive = still in the scene and not at 0 health.</summary>
    private static bool IsAlive(CockroachMovement player)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return false;

        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }
}
