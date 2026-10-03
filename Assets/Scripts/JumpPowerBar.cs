using UnityEngine;

/// <summary>
/// Jump power bar: a small fill bar above the cockroach's head that only
/// appears while the jump key is held, and fills up as the jump charges.
/// Builds itself from plain sprites, so no extra assets or Canvas are needed.
/// Add this component to each cockroach next to CockroachMovement.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
public class JumpPowerBar : MonoBehaviour
{
    [Header("Size (world units)")]
    [Tooltip("Width of the bar when completely full.")]
    public float barWidth = 5f;

    [Tooltip("Height of the bar.")]
    public float barHeight = 0.7f;

    [Tooltip("Thickness of the dark outline around the bar.")]
    public float borderThickness = 0.15f;

    [Header("Position")]
    [Tooltip("How far above the cockroach's head the bar floats.")]
    public float heightAboveHead = 1.5f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

    [Tooltip("Fill color at the smallest jump.")]
    public Color lowPowerColor = Color.green;

    [Tooltip("Fill color at the biggest jump.")]
    public Color highPowerColor = Color.red;

    [Header("Drawing")]
    [Tooltip("The bar is drawn on the cockroach's own sorting layer, this many steps ABOVE the cockroach sprite. Raise it if the bar is still hidden behind other sprites.")]
    public int sortingOrder = 100;

    // Filled in automatically.
    private CockroachMovement movement;
    private Collider2D bodyCollider;

    private GameObject barRoot;
    private Transform fillTransform;
    private SpriteRenderer fillRenderer;

    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        bodyCollider = GetComponent<Collider2D>();

        BuildBar();
        barRoot.SetActive(false);
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

        barRoot = new GameObject("JumpPowerBar_" + gameObject.name);

        // Background / outline.
        GameObject background = new GameObject("Background");
        background.transform.SetParent(barRoot.transform, false);
        background.transform.localPosition = new Vector3(-(barWidth * 0.5f + borderThickness), 0f, 0f);
        background.transform.localScale = new Vector3(
            barWidth + borderThickness * 2f,
            barHeight + borderThickness * 2f,
            1f);

        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = whiteSprite;
        backgroundRenderer.color = backgroundColor;
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
    private void LateUpdate()
    {
        bool shouldShow = movement.IsChargingJump;

        if (barRoot.activeSelf != shouldShow)
            barRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        float charge = movement.JumpCharge01;

        // Fill grows from the left with the charge, and shifts from green to red.
        fillTransform.localScale = new Vector3(barWidth * charge, barHeight, 1f);
        fillRenderer.color = Color.Lerp(lowPowerColor, highPowerColor, charge);

        // Float above the top of the cockroach's collider.
        float topY = bodyCollider != null ? bodyCollider.bounds.max.y : transform.position.y;
        float centerX = bodyCollider != null ? bodyCollider.bounds.center.x : transform.position.x;
        barRoot.transform.position = new Vector3(centerX, topY + heightAboveHead, 0f);
    }

    private void OnDisable()
    {
        if (barRoot != null)
            barRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up the objects we created (e.g. when the cockroach dies and is destroyed).
        if (barRoot != null)
            Destroy(barRoot);

        if (whiteSprite != null)
            Destroy(whiteSprite);

        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}