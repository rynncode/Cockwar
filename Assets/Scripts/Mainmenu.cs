using UnityEngine;
using UnityEngine.SceneManagement;
public class Mainmenu : MonoBehaviour
{
    public void PlayGame()
    {
        // SceneManager.LoadSceneAsync(1);
        LoadingScreenManager.Instance.SwitchToScene(1);
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
