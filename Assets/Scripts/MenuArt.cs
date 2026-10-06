using UnityEngine;

/// <summary>
/// The pixel-art sprites the in-game pause and settings menu (GameMenu) uses.
/// The one asset lives at Assets/Resources/MenuArt so the menu can load it without any
/// Inspector wiring. Any empty slot falls back to a plain code-drawn panel or a text label.
/// </summary>
[CreateAssetMenu(fileName = "MenuArt", menuName = "Cockwar/Menu Art")]
public class MenuArt : ScriptableObject
{
    [Header("Pause")]
    public Sprite pauseButton;
    public Sprite pauseTitle;
    public Sprite resumeIcon;
    public Sprite restartIcon;
    public Sprite homeIcon;
    public Sprite musicOnIcon;
    public Sprite musicOffIcon;

    [Header("Settings")]
    [Tooltip("Square panel with the SETTINGS header drawn at the top.")]
    public Sprite settingsPanel;
    public Sprite closeButton;
    public Sprite sliderTrack;
    public Sprite sliderHandle;

    private static MenuArt cached;

    /// <summary>The asset at Resources/MenuArt, or null if it was removed.</summary>
    public static MenuArt Load()
    {
        if (cached == null) cached = Resources.Load<MenuArt>("MenuArt");
        return cached;
    }
}
