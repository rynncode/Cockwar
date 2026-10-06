using System.Collections.Generic;
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

    // Main Menu only: each scene sound's own volume, before the SFX slider is applied.
    private readonly Dictionary<AudioSource, float> menuSoundVolumes = new Dictionary<AudioSource, float>();

    private void OnEnable()
    {
        // The music follows the Music slider in the settings menu (35% by default,
        // so it sits under the sound effects instead of drowning them out).
        GameSettings.Changed += ApplyVolumes;
        ApplyVolumes();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplyVolumes;
    }

    private void ApplyVolumes()
    {
        if (musicSource != null) musicSource.volume = GameSettings.MusicVolume;
        ApplyMenuSoundVolumes();
    }

    /// <summary>
    /// In the Main Menu the button clicks and hover sounds are plain AudioSources in the scene, so
    /// the SFX slider sets their volume here. Not in a match: gameplay sounds apply the slider
    /// themselves when they play, and scaling them here as well would make them twice as quiet.
    /// </summary>
    private void ApplyMenuSoundVolumes()
    {
        if (FindAnyObjectByType<TurnManager>() != null) return;

        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (source == musicSource || source.gameObject.scene != gameObject.scene) continue;

            if (!menuSoundVolumes.TryGetValue(source, out float ownVolume))
            {
                ownVolume = source.volume;
                menuSoundVolumes[source] = ownVolume;
            }

            source.volume = ownVolume * GameSettings.SfxVolume;
        }
    }

    public void Start()
    {
        musicSource.clip = background;
        musicSource.Play();
    }

    public void PlaySFX (AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }
}
