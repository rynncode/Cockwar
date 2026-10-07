using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The original scene-built pause menu. GameMenu has replaced it (and hides its pause button),
/// but any scene button still wired to these methods keeps working: when a GameMenu exists,
/// Pause and Resume are handed to it so the two can never disagree about being paused.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private AudioSource bgmSource; // Drag your Music AudioSource here

    public void Pause()
    {
        if (GameMenu.Instance != null)
        {
            GameMenu.Instance.Pause();
            return;
        }

        pauseMenu.SetActive(true);
        Time.timeScale = 0;

        if (bgmSource != null)
            bgmSource.Pause();
    }

    public void Home()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("Main Menu");
    }

    public void Resume()
    {
        if (GameMenu.Instance != null)
        {
            pauseMenu.SetActive(false);
            GameMenu.Instance.Resume();
            return;
        }

        pauseMenu.SetActive(false);
        Time.timeScale = 1;

        if (bgmSource != null)
            bgmSource.UnPause();
    }

    public void Restart()
    {
        Time.timeScale = 1;
        MatchSetup.KeepModeForRestart();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
