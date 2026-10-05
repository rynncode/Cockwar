using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header ("-----------AUDIO SOURCES-----------")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    [Header("-----------AUDIO CLIPS-----------")]
    public AudioClip background;
    public AudioClip hoverButton;
    public AudioClip onClickButton;
    public AudioClip levelhoverMusic1;
    public AudioClip levelhoverMusic2;

    public AudioClip levelOnClick;
    

    public void Start()
    {
        musicSource.clip = background;
        musicSource.Play();
    }
}
