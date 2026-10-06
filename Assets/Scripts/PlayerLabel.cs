using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Player label: a pixelated "P1" (red), "P2" (blue) ... that floats above the
/// cockroach's head so players can tell identical cockroaches apart.
/// To avoid distraction it is NOT always on: it shows briefly when this cockroach's turn
/// begins (and during the TurnManager's game intro), holds for a moment, then fades out.
/// The pixel text is generated in code, so no image assets are needed.
/// Add this component to each cockroach next to CockroachMovement.
/// </summary>
[DefaultExecutionOrder(13)] // runs after the bars, so it can sit on top of whichever are showing
[RequireComponent(typeof(CockroachMovement))]
public class PlayerLabel : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("1, 2, 3 or 4. Leave at 0 to work it out from the object name: \"Cockroach\" = P1, \"Cockroach (P2)\" = P2.")]
    [Range(0, 4)]
    public int playerNumber = 0;

    [Header("When to show")]
    [Tooltip("Show the label each time this cockroach's turn begins.")]
    public bool showAtTurnStart = true;

    [Tooltip("Seconds the label stays fully visible before it starts fading.")]
    public float holdTime = 0.6f;

    [Tooltip("Seconds the fade-out takes.")]
    public float fadeTime = 1f;

    [Header("Size (world units)")]
    [Tooltip("Size of ONE pixel of the label. The label is 9 x 7 pixels, so 0.6 gives about 5.4 x 4.2 units.")]
    public float pixelSize = 0.6f;

    [Header("Drawing")]
    [Tooltip("The label is drawn on the cockroach's own sorting layer, this many steps ABOVE the cockroach sprite.")]
    public int sortingOrder = 100;

    // Filled in automatically.
    private CockroachMovement movement;
    private OverheadStack stack;

    private GameObject labelObject;
    private SpriteRenderer labelRenderer;
    private Texture2D labelTexture;
    private Sprite labelSprite;

    private bool wasMyTurn;
    private bool showing;
    private float showElapsed;

    // --- Tiny 3x5 pixel font (only the characters we need). 1 = filled pixel. ---
    private const int GlyphWidth = 3;
    private const int GlyphHeight = 5;

    private static string[] GetGlyph(char c)
    {
        switch (c)
        {
            case 'P': return new[] { "111", "101", "111", "100", "100" };
            case '1': return new[] { "010", "110", "010", "010", "111" };
            case '2': return new[] { "111", "001", "111", "100", "111" };
            case '3': return new[] { "111", "001", "111", "001", "111" };
            case '4': return new[] { "101", "101", "111", "001", "001" };
            default:  return new[] { "000", "000", "000", "000", "000" };
        }
    }

    /// <summary>True while the label is on screen (including its fade-out).</summary>
    public bool IsShowing => showing;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        stack = OverheadStack.For(gameObject);

        if (playerNumber <= 0)
            playerNumber = DetectPlayerNumber();

        BuildLabel();
        labelObject.SetActive(false);
    }

    private void Start()
    {
        wasMyTurn = movement.isMyTurn;
    }

    /// <summary>
    /// Reads the player number from the object name, e.g. "Cockroach (P2)" gives 2.
    /// No number in the name means player 1.
    /// </summary>
    private int DetectPlayerNumber()
    {
        Match match = Regex.Match(gameObject.name, @"\(P(\d)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            return Mathf.Clamp(number, 1, 4);

        return 1;
    }

    /// <summary>The color for a player number (1 red, 2 blue, 3 green, 4 yellow). Also used by DistanceHud.</summary>
    public static Color GetPlayerColor(int number)
    {
        switch (number)
        {
            case 1: return new Color(0.95f, 0.15f, 0.15f); // red
            case 2: return new Color(0.2f, 0.5f, 1f);      // blue
            case 3: return new Color(0.2f, 0.85f, 0.3f);   // green
            default: return new Color(1f, 0.85f, 0.15f);   // yellow
        }
    }

    /// <summary>
    /// Draws "P" + number into a tiny texture with a dark 1-pixel outline,
    /// so it stays readable on any background.
    /// </summary>
    private void BuildLabel()
    {
        string text = "P" + Mathf.Clamp(playerNumber, 1, 4);

        int textWidth = text.Length * GlyphWidth + (text.Length - 1); // 1 pixel gap between letters
        int texWidth = textWidth + 2;                                 // +1 pixel outline on each side
        int texHeight = GlyphHeight + 2;

        // Work out which pixels are filled. y = 0 is the BOTTOM row.
        bool[,] filled = new bool[texWidth, texHeight];

        for (int i = 0; i < text.Length; i++)
        {
            string[] glyph = GetGlyph(text[i]);
            int startX = 1 + i * (GlyphWidth + 1);

            for (int row = 0; row < GlyphHeight; row++)
            {
                int y = 1 + (GlyphHeight - 1 - row); // glyph rows are written top to bottom
                for (int col = 0; col < GlyphWidth; col++)
                {
                    if (glyph[row][col] == '1')
                        filled[startX + col, y] = true;
                }
            }
        }

        Color32 fillColor = GetPlayerColor(playerNumber);
        Color32 outlineColor = new Color32(0, 0, 0, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        Color32[] pixels = new Color32[texWidth * texHeight];

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                if (filled[x, y])
                    pixels[y * texWidth + x] = fillColor;
                else if (TouchesFilledPixel(filled, x, y, texWidth, texHeight))
                    pixels[y * texWidth + x] = outlineColor;
                else
                    pixels[y * texWidth + x] = clear;
            }
        }

        labelTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        labelTexture.filterMode = FilterMode.Point;   // keep the pixels sharp
        labelTexture.wrapMode = TextureWrapMode.Clamp;
        labelTexture.SetPixels32(pixels);
        labelTexture.Apply();

        // Pivot at the bottom-centre, 1 pixel = 1 unit (scaled up by pixelSize below).
        labelSprite = Sprite.Create(
            labelTexture,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0f),
            1f, 0, SpriteMeshType.FullRect);

        // Not a child of the cockroach, so its scale / left-right flip can't squash or mirror the label.
        labelObject = new GameObject("PlayerLabel_" + gameObject.name);
        labelObject.transform.localScale = new Vector3(pixelSize, pixelSize, 1f);

        labelRenderer = labelObject.AddComponent<SpriteRenderer>();
        labelRenderer.sprite = labelSprite;

        // Same sorting layer as the cockroach, a bit above it (same fix as the jump bar).
        SpriteRenderer cockroachSprite = GetComponentInChildren<SpriteRenderer>();
        labelRenderer.sortingLayerID = cockroachSprite != null ? cockroachSprite.sortingLayerID : 0;
        labelRenderer.sortingOrder = (cockroachSprite != null ? cockroachSprite.sortingOrder : 0) + sortingOrder;
    }

    private static bool TouchesFilledPixel(bool[,] filled, int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                if (filled[nx, ny])
                    return true;
            }
        }

        return false;
    }

    /// <summary>Starts (or restarts) the show-then-fade sequence.</summary>
    public void ShowLabel()
    {
        showing = true;
        showElapsed = 0f;
        labelObject.SetActive(true);
        SetAlpha(1f);
    }

    // LateUpdate so the label follows the cockroach AFTER it has moved this frame.
    private void LateUpdate()
    {
        // Turn just started for this cockroach: show the label again.
        bool isMyTurn = movement.isMyTurn;
        if (showAtTurnStart && isMyTurn && !wasMyTurn)
            ShowLabel();
        wasMyTurn = isMyTurn;

        if (!showing)
        {
            stack.Report(OverheadStack.SlotLabel, false, 0f);
            return;
        }

        showElapsed += Time.deltaTime;

        // Fully visible for holdTime, then fade linearly to nothing over fadeTime.
        float alpha = 1f;
        if (showElapsed > holdTime)
            alpha = 1f - (showElapsed - holdTime) / Mathf.Max(0.01f, fadeTime);

        if (alpha <= 0f)
        {
            showing = false;
            labelObject.SetActive(false);
            stack.Report(OverheadStack.SlotLabel, false, 0f);
            return;
        }

        SetAlpha(alpha);

        // Sit on top of whichever bars (health, stamina, jump power) are showing below.
        float labelHeight = labelTexture.height * pixelSize;
        stack.Report(OverheadStack.SlotLabel, true, labelHeight);

        labelObject.transform.position = new Vector3(
            stack.CenterX,
            stack.GetBottomY(OverheadStack.SlotLabel),
            0f);
    }

    private void SetAlpha(float alpha)
    {
        labelRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }

    private void OnDisable()
    {
        if (labelObject != null)
            labelObject.SetActive(false);

        showing = false;

        if (stack != null)
            stack.Report(OverheadStack.SlotLabel, false, 0f);
    }

    private void OnDestroy()
    {
        // Clean up the objects we created (e.g. when the cockroach dies and is destroyed).
        if (labelObject != null)
            Destroy(labelObject);

        if (labelSprite != null)
            Destroy(labelSprite);

        if (labelTexture != null)
            Destroy(labelTexture);
    }
}
