using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance;

    [Header("UI References")]
    public GameObject m_LoadingScreenObject;
    public Slider ProgressBar;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    public void SwitchToScene(int id)
    {
        m_LoadingScreenObject.SetActive(true);
        ProgressBar.value = 0;
        StartCoroutine(SwitchToSceneAsynchronous(id));
    }

    IEnumerator SwitchToSceneAsynchronous(int id)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(id);

        while (!asyncLoad.isDone)
        {
           
  
            ProgressBar.value = asyncLoad.progress;

            yield return null;
        }

        // Brief delay before turning off the loading screen panel
        yield return new WaitForSeconds(0.2f);
        m_LoadingScreenObject.SetActive(false);
    }
}