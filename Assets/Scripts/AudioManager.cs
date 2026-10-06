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


    private void Awake()
    {
        // Gameplay sound effects (Sfx.Play) go through the same mixer group as this SFX source.
        if (SFXSource != null) Sfx.Output = SFXSource.outputAudioMixerGroup;
    }

    private void OnEnable()
    {
        // The music follows the Music slider in the settings menu (35% by default,
        // so it sits under the sound effects instead of drowning them out).
        GameSettings.Changed += ApplyMusicVolume;
        ApplyMusicVolume();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplyMusicVolume;
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null) musicSource.volume = GameSettings.MusicVolume;
    }

    public void Start()
    {
        musicSource.clip = background;
        musicSource.Play();
    }
}
