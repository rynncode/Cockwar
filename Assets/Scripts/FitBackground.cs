using UnityEngine;

/// <summary>
/// Fit Background: makes a background picture always cover the whole screen,
/// whatever the camera zoom or window shape, and slides it a little as the camera
/// moves across the map so it feels like it sits behind the world (parallax).
/// Put this on a SpriteRenderer object that holds the background picture.
/// The object must be at the top level of the Hierarchy (not inside another object).
/// </summary>
[DefaultExecutionOrder(200)] // runs after the camera has moved this frame
[RequireComponent(typeof(SpriteRenderer))]
public class FitBackground : MonoBehaviour
{
    [Header("Fit")]
    [Tooltip("Leave empty to use the Main Camera.")]
    public Camera targetCamera;

    [Tooltip("How much bigger than the screen the picture is drawn, so it has room to slide for the parallax without ever showing an edge. 1 = exact fit, which leaves no room to slide.")]
    [Range(1f, 1.6f)]
    public float extraSize = 1.15f;

    [Header("Parallax")]
    [Tooltip("Leave empty to find the TerrainGenerator automatically. The background slides across its full extra room as the camera goes from one end of this map to the other.")]
    public TerrainGenerator map;

    [Tooltip("0 = the picture stays glued to the screen. 1 = it slides over all of its extra room as the camera crosses the map.")]
    [Range(0f, 1f)]
    public float parallaxAmount = 0.8f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (map == null)
            map = FindFirstObjectByType<TerrainGenerator>();
    }

    private void LateUpdate()
    {
        if (targetCamera == null || spriteRenderer.sprite == null)
            return;

        // How much world the camera shows right now.
        float viewHeight = targetCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * targetCamera.aspect;

        // Scale the picture so it covers the whole view (and a bit more), keeping its shape.
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * extraSize;
        transform.localScale = new Vector3(scale, scale, 1f);

        // How far the picture can slide before one of its edges would show.
        Vector2 slideRoom = new Vector2(
            spriteSize.x * scale - viewWidth,
            spriteSize.y * scale - viewHeight) * 0.5f;

        Vector2 cameraPosition = targetCamera.transform.position;
        Vector2 shift = Vector2.zero;

        if (map != null && parallaxAmount > 0f)
        {
            // -1 to +1 across the map, measured from the middle.
            Vector2 mapCenter = map.transform.position;
            Vector2 mapHalfSize = new Vector2(map.mapWidth, map.mapHeight) * 0.5f;

            Vector2 across = new Vector2(
                Mathf.Clamp((cameraPosition.x - mapCenter.x) / mapHalfSize.x, -1f, 1f),
                Mathf.Clamp((cameraPosition.y - mapCenter.y) / mapHalfSize.y, -1f, 1f));

            // The picture slides the opposite way to the camera, like something far behind.
            shift = -Vector2.Scale(across, slideRoom) * parallaxAmount;
        }

        transform.position = new Vector3(
            cameraPosition.x + shift.x,
            cameraPosition.y + shift.y,
            transform.position.z);
    }
}
