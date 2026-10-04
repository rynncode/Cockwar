using UnityEngine;

/// <summary>
/// Shared code for the small bars that float above a cockroach's head
/// (health, stamina, jump power). A bar builds itself from plain sprites, fades in and
/// out, and lets OverheadStack decide where it sits so bars never overlap.
/// Not added to objects directly: add HealthBar, StaminaBar or JumpPowerBar instead.
/// </summary>
public abstract class OverheadBar : MonoBehaviour
{
    [Header("Size (world units)")]
    [Tooltip("Width of the bar when completely full.")]
    public float barWidth = 5f;

    [Tooltip("Height of the bar.")]
    public float barHeight = 0.7f;

    [Tooltip("Thickness of the dark outline around the bar.")]
    public float borderThickness = 0.15f;

    [Header("Background")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

    [Header("Drawing")]
    [Tooltip("The bar is drawn on the cockroach's own sorting layer, this many steps ABOVE the cockroach sprite. Raise it if the bar is hidden behind other sprites.")]
    public int sortingOrder = 100;

    // --- What each kind of bar decides ---

    /// <summary>Which place in the stack above the head this bar uses (OverheadStack.Slot...).</summary>
    protected abstract int Slot { get; }

    /// <summary>True while the bar should be on screen.</summary>
    protected abstract bool ShouldShow();

    /// <summary>How full the bar is, from 0 to 1.</summary>
    protected abstract float Value01();

    /// <summary>Fill color for the given fullness.</summary>
    protected abstract Color FillColor(float value01);

    /// <summary>Seconds the bar takes to fade in / out. 0 = instant.</summary>
    protected virtual float FadeTime => 0f;

    /// <summary>Called once from Awake, BEFORE the bar is built. A subclass can adjust its size here.</summary>
    protected virtual void OnBeforeBuild() { }

    /// <summary>Called once from Awake, after the bar is built, so subclasses can find their components.</summary>
    protected virtual void OnBarAwake() { }

    /// <summary>
    /// Called at the end of every LateUpdate, whether the bar is showing or not.
    /// visible = the bar is on screen this frame; alpha = how faded in it is (0 to 1).
    /// </summary>
    protected virtual void AfterBarUpdate(bool visible, float alpha) { }

    /// <summary>Called when the bar is being destroyed, so a subclass can clean up what it made.</summary>
    protected virtual void OnBarDestroyed() { }

    // --- For subclasses that add their own visuals ---

    /// <summary>The bar's centre. Children of this move, hide and show with the bar.</summary>
    protected Transform BarRoot => barRoot != null ? barRoot.transform : null;

    /// <summary>Sorting layer the bar is drawn on (the cockroach's own).</summary>
    protected int BarSortingLayerId { get; private set; }

    /// <summary>Sorting order of the bar's background. The fill is +1; use +2 or more to draw on top.</summary>
    protected int BarBaseOrder { get; private set; }

    /// <summary>The stack above the head that positions this bar.</summary>
    protected OverheadStack Stack => stack;

    // Total height including the outline.
    private float TotalHeight => barHeight + borderThickness * 2f;

    private OverheadStack stack;

    private GameObject barRoot;
    private Transform fillTransform;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;

    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    private float alpha;

    private void Awake()
    {
        stack = OverheadStack.For(gameObject);

        OnBeforeBuild();
        BuildBar();
        barRoot.SetActive(false);

        OnBarAwake();
    }

    /// <summary>
    /// Creates the bar from two stretched white sprites (background + fill).
    /// The bar is NOT a child of the cockroach, so the cockroach's scale and
    /// left/right flipping can never squash or mirror it.
    /// </summary>
    private void BuildBar()
    {
        // A 1x1 white pixel with its pivot on the LEFT edge, so scaling X grows the fill to the right.
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

        // Draw on the SAME sorting layer as the cockroach, otherwise the bar can end up
        // behind the map/water even with a high sorting order.
        SpriteRenderer cockroachSprite = GetComponentInChildren<SpriteRenderer>();
        int sortingLayerId = cockroachSprite != null ? cockroachSprite.sortingLayerID : 0;
        int baseOrder = (cockroachSprite != null ? cockroachSprite.sortingOrder : 0) + sortingOrder;

        BarSortingLayerId = sortingLayerId;
        BarBaseOrder = baseOrder;

        barRoot = new GameObject(GetType().Name + "_" + gameObject.name);

        // Background / outline.
        GameObject background = new GameObject("Background");
        background.transform.SetParent(barRoot.transform, false);
        background.transform.localPosition = new Vector3(-(barWidth * 0.5f + borderThickness), 0f, 0f);
        background.transform.localScale = new Vector3(
            barWidth + borderThickness * 2f,
            TotalHeight,
            1f);

        backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = whiteSprite;
        backgroundRenderer.sortingLayerID = sortingLayerId;
        backgroundRenderer.sortingOrder = baseOrder;

        // Fill.
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(barRoot.transform, false);
        fill.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
        fillTransform = fill.transform;

        fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = whiteSprite;
        fillRenderer.sortingLayerID = sortingLayerId;
        fillRenderer.sortingOrder = baseOrder + 1;
    }

    // LateUpdate so the bar follows the cockroach AFTER it has moved this frame.
    // Bars run in stack order (see DefaultExecutionOrder on each bar), so the bars
    // below this one have already reported if they are visible this frame.
    private void LateUpdate()
    {
        // Fade toward 1 (showing) or 0 (hidden).
        float targetAlpha = ShouldShow() ? 1f : 0f;
        float fadeTime = FadeTime;
        alpha = fadeTime > 0f
            ? Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime / fadeTime)
            : targetAlpha;

        bool visible = alpha > 0f;

        if (barRoot.activeSelf != visible)
            barRoot.SetActive(visible);

        // Hidden bars take no space in the stack; fading bars keep their space until gone.
        stack.Report(Slot, visible, TotalHeight);

        if (!visible)
        {
            AfterBarUpdate(false, 0f);
            return;
        }

        float value = Mathf.Clamp01(Value01());

        fillTransform.localScale = new Vector3(barWidth * value, barHeight, 1f);

        Color background = backgroundColor;
        background.a *= alpha;
        backgroundRenderer.color = background;

        Color fill = FillColor(value);
        fill.a *= alpha;
        fillRenderer.color = fill;

        // Sit on top of whatever is stacked below this bar.
        float centerY = stack.GetBottomY(Slot) + TotalHeight * 0.5f;
        barRoot.transform.position = new Vector3(stack.CenterX, centerY, 0f);

        AfterBarUpdate(true, alpha);
    }

    private void OnDisable()
    {
        if (barRoot != null)
            barRoot.SetActive(false);

        alpha = 0f;

        if (stack != null)
            stack.Report(Slot, false, 0f);
    }

    private void OnDestroy()
    {
        OnBarDestroyed();

        // Clean up the objects we created (e.g. when the cockroach dies and is destroyed).
        if (barRoot != null)
            Destroy(barRoot);

        if (whiteSprite != null)
            Destroy(whiteSprite);

        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}
