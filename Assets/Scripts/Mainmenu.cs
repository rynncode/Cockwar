using UnityEngine;
using UnityEngine.SceneManagement;
public class Mainmenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(1);
        //LoadingScreenManager.Instance.SwitchToScene(1);
    }

    /// <summary>
    /// The Settings button: opens the same settings panel as the in-game pause menu (volumes and
    /// key bindings, saved by GameSettings, so they carry into the match).
    /// </summary>
    public void OpenSettings()
    {
        GameMenu.OpenSettings();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
