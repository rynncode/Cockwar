using UnityEngine;

public class CameraController : MonoBehaviour
{
    Transform target;
    Vector3 velocity = Vector3.zero;

    [Range(0, 1)]
    public float smoothTime;

    public Vector3 positionffset;

    [Header("Dead Zone")]
    [Tooltip("When on, the camera stays still while the cockroach moves around inside a box in the middle of the screen, and only follows once it reaches the edge of that box.")]
    public bool useDeadZone = true;

    [Tooltip("Width of the dead zone box as a fraction of the screen width. 0.3 = the middle 30%. 0 = camera always centred on the cockroach.")]
    [Range(0f, 1f)]
    public float deadZoneWidth = 0.3f;

    [Tooltip("Height of the dead zone box as a fraction of the screen height.")]
    [Range(0f, 1f)]
    public float deadZoneHeight = 0.3f;

    [Tooltip("When on, once the cockroach stands still the camera slowly re-centres on it, so the dead zone is evenly spaced around it again (equal room left and right).")]
    public bool recenterWhenStill = true;

    [Tooltip("Seconds the cockroach must stand still before the camera starts re-centring.")]
    public float recenterDelay = 0.5f;

    [Tooltip("How long the re-centring takes. Bigger = slower, gentler.")]
    public float recenterSmoothTime = 0.6f;

    [Header("Zoom")]
    [Tooltip("How much each scroll notch changes the zoom. Higher = faster zoom.")]
    public float zoomSpeed = 5f;

    [Tooltip("Closest the player can zoom in (smaller = closer, since this is Orthographic Size).")]
    public float minZoom = 20f;

    [Tooltip("Furthest the player can zoom out (larger = further away).")]
    public float maxZoom = 100f;

    [Tooltip("How smoothly the zoom eases toward the target amount. 0 = instant.")]
    public float zoomSmoothTime = 0.15f;

    // The point (in cockroach terms) the camera is centred on. The dead zone slides this
    // point around; the camera then follows it. Reset to the cockroach whenever the target changes.
    private Vector2 focusPoint;

    // Used to notice when the cockroach is standing still, for re-centring.
    private Vector2 lastTargetPoint;
    private float stillTimer;
    private Vector2 recenterVelocity;
    private const float StillSpeed = 1f; // world units per second; slower than this counts as still

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity;

    // A hidden, reused point for holding the camera on a fixed world position
    // (e.g. an explosion) instead of following a real moving Transform.
    private Transform staticPointHolder;

    // Intro pan: while active, pan and zoom use introSmoothTime so the camera
    // visibly glides instead of snapping (the normal smoothTime can be 0).
    private bool introActive;
    private float introSmoothTime;

    private void Awake()
    {
        // Fallback only, in case nothing has called SetTarget yet
        // (e.g. testing this scene without a TurnManager in it).
        GameObject fallback = GameObject.FindGameObjectWithTag("Player");
        if (fallback != null)
        {
            target = fallback.transform;
            focusPoint = target.position;
            lastTargetPoint = focusPoint;
        }

        cam = GetComponent<Camera>();
        if (cam != null)
        {
            targetZoom = cam.orthographicSize;
        }
        else
        {
            Debug.LogWarning("CameraController: no Camera component found on this object. Zoom will not work.");
        }
    }

    private void Update()
    {
        if (cam == null) return;

        float scrollAmount = Input.mouseScrollDelta.y;
        if (scrollAmount != 0f)
        {
            // Scrolling up (positive) zooms in, so it subtracts from the size.
            targetZoom -= scrollAmount * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    private void LateUpdate()
    {
        if (cam != null)
        {
            float zoomSmooth = introActive ? introSmoothTime : zoomSmoothTime;
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmooth);
        }

        if (target == null) return;

        UpdateFocusPoint();

        Vector3 targetPosition = new Vector3(focusPoint.x, focusPoint.y, target.position.z) + positionffset;
        float panSmooth = introActive ? introSmoothTime : smoothTime;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, panSmooth);
    }

    /// <summary>
    /// Step 10: switches which cockroach the camera follows.
    /// Called by TurnManager at the start of each turn, so the camera pans
    /// to the new active player instead of staying on whoever had it last.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        // A new player gets the camera centred on them, not left at the edge of a dead zone.
        if (target != null)
        {
            focusPoint = target.position;
            lastTargetPoint = focusPoint;
            stillTimer = 0f;
            recenterVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Dead zone: the focus point stays put while the cockroach is inside the box,
    /// and is pushed along just enough to keep the cockroach on the box's edge once it leaves.
    /// </summary>
    private void UpdateFocusPoint()
    {
        Vector2 targetPoint = target.position;

        if (!useDeadZone || cam == null)
        {
            focusPoint = targetPoint;
            return;
        }

        // Is the cockroach standing still? (Used for re-centring below.)
        float movedDistance = (targetPoint - lastTargetPoint).magnitude;
        lastTargetPoint = targetPoint;

        bool isStill = Time.deltaTime > 0f && movedDistance / Time.deltaTime < StillSpeed;
        stillTimer = isStill ? stillTimer + Time.deltaTime : 0f;

        // Half the visible area in world units. Using the current zoom means the box
        // scales with zooming, so it always covers the same part of the screen.
        float halfScreenHeight = cam.orthographicSize;
        float halfScreenWidth = halfScreenHeight * cam.aspect;

        float halfZoneWidth = halfScreenWidth * deadZoneWidth;
        float halfZoneHeight = halfScreenHeight * deadZoneHeight;

        Vector2 offset = targetPoint - focusPoint;

        if (offset.x > halfZoneWidth)
            focusPoint.x += offset.x - halfZoneWidth;
        else if (offset.x < -halfZoneWidth)
            focusPoint.x += offset.x + halfZoneWidth;

        if (offset.y > halfZoneHeight)
            focusPoint.y += offset.y - halfZoneHeight;
        else if (offset.y < -halfZoneHeight)
            focusPoint.y += offset.y + halfZoneHeight;

        // Standing still for a moment: ease the focus back onto the cockroach, so it ends up
        // in the middle of the dead zone with equal room on every side.
        if (recenterWhenStill && stillTimer >= recenterDelay)
        {
            focusPoint = Vector2.SmoothDamp(focusPoint, targetPoint, ref recenterVelocity, Mathf.Max(0.01f, recenterSmoothTime));
        }
        else
        {
            recenterVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Holds the camera on a fixed world position instead of following a
    /// moving Transform — for example, lingering on an explosion after the
    /// Transform that caused it (a projectile) has already been destroyed.
    /// Call SetTarget again later to go back to following something.
    /// </summary>
    public void SetTargetPosition(Vector3 worldPosition)
    {
        if (staticPointHolder == null)
        {
            GameObject holder = new GameObject("CameraStaticPoint (hidden)");
            holder.hideFlags = HideFlags.HideInHierarchy;
            staticPointHolder = holder.transform;
        }

        staticPointHolder.position = worldPosition;
        SetTarget(staticPointHolder);
    }

    /// <summary>The zoom (Orthographic Size) the camera is heading for.</summary>
    public float TargetZoom => targetZoom;

    /// <summary>
    /// Sets the zoom the camera eases toward. Kept inside minZoom / maxZoom.
    /// </summary>
    public void SetZoom(float size)
    {
        targetZoom = Mathf.Clamp(size, minZoom, maxZoom);
    }

    /// <summary>
    /// Start of the game intro: pan and zoom glide with the given smooth time
    /// (bigger = slower and smoother) until EndIntro is called.
    /// </summary>
    public void BeginIntro(float smoothTime)
    {
        introActive = true;
        introSmoothTime = Mathf.Max(0.01f, smoothTime);
    }

    /// <summary>Back to the normal camera smoothing.</summary>
    public void EndIntro()
    {
        introActive = false;
    }
}