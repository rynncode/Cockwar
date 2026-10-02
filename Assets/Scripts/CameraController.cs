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
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }

        if (target == null) return;

        Vector3 targetPosition = target.position + positionffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
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
}


