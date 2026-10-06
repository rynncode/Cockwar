using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Distance HUD: while it is someone's turn, shows at the top of the screen how far
/// away each opposing player is, in meters, with an arrow for which way to look.
/// Example (player 1 to move):   P2  42 m >>
/// Builds its own canvas and text in code, so there is nothing to set up.
/// Add this component to any object, for example the TurnManager.
/// </summary>
public class DistanceHud : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to find the TurnManager in the scene automatically.")]
    public TurnManager turnManager;

    [Header("Distance")]
    [Tooltip("How many world units make 1 meter. Distances are divided by this. 10 means a 420-unit gap shows as 42 m. Raise it for smaller numbers, lower it for bigger ones.")]
    public float unitsPerMeter = 10f;

    [Tooltip("Add an arrow showing which side the opponent is on: >> means to the right, << to the left.")]
    public bool showDirectionArrows = true;

    [Header("Look")]
    public float fontSize = 36f;

    [Tooltip("Gap in pixels between the top of the screen and the text (at 1920x1080).")]
    public float topMargin = 30f;

    [Tooltip("Draw order of the HUD canvas. Higher = on top of other canvases.")]
    public int canvasSortingOrder = 50;

    [Header("Update")]
    [Tooltip("Seconds between refreshes. The text does not need to change every frame.")]
    public float updateInterval = 0.1f;

    private GameObject canvasObject;
    private TextMeshProUGUI text;
    private RectTransform textRect;
    private readonly StringBuilder builder = new StringBuilder();
    private string lastText = null;
    private float updateTimer;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        BuildHud();
    }

    /// <summary>Creates a screen-space canvas with one line of centered text at the top.</summary>
    private void BuildHud()
    {
        canvasObject = new GameObject("DistanceHud_Canvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        // Scale with the screen so the text is the same relative size at any resolution.
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("DistanceText");
        textObject.transform.SetParent(canvasObject.transform, false);

        text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Top;
        text.richText = true;
        text.raycastTarget = false; // never blocks clicks

        // Dark outline so the text is readable on any background.
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 255);

        // Anchored to the top-centre of the screen.
        RectTransform rect = text.rectTransform;
        textRect = rect;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -topMargin);
        rect.sizeDelta = new Vector2(900f, 220f);

        text.text = "";
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer > 0f)
            return;

        updateTimer = updateInterval;
        Refresh();
    }

    private void Refresh()
    {
        // Sit below the turn timer, which also lives at the top middle.
        textRect.anchoredPosition = new Vector2(0f, -(topMargin + TurnTimerHud.ReservedTopSpace));

        CockroachMovement current = turnManager != null ? turnManager.CurrentPlayer : null;

        // No turn running (intro, or game over): show nothing.
        if (current == null)
        {
            SetText("");
            return;
        }

        builder.Clear();

        for (int i = 0; i < turnManager.players.Count; i++)
        {
            CockroachMovement other = turnManager.players[i];

            if (other == null || other == current || !IsAlive(other))
                continue;

            Vector2 difference = other.transform.position - current.transform.position;
            int meters = Mathf.RoundToInt(difference.magnitude / Mathf.Max(0.0001f, unitsPerMeter));

            // The opponent's own name and color, same as their label above their head.
            int number = i + 1;
            Color color = Color.white;

            PlayerLabel label = other.GetComponent<PlayerLabel>();
            if (label != null)
            {
                number = label.playerNumber;
                color = PlayerLabel.GetPlayerColor(label.playerNumber);
            }

            if (builder.Length > 0)
                builder.Append('\n');

            string hex = ColorUtility.ToHtmlStringRGB(color);

            // <noparse> stops the text system treating "<<" as the start of a tag.
            if (showDirectionArrows && difference.x < 0f)
                builder.Append("<noparse><<</noparse> ");

            builder.Append("<color=#").Append(hex).Append(">P").Append(number).Append("</color>  ");
            builder.Append(meters).Append(" m");

            if (showDirectionArrows && difference.x >= 0f)
                builder.Append(" >>");
        }

        SetText(builder.ToString());
    }

    // Only touches the text when it actually changed, so the UI isn't rebuilt for nothing.
    private void SetText(string newText)
    {
        if (newText == lastText)
            return;

        lastText = newText;
        text.text = newText;
    }

    /// <summary>Alive = still in the scene and not at 0 health.</summary>
    private bool IsAlive(CockroachMovement player)
    {
        if (!player.gameObject.activeInHierarchy)
            return false;

        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);
    }
}
