using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// One shared place to play short sound effects from any script:
///     Sfx.Play(clip);
///     Sfx.PlayRandom(clips, volume, pitchVariation);
/// It creates itself the first time it is used (nothing to add to the scene),
/// keeps a small pool of AudioSources so overlapping sounds don't cut each other
/// off, and survives scene loads. Sounds are 2D (same volume anywhere on screen).
/// AudioManager hands it the mixer's SFX group, so the SFX volume slider applies.
/// </summary>
public class Sfx : MonoBehaviour
{
    private const int PoolSize = 16;

    private static Sfx instance;

    /// <summary>Mixer group every sound plays through. Set by AudioManager; null = no mixer.</summary>
    public static AudioMixerGroup Output
    {
        get => output;
        set
        {
            output = value;
            if (instance == null) return;
            foreach (AudioSource source in instance.sources) source.outputAudioMixerGroup = value;
        }
    }
    private static AudioMixerGroup output;

    private AudioSource[] sources;
    private int next;

    private static Sfx Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        sources = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
            sources[i] = source;
        }
    }

    /// <summary>Plays a clip once. Does nothing if the clip is null, so unassigned slots are safe.</summary>
    /// <param name="pitchVariation">Random pitch range around 1 (0.1 = 0.9 to 1.1), so repeated sounds don't sound identical.</param>
    public static void Play(AudioClip clip, float volume = 1f, float pitchVariation = 0f)
    {
        if (clip == null) return;
        Instance.PlayOnFreeSource(clip, volume, 1f + Random.Range(-pitchVariation, pitchVariation));
    }

    /// <summary>Plays one random clip from the array. Empty or null arrays are ignored.</summary>
    public static void PlayRandom(AudioClip[] clips, float volume = 1f, float pitchVariation = 0f)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[Random.Range(0, clips.Length)], volume, pitchVariation);
    }

    private void PlayOnFreeSource(AudioClip clip, float volume, float pitch)
    {
        // Prefer an idle source; if all are busy, reuse the oldest one (round robin).
        AudioSource chosen = null;
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource candidate = sources[(next + i) % PoolSize];
            if (!candidate.isPlaying) { chosen = candidate; break; }
        }
        if (chosen == null) chosen = sources[next];
        next = (next + 1) % PoolSize;

        chosen.pitch = pitch;
        chosen.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}
