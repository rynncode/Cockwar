using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-game pause button, pause menu and settings menu, built entirely in code like the
/// other HUDs, on a canvas drawn above all of them (so the turn timer can't cover it).
/// It adds itself to any scene that has a TurnManager, so there is nothing to set up.
/// In the Main Menu (no TurnManager) it is created by the Settings button (Mainmenu.OpenSettings)
/// and shows only the settings panel: no pause button or pause menu, and nothing is frozen.
///
///  - Pause button in the top-left corner, or ESC.
///  - Pause menu: Resume, Restart, Main Menu, Mute, Settings.
///  - Settings: Master / Music / SFX volume, and rebinding the gameplay keys.
///
/// Art comes from Resources/MenuArt (the PAUSE PANEL, Options panel and SLIDERS sheets).
/// Settings are stored by GameSettings, so they persist between matches.
/// </summary>
public class GameMenu : MonoBehaviour
{
    /// <summary>True while the pause or settings menu is open. Gameplay scripts ignore input while this is set.</summary>
    public static bool IsPaused { get; private set; }

    public static GameMenu Instance { get; private set; }

    [Tooltip("Draw order of the menu canvas. Above the weapon panel (60) and turn timer (55).")]
    public int canvasSortingOrder = 100;

    [Tooltip("Scene loaded by the Main Menu button.")]
    public string mainMenuScene = "Main Menu";

    // Layout, in UI units at 1920x1080.
    private const float ScreenMargin = 20f;
    private const float PanelPixelSize = 4f;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color PausePanelColor = new Color(0.08f, 0.12f, 0.08f, 0.95f);
    private static readonly Color ButtonColor = new Color(0.14f, 0.22f, 0.13f, 1f);
    private static readonly Color LightText = new Color(0.9f, 0.96f, 0.82f, 1f);
    private static readonly Color DarkText = new Color(0.08f, 0.13f, 0.08f, 1f);
    private static readonly Color FillColor = new Color(0.47f, 0.78f, 0.3f, 1f);
    private static readonly Color WaitingColor = new Color(1f, 0.82f, 0.25f, 1f);

    private MenuArt art;
    private GameOverSequence gameOver;

    // True in a scene without a match (the Main Menu): only the settings panel, opened by the
    // scene's Settings button. No pause button, no pause menu, and nothing is ever frozen.
    private bool menuMode;

    private GameObject pauseButton;
    private GameObject pauseRoot;
    private GameObject settingsRoot;

    private Image muteIcon;
    private TextMeshProUGUI muteLabel;
    private readonly List<TextMeshProUGUI> keyLabels = new List<TextMeshProUGUI>();

    // The action waiting for a new key (-1 = not rebinding).
    private int rebinding = -1;
    private float timeScaleBeforePause = 1f;

    private static KeyCode[] keyboardKeys;

    // =====================================================================
    // Adding itself to game scenes
    // =====================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookSceneLoads()
    {
        IsPaused = false;
        SceneManager.sceneLoaded -= OnSceneLoaded; // in case domain reload is off in the editor
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null && Instance.gameObject.scene == scene) return;
        if (FindAnyObjectByType<TurnManager>() == null) return; // main menu, loading screen, ...

        new GameObject("GameMenu").AddComponent<GameMenu>();
    }

    /// <summary>
    /// Opens the settings panel in a scene without a match (the Main Menu's Settings button calls
    /// this through Mainmenu.OpenSettings). Creates the menu the first time.
    /// </summary>
    public static void OpenSettings()
    {
        if (Instance == null) new GameObject("GameMenu").AddComponent<GameMenu>();
        Instance.ShowSettings();
    }

    private void Awake()
    {
        Instance = this;
        art = MenuArt.Load();
        gameOver = FindAnyObjectByType<GameOverSequence>();
        menuMode = FindAnyObjectByType<TurnManager>() == null;

        // Buttons need an EventSystem; the scene normally has one already.
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Build();
        if (!menuMode) HideOldPauseButtons();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // Leaving the scene while paused must not leave the next scene frozen or silent.
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    /// <summary>
    /// The scene's hand-built pause button (calling PauseMenu.Pause) sits under the turn timer.
    /// This menu replaces it, so hide it rather than show two pause buttons.
    /// </summary>
    private static void HideOldPauseButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) is PauseMenu && button.onClick.GetPersistentMethodName(i) == "Pause")
                {
                    button.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    // =====================================================================
    // Opening and closing
    // =====================================================================

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true; // freezes music and sound effects together

        ShowPauseMenu();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        rebinding = -1;

        Time.timeScale = timeScaleBeforePause;
        AudioListener.pause = false;

        if (pauseRoot != null) pauseRoot.SetActive(false);
        settingsRoot.SetActive(false);
    }

    public void Restart()
    {
        MatchSetup.KeepModeForRestart();   // same 1 VS 1 / 2 VS 2 again, without asking
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        MatchSetup.ForgetMode();   // the next match asks for the mode again
        Resume();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void ShowPauseMenu()
    {
        rebinding = -1;
        if (pauseRoot != null) pauseRoot.SetActive(true);
        settingsRoot.SetActive(false);
        RefreshMute();
    }

    public void ShowSettings()
    {
        if (pauseRoot != null) pauseRoot.SetActive(false);
        settingsRoot.SetActive(true);
        RefreshKeyLabels();
    }

    /// <summary>The settings panel's X (and ESC): back to the pause menu in a match, or just closed in the Main Menu.</summary>
    private void CloseSettings()
    {
        if (!menuMode)
        {
            ShowPauseMenu();
            return;
        }

        rebinding = -1;
        settingsRoot.SetActive(false);
    }

    private void ToggleMute()
    {
        GameSettings.Muted = !GameSettings.Muted;
        RefreshMute();
    }

    private void RefreshMute()
    {
        bool muted = GameSettings.Muted;
        if (muteLabel != null) muteLabel.text = muted ? "UNMUTE" : "MUTE";
        if (muteIcon != null && art != null)
            muteIcon.sprite = muted ? art.musicOffIcon : art.musicOnIcon;
    }

    // =====================================================================
    // Input (runs while paused: Update still runs at time scale 0)
    // =====================================================================

    private void Update()
    {
        ClearSelection();

        bool matchOver = gameOver != null && gameOver.HasStarted;
        if (pauseButton != null) pauseButton.SetActive(!IsPaused && !matchOver);

        if (rebinding >= 0)
        {
            ListenForRebind();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Main Menu: ESC only closes the settings panel.
        if (menuMode)
        {
            if (settingsRoot.activeSelf) CloseSettings();
            return;
        }

        if (settingsRoot.activeSelf) ShowPauseMenu();
        else if (IsPaused) Resume();
        else if (!matchOver) Pause();
    }

    private void StartRebind(int action)
    {
        rebinding = action;
        RefreshKeyLabels();
    }

    private void ListenForRebind()
    {
        if (keyboardKeys == null)
        {
            // Keyboard keys only: the click that started the rebind must not become the new key.
            List<KeyCode> list = new List<KeyCode>();
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                if (key > KeyCode.None && key < KeyCode.Mouse0) list.Add(key);
            keyboardKeys = list.ToArray();
        }

        foreach (KeyCode key in keyboardKeys)
        {
            if (!Input.GetKeyDown(key)) continue;

            // ESC cancels; it stays reserved for the pause menu.
            if (key != KeyCode.Escape) GameSettings.SetKey((GameAction)rebinding, key);

            rebinding = -1;
            RefreshKeyLabels();
            return;
        }
    }

    private void RefreshKeyLabels()
    {
        for (int i = 0; i < keyLabels.Count; i++)
        {
            bool waiting = i == rebinding;
            keyLabels[i].text = waiting ? "PRESS A KEY" : KeyName(GameSettings.Key((GameAction)i));
            keyLabels[i].color = waiting ? WaitingColor : LightText;
        }
    }

    private static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Return: return "ENTER";
            case KeyCode.LeftShift: return "L-SHIFT";
            case KeyCode.RightShift: return "R-SHIFT";
            case KeyCode.LeftControl: return "L-CTRL";
            case KeyCode.RightControl: return "R-CTRL";
            case KeyCode.LeftAlt: return "L-ALT";
            case KeyCode.RightAlt: return "R-ALT";
            case KeyCode.UpArrow: return "UP";
            case KeyCode.DownArrow: return "DOWN";
            case KeyCode.LeftArrow: return "LEFT";
            case KeyCode.RightArrow: return "RIGHT";
        }

        string name = key.ToString();
        if (name.StartsWith("Alpha")) name = name.Substring(5);
        return name.ToUpperInvariant();
    }

    // =====================================================================
    // Building
    // =====================================================================

    private void Build()
    {
        GameObject canvasObject = new GameObject("GameMenu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasObject.transform;

        // The Main Menu only gets the settings panel.
        if (!menuMode)
        {
            BuildPauseButton(root);
            BuildPauseMenu(root);
            pauseRoot.SetActive(false);
        }

        BuildSettings(root);
        settingsRoot.SetActive(false);
    }

    private void BuildPauseButton(RectTransform parent)
    {
        Button button = art != null && art.pauseButton != null
            ? NewSpriteButton("PauseButton", parent, art.pauseButton, 2f)
            : NewTextButton("PauseButton", parent, "II", new Vector2(80f, 80f), 40f);

        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(ScreenMargin, -ScreenMargin);

        button.onClick.AddListener(Pause);
        pauseButton = button.gameObject;
    }

    private void BuildPauseMenu(RectTransform parent)
    {
        pauseRoot = NewDim("PauseMenu", parent).gameObject;
        RectTransform rootRect = (RectTransform)pauseRoot.transform;

        Image panel = NewPixelPanel("Panel", rootRect, PausePanelColor);
        panel.rectTransform.sizeDelta = new Vector2(760f, 480f);

        // PAUSE title bar, sitting on the panel's top edge.
        if (art != null && art.pauseTitle != null)
        {
            Image title = NewImage("Title", panel.rectTransform, art.pauseTitle);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(art.pauseTitle.rect.width, art.pauseTitle.rect.height) * 3f;
        }
        else
        {
            TextMeshProUGUI title = NewText("Title", panel.rectTransform, "PAUSED", 64f, LightText);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        }

        // Icon row.
        string[] labels = { "RESUME", "RESTART", "MAIN MENU", "MUTE" };
        Sprite[] icons = art != null
            ? new[] { art.resumeIcon, art.restartIcon, art.homeIcon, art.musicOnIcon }
            : new Sprite[4];
        UnityEngine.Events.UnityAction[] actions = { Resume, Restart, GoToMainMenu, ToggleMute };

        for (int i = 0; i < labels.Length; i++)
        {
            float x = (i - 1.5f) * 165f;

            Button button = icons[i] != null
                ? NewSpriteButton(labels[i], panel.rectTransform, icons[i], 3f)
                : NewTextButton(labels[i], panel.rectTransform, labels[i].Substring(0, 1), new Vector2(102f, 102f), 48f);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, 45f);
            button.onClick.AddListener(actions[i]);

            TextMeshProUGUI label = NewText(labels[i] + "Label", panel.rectTransform, labels[i], 24f, LightText);
            label.rectTransform.anchoredPosition = new Vector2(x, -35f);

            if (i == 3)
            {
                muteIcon = icons[i] != null ? button.GetComponent<Image>() : null;
                muteLabel = label;
            }
        }

        Button settings = NewTextButton("Settings", panel.rectTransform, "SETTINGS", new Vector2(360f, 72f), 34f);
        ((RectTransform)settings.transform).anchoredPosition = new Vector2(0f, -125f);
        settings.onClick.AddListener(ShowSettings);

        TextMeshProUGUI hint = NewText("Hint", panel.rectTransform, "ESC TO RESUME", 20f, new Color(LightText.r, LightText.g, LightText.b, 0.55f));
        hint.rectTransform.anchoredPosition = new Vector2(0f, -205f);
    }

    private void BuildSettings(RectTransform parent)
    {
        settingsRoot = NewDim("Settings", parent).gameObject;
        RectTransform rootRect = (RectTransform)settingsRoot.transform;

        // The art is a square sheet with the SETTINGS header drawn in; scale it up whole.
        bool hasArt = art != null && art.settingsPanel != null;
        Image panel = hasArt ? NewImage("Panel", rootRect, art.settingsPanel) : NewPixelPanel("Panel", rootRect, new Color(0.55f, 0.7f, 0.55f, 0.97f));
        panel.raycastTarget = true;
        panel.rectTransform.sizeDelta = hasArt
            ? new Vector2(art.settingsPanel.rect.width, art.settingsPanel.rect.height) * 10f
            : new Vector2(830f, 820f);
        RectTransform p = panel.rectTransform;

        if (!hasArt)
        {
            TextMeshProUGUI heading = NewText("Heading", p, "SETTINGS", 44f, DarkText);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -55f);
        }

        Button close = art != null && art.closeButton != null
            ? NewSpriteButton("Close", p, art.closeButton, 2f)
            : NewTextButton("Close", p, "X", new Vector2(68f, 68f), 36f);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-62f, -58f);
        close.onClick.AddListener(CloseSettings);

        // Rows are laid out top to bottom from just under the header.
        const float left = 75f;
        float y = -148f;

        Heading(p, "AUDIO", left, ref y);
        VolumeRow(p, "MASTER", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, left, ref y);
        VolumeRow(p, "MUSIC", GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, left, ref y);
        VolumeRow(p, "SFX", GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, left, ref y);

        y -= 12f;
        Heading(p, "CONTROLS", left, ref y);

        keyLabels.Clear();
        for (int i = 0; i < GameSettings.ActionCount; i++) KeyRow(p, i, left, ref y);

        y -= 14f;
        Button reset = NewTextButton("ResetKeys", p, "RESET CONTROLS", new Vector2(320f, 52f), 24f);
        RectTransform resetRect = (RectTransform)reset.transform;
        resetRect.anchorMin = resetRect.anchorMax = new Vector2(0.5f, 1f);
        resetRect.pivot = new Vector2(0.5f, 1f);
        resetRect.anchoredPosition = new Vector2(0f, y);
        reset.onClick.AddListener(() => { GameSettings.ResetKeys(); rebinding = -1; RefreshKeyLabels(); });
    }

    private static void Heading(RectTransform panel, string text, float left, ref float y)
    {
        TextMeshProUGUI heading = NewText(text, panel, text, 30f, DarkText);
        heading.fontStyle = FontStyles.Bold;
        heading.alignment = TextAlignmentOptions.Left;
        PlaceRow(heading.rectTransform, left, y, 400f, 40f);
        y -= 44f;
    }

    private void VolumeRow(RectTransform panel, string name, float value, Action<float> onChange, float left, ref float y)
    {
        const float rowHeight = 56f;

        TextMeshProUGUI label = NewText(name, panel, name, 26f, DarkText);
        label.alignment = TextAlignmentOptions.Left;
        PlaceRow(label.rectTransform, left, y, 170f, rowHeight);

        TextMeshProUGUI percent = NewText(name + "Percent", panel, "", 26f, DarkText);
        percent.alignment = TextAlignmentOptions.Right;
        PlaceRow(percent.rectTransform, left + 600f, y, 85f, rowHeight);

        Slider slider = NewSlider(name + "Slider", panel);
        PlaceRow((RectTransform)slider.transform, left + 180f, y - (rowHeight - 42f) * 0.5f, 408f, 42f);
        slider.value = value;
        percent.text = Mathf.RoundToInt(value * 100f) + "%";
        slider.onValueChanged.AddListener(v =>
        {
            onChange(v);
            percent.text = Mathf.RoundToInt(v * 100f) + "%";
        });

        y -= rowHeight;
    }

    private void KeyRow(RectTransform panel, int action, float left, ref float y)
    {
        const float rowHeight = 48f;

        TextMeshProUGUI label = NewText("Action" + action, panel, GameSettings.ActionName((GameAction)action).ToUpperInvariant(), 26f, DarkText);
        label.alignment = TextAlignmentOptions.Left;
        PlaceRow(label.rectTransform, left, y, 330f, rowHeight);

        Button button = NewTextButton("Key" + action, panel, "", new Vector2(270f, 40f), 24f);
        PlaceRow((RectTransform)button.transform, left + 415f, y - 4f, 270f, 40f);
        button.onClick.AddListener(() => StartRebind(action));
        keyLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>());

        y -= rowHeight;
    }

    /// <summary>Places a rect by its top-left corner, measured from the panel's top-left.</summary>
    private static void PlaceRow(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // =====================================================================
    // Small UI helpers
    // =====================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Full-screen dark backdrop. It catches clicks, so nothing behind the menu can be clicked.</summary>
    private static RectTransform NewDim(string name, Transform parent)
    {
        Image dim = NewRect(name, parent).gameObject.AddComponent<Image>();
        dim.color = DimColor;
        RectTransform rect = dim.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Image NewPixelPanel(string name, Transform parent, Color color)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = PixelSprites.Panel();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PanelPixelSize;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color)
    {
        TextMeshProUGUI label = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(300f, size * 1.5f);
        return label;
    }

    /// <summary>A button showing a pixel-art sprite at a whole-number scale.</summary>
    private static Button NewSpriteButton(string name, Transform parent, Sprite sprite, float scale)
    {
        Image image = NewImage(name, parent, sprite);
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * scale;

        Button button = image.gameObject.AddComponent<Button>();
        SetTint(button);
        return button;
    }

    /// <summary>A dark pixel panel with a text label, for buttons that have no art.</summary>
    private static Button NewTextButton(string name, Transform parent, string text, Vector2 size, float fontSize)
    {
        Image image = NewPixelPanel(name, parent, ButtonColor);
        image.raycastTarget = true;
        image.rectTransform.sizeDelta = size;

        TextMeshProUGUI label = NewText("Label", image.rectTransform, text, fontSize, LightText);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

        Button button = image.gameObject.AddComponent<Button>();
        SetTint(button);
        return button;
    }

    private static void SetTint(Button button)
    {
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.8f, 1f, 0.75f, 1f);
        colors.selectedColor = Color.white; // don't stay highlighted after a click
        colors.pressedColor = new Color(0.6f, 0.75f, 0.55f, 1f);
        button.colors = colors;

        // No keyboard navigation: Space / Enter are game keys, and must never "click" a menu button.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    /// <summary>
    /// Unity keeps the last clicked button or slider "selected", and Space / Enter then submit it
    /// (pressing Jump would reopen the pause menu). Clear the selection whenever it lands on this menu.
    /// </summary>
    private void ClearSelection()
    {
        EventSystem events = EventSystem.current;
        if (events == null || events.currentSelectedGameObject == null) return;
        if (events.currentSelectedGameObject.transform.IsChildOf(transform)) events.SetSelectedGameObject(null);
    }

    private Slider NewSlider(string name, Transform parent)
    {
        RectTransform root = NewRect(name, parent);

        Image track = art != null && art.sliderTrack != null
            ? NewImage("Track", root, art.sliderTrack)
            : NewPixelPanel("Track", root, new Color(0.25f, 0.27f, 0.3f, 1f));
        track.preserveAspect = false;
        Stretch(track.rectTransform, 0f, 0f);

        RectTransform fillArea = NewRect("FillArea", root);
        Stretch(fillArea, 8f, 10f);
        Image fill = NewRect("Fill", fillArea).gameObject.AddComponent<Image>();
        fill.color = FillColor;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform, 0f, 0f);

        RectTransform handleArea = NewRect("HandleArea", root);
        Stretch(handleArea, 18f, 0f);
        Image handle = art != null && art.sliderHandle != null
            ? NewImage("Handle", handleArea, art.sliderHandle)
            : NewPixelPanel("Handle", handleArea, FillColor);
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(51f, 0f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Clicking anywhere on the bar (not only the handle) moves it.
        track.raycastTarget = true;
        return slider;
    }

    private static void Stretch(RectTransform rect, float insetX, float insetY)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(insetX, insetY);
        rect.offsetMax = new Vector2(-insetX, -insetY);
    }
}
