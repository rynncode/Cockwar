using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private AudioSource bgmSource; // Drag your Music AudioSource here

    public void Pause()
    {
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
        pauseMenu.SetActive(false);
        Time.timeScale = 1;

        if (bgmSource != null)
            bgmSource.UnPause();
    }

    public void Restart()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}