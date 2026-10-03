using UnityEngine;
using TMPro;

/// <summary>
/// Points a UI arrow toward a target (the opponent, for now) whenever that
/// target is off-screen, and hides itself once the target comes back into view.
/// Also shows a rough distance readout ("41 m" style).
///
/// This script is self-contained: it doesn't know anything about turns or
/// cockroaches. TurnManager just calls SetTarget(...) each time the turn
/// changes, the same way it already calls CameraController.SetTarget(...).
///
/// Setup (do this once in the Editor):
///  1. Under your HUD Canvas, make a UI Image for the indicator bubble (this
///     is the object this script goes on). Give it a child Image called
///     "Arrow" (the little triangle/arrow), and optionally a child
///     TextMeshProUGUI called "DistanceText".
///  2. Add a CanvasGroup component to the same object this script is on.
///     That's what hides/shows it (alpha 0/1) without disabling the
///     GameObject — important, because a disabled GameObject would stop
///     this script from ever checking again.
///  3. Drag all the pieces into the fields below.
///  4. Leave Target and Viewer empty — TurnManager fills those in at runtime.
/// </summary>
public class OffscreenIndicator : MonoBehaviour
{
    [Header("Who To Track")]
    [Tooltip("The thing this indicator points at. TurnManager sets this automatically each turn — leave empty here.")]
    public Transform target;

    [Tooltip("Where to measure the displayed distance FROM (usually the active player). Optional — if left empty, distance is measured from the camera instead.")]
    public Transform viewer;

    [Header("References")]
    [Tooltip("The camera used to work out what's on/off screen. Defaults to Camera.main if left empty.")]
    public Camera cam;

    [Tooltip("The Canvas this indicator lives under. Auto-found on Awake if left empty.")]
    public Canvas canvas;

    [Tooltip("This object's own RectTransform. Auto-found on Awake if left empty.")]
    public RectTransform indicatorRect;

    [Tooltip("Fades this in/out instead of disabling the GameObject, so the script keeps running while hidden. Auto-found on Awake if left empty.")]
    public CanvasGroup canvasGroup;

    [Tooltip("The arrow child Image's RectTransform, rotated to point at the target. Leave empty if you don't have one.")]
    public RectTransform arrow;

    [Tooltip("Optional distance readout, e.g. a TextMeshProUGUI showing '41 m'.")]
    public TextMeshProUGUI distanceText;

    [Header("Layout")]
    [Tooltip("How far in from the screen edge the indicator stays, in pixels.")]
    public float edgePadding = 60f;

    [Header("Distance Display")]
    [Tooltip("Your world scale is large (camera zoom 20-100, projectile scale ~20), so raw world-unit distances look huge on screen. This divides the raw distance down to a nicer-looking number. Tune it after testing — there's no 'correct' value, just whatever reads well.")]
    public float worldUnitsPerDisplayUnit = 20f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (indicatorRect == null) indicatorRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            Debug.LogWarning("OffscreenIndicator: no CanvasGroup found on " + name + ". Add one so the indicator can hide itself.");
        }
    }

    /// <summary>
    /// Called by TurnManager at the start of each turn. newViewer is optional —
    /// pass the active player so the distance readout measures from them.
    /// </summary>
    public void SetTarget(Transform newTarget, Transform newViewer = null)
    {
        target = newTarget;
        viewer = newViewer;
    }

    private void LateUpdate()
    {
        if (target == null || cam == null || canvasGroup == null || indicatorRect == null)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            return;
        }

        Vector3 viewportPos = cam.WorldToViewportPoint(target.position);

        bool onScreen = viewportPos.z > 0f
            && viewportPos.x > 0f && viewportPos.x < 1f
            && viewportPos.y > 0f && viewportPos.y < 1f;

        if (onScreen)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        canvasGroup.alpha = 1f;

        // If the target is behind the camera, flip the point so the arrow still
        // points the right way instead of backwards. Shouldn't normally happen
        // in a 2D side-on game, but costs nothing to guard against.
        if (viewportPos.z < 0f)
        {
            viewportPos.x = 1f - viewportPos.x;
            viewportPos.y = 1f - viewportPos.y;
        }

        // Direction from the centre of the screen to the target, in viewport space (0-1).
        Vector2 centre = new Vector2(0.5f, 0.5f);
        Vector2 dir = ((Vector2)viewportPos - centre).normalized;

        // Walk from the centre toward the target until we hit the screen edge,
        // inset by edgePadding (converted from pixels into a viewport fraction).
        float paddingX = edgePadding / Mathf.Max(1f, Screen.width);
        float paddingY = edgePadding / Mathf.Max(1f, Screen.height);

        float maxX = 0.5f - paddingX;
        float maxY = 0.5f - paddingY;

        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? maxX / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? maxY / Mathf.Abs(dir.y) : float.MaxValue;
        float scale = Mathf.Min(scaleX, scaleY);

        Vector2 clampedViewport = centre + dir * scale;

        // Convert that viewport position into an actual screen pixel position,
        // then into the Canvas's local space so the RectTransform can use it.
        Vector2 screenPos = new Vector2(clampedViewport.x * Screen.width, clampedViewport.y * Screen.height);

        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCam, out Vector2 localPoint))
        {
            indicatorRect.anchoredPosition = localPoint;
        }

        // Rotate the arrow to point from the centre toward the target.
        // Assumes the arrow art points to the right (0 degrees) by default —
        // if yours points up instead, add/subtract 90 here.
        if (arrow != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (distanceText != null)
        {
            Vector3 fromPosition = viewer != null ? viewer.position : cam.transform.position;
            float worldDistance = Vector2.Distance(fromPosition, target.position);
            float displayDistance = worldDistance / Mathf.Max(0.0001f, worldUnitsPerDisplayUnit);
            distanceText.text = Mathf.RoundToInt(displayDistance) + " m";
        }
    }
}
