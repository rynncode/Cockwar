using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverSequence : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Background music AudioSource to stop when the game ends.")]
    public AudioSource bgmSource;

    [Header("Camera")]
    [Tooltip("Zoom on the winner (Orthographic Size). Smaller = closer.")]
    public float zoom = 30f;

    [Tooltip("How smooth the pan and zoom to the winner are. Bigger = slower and softer.")]
    public float smoothTime = 0.8f;

    [Header("Winner text")]
    [Tooltip("The text that appears above the winner's head.")]
    public string winnerText = "WINNER";

    [Tooltip("Seconds after the camera starts moving before the text pops in.")]
    public float textDelay = 1f;

    [Tooltip("World size of ONE pixel of the text's font. 0.7 makes WINNER about 21 units wide.")]
    public float textPixelSize = 0.7f;

    [Tooltip("Gap between the top of the winner's head (and any bars) and the text.")]
    public float textGapAboveHead = 2f;

    public Color winnerColor = new Color(1f, 0.82f, 0.2f);

    [Tooltip("How many font pixels the text bobs up and down. 0 = still.")]
    public int bobPixels = 2;

    [Header("Panel")]
    [Tooltip("Seconds after the winner text appears before the panel comes up.")]
    public float panelDelay = 3f;

    [Tooltip("Your game-over panel: a duplicate of the Pause panel. Leave it switched off in the Hierarchy; this script switches it on.")]
    public GameObject panel;

    [Tooltip("The panel's Retry button.")]
    public Button retryButton;

    [Tooltip("The panel's Home button.")]
    public Button homeButton;

    [Tooltip("Optional: a text on the panel for the title. Leave empty if your title is a picture.")]
    public TMP_Text titleText;

    [Tooltip("What the optional title text says.")]
    public string titleString = "GAME OVER";

    [Tooltip("Build index of the main menu scene (File > Build Profiles / Build Settings). Your Mainmenu script loads scene 1 to play, so the menu is 0.")]
    public int homeSceneIndex = 0;

    [Tooltip("Seconds the panel takes to fade and pop in.")]
    public float panelFadeTime = 0.35f;

    private bool started;

    /// <summary>True once the match has ended and the winner sequence is playing. GameMenu hides the pause button then.</summary>
    public bool HasStarted => started;

    private PixelNumber winnerLabel;
    private Transform winnerTransform;
    private OverheadStack winnerStack;
    private float labelShownAt;

    private const float PopInTime = 0.25f;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void Begin(CockroachMovement winner)
    {
        if (started) return;
        started = true;

        // Stop background music as soon as the sequence begins
        if (bgmSource != null)
            bgmSource.Stop();

        StartCoroutine(Run(winner));
    }

    private IEnumerator Run(CockroachMovement winner)
    {
        if (winner != null)
        {
            CameraController cam = FindFirstObjectByType<CameraController>();
            if (cam != null)
            {
                cam.BeginIntro(smoothTime);
                cam.SetZoom(zoom);
                cam.SetTarget(winner.transform);
            }

            yield return new WaitForSeconds(textDelay);
            ShowWinnerText(winner);
        }

        yield return new WaitForSeconds(panelDelay);

        yield return ShowPanel();
    }

    private void ShowWinnerText(CockroachMovement winner)
    {
        SpriteRenderer winnerSprite = winner.GetComponent<SpriteRenderer>();
        if (winnerSprite == null)
            winnerSprite = winner.GetComponentInChildren<SpriteRenderer>();

        int layerId = winnerSprite != null ? winnerSprite.sortingLayerID : 0;
        int order = (winnerSprite != null ? winnerSprite.sortingOrder : 0) + 120;

        winnerLabel = new PixelNumber(null, "WinnerText", layerId, order, textPixelSize);
        winnerLabel.SetText(winnerText);
        winnerLabel.SetColor(winnerColor);

        winnerTransform = winner.transform;
        winnerStack = OverheadStack.For(winner.gameObject);
        labelShownAt = Time.time;
    }

    private void LateUpdate()
    {
        if (winnerLabel == null || winnerTransform == null)
            return;

        float age = Time.time - labelShownAt;

        float t = Mathf.Clamp01(age / PopInTime);
        float scale = t < 1f ? 1.2f * Mathf.Sin(t * Mathf.PI * 0.5f) : 1f;
        winnerLabel.Transform.localScale = new Vector3(scale, scale, 1f);

        float halfHeight = PixelNumber.HeightInPixels * 0.5f * textPixelSize;
        float baseY = winnerStack.GetBottomY(OverheadStack.SlotLabel) + textGapAboveHead + halfHeight;
        float bob = Mathf.Round(Mathf.Sin(age * 3f) * bobPixels) * textPixelSize;

        winnerLabel.Transform.position = new Vector3(winnerStack.CenterX, baseY + bob, 0f);
    }

    private IEnumerator ShowPanel()
    {
        if (panel == null)
        {
            Debug.LogWarning("GameOverSequence: no Panel is assigned.");
            yield break;
        }

        if (titleText != null)
            titleText.text = titleString;

        SetUpButton(retryButton, Retry);
        SetUpButton(homeButton, Home);

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        group.alpha = 0f;
        group.interactable = false;
        panel.SetActive(true);

        Vector3 fullScale = panel.transform.localScale;
        float elapsed = 0f;

        while (elapsed < panelFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, panelFadeTime));
            float eased = 1f - (1f - t) * (1f - t);

            group.alpha = eased;
            panel.transform.localScale = fullScale * Mathf.Lerp(0.85f, 1f, eased);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        panel.transform.localScale = fullScale;
    }

    private void SetUpButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

        button.onClick.AddListener(action);
    }

    private void Retry()
    {
        Time.timeScale = 1f;
        MatchSetup.KeepModeForRestart();   // a rematch keeps 1 VS 1 / 2 VS 2
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Home()
    {
        Time.timeScale = 1f;
        MatchSetup.ForgetMode();
        SceneManager.LoadScene(homeSceneIndex);
    }

    private void OnDestroy()
    {
        if (winnerLabel != null)
            winnerLabel.Destroy();
    }
}