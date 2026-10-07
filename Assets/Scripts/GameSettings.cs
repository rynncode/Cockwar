using System;
using UnityEngine;

/// <summary>A rebindable action. The settings menu lists these in this order.</summary>
public enum GameAction
{
    MoveLeft,
    MoveRight,
    Jump,
    NextWeapon,
    WeaponPanel,
    EndTurn,
    Vehicle
}

/// <summary>
/// The player's settings: volumes, mute and key bindings. Saved with PlayerPrefs, so they
/// carry over between scenes and play sessions. Loaded the first time anything reads them.
/// Scripts read keys from here (GameSettings.Key(GameAction.Jump)) instead of their own
/// KeyCode fields, so a rebind in the settings menu applies everywhere at once.
/// </summary>
public static class GameSettings
{
    // Music sits well under the sound effects by default so it doesn't drown them out.
    public const float DefaultMasterVolume = 1f;
    public const float DefaultMusicVolume = 0.35f;
    public const float DefaultSfxVolume = 0.9f;

    private static readonly KeyCode[] DefaultKeys =
    {
        KeyCode.A,      // MoveLeft
        KeyCode.D,      // MoveRight
        KeyCode.Space,  // Jump
        KeyCode.Tab,    // NextWeapon
        KeyCode.Q,      // WeaponPanel
        KeyCode.Return, // EndTurn
        KeyCode.F       // Vehicle (get in / out of the tank)
    };

    private static readonly string[] ActionNames =
    {
        "Move Left", "Move Right", "Jump", "Next Weapon", "Weapon Panel", "End Turn", "Tank In / Out"
    };

    private static bool loaded;
    private static float masterVolume;
    private static float musicVolume;
    private static float sfxVolume;
    private static bool muted;
    private static KeyCode[] keys;

    /// <summary>Raised whenever any setting changes. Music sources listen to this to update their volume.</summary>
    public static event Action Changed;

    public static int ActionCount => DefaultKeys.Length;

    public static float MasterVolume { get { Load(); return masterVolume; } set => SetFloat(ref masterVolume, value, "MasterVolume"); }
    public static float MusicVolume  { get { Load(); return musicVolume; }  set => SetFloat(ref musicVolume, value, "MusicVolume"); }
    public static float SfxVolume    { get { Load(); return sfxVolume; }    set => SetFloat(ref sfxVolume, value, "SfxVolume"); }

    /// <summary>Silences all game audio without touching the volume sliders.</summary>
    public static bool Muted
    {
        get { Load(); return muted; }
        set
        {
            Load();
            muted = value;
            PlayerPrefs.SetInt("Muted", value ? 1 : 0);
            Apply();
        }
    }

    public static string ActionName(GameAction action) => ActionNames[(int)action];

    public static KeyCode Key(GameAction action)
    {
        Load();
        return keys[(int)action];
    }

    /// <summary>
    /// Binds a key to an action. If another action already used that key, the two swap,
    /// so no key ever ends up doing two things.
    /// </summary>
    public static void SetKey(GameAction action, KeyCode key)
    {
        Load();
        int index = (int)action;
        int clash = Array.IndexOf(keys, key);
        if (clash >= 0 && clash != index) keys[clash] = keys[index];
        keys[index] = key;

        SaveKeys();
        Apply();
    }

    public static void ResetKeys()
    {
        Load();
        keys = (KeyCode[])DefaultKeys.Clone();
        SaveKeys();
        Apply();
    }

    private static void SetFloat(ref float field, float value, string prefKey)
    {
        Load();
        field = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(prefKey, field);
        Apply();
    }

    private static void SaveKeys()
    {
        for (int i = 0; i < keys.Length; i++) PlayerPrefs.SetInt("Key." + (GameAction)i, (int)keys[i]);
    }

    /// <summary>Pushes the master volume and mute to Unity's listener, then tells listeners something changed.</summary>
    private static void Apply()
    {
        AudioListener.volume = muted ? 0f : masterVolume;
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        masterVolume = PlayerPrefs.GetFloat("MasterVolume", DefaultMasterVolume);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", DefaultMusicVolume);
        sfxVolume = PlayerPrefs.GetFloat("SfxVolume", DefaultSfxVolume);
        muted = PlayerPrefs.GetInt("Muted", 0) == 1;

        keys = new KeyCode[DefaultKeys.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = (KeyCode)PlayerPrefs.GetInt("Key." + (GameAction)i, (int)DefaultKeys[i]);

        AudioListener.volume = muted ? 0f : masterVolume;
    }

    /// <summary>Applies the saved master volume as soon as the game starts, before any scene plays a sound.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        loaded = false; // domain reload may be off in the editor; always read fresh on Play
        Load();
    }
}
