using UnityEngine;

public class CameraController : MonoBehaviour
{
    Transform target;
    Vector3 velocity = Vector3.zero;

    [Range(0, 1)]
    public float smoothTime;

    public Vector3 positionffset;

    [Header("Zoom")]
    [Tooltip("How much each scroll notch changes the zoom. Higher = faster zoom.")]
    public float zoomSpeed = 5f;

    [Tooltip("Closest the player can zoom in (smaller = closer, since this is Orthographic Size).")]
    public float minZoom = 20f;

    [Tooltip("Furthest the player can zoom out (larger = further away).")]
    public float maxZoom = 100f;

    [Tooltip("How smoothly the zoom eases toward the target amount. 0 = instant.")]
    public float zoomSmoothTime = 0.15f;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity;

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

        Vector3 targetPosition = target.position + positionffset;
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