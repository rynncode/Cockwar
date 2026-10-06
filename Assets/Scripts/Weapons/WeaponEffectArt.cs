using UnityEngine;

/// <summary>
/// Which animation frames the special weapons use for each effect, in one asset:
/// Assets/Resources/WeaponEffectArt. Every slot is a list of frames played in order (the
/// sprites from Assets/Sprites/Explosion files). Swap any list in the Inspector to restyle an
/// effect; an empty list falls back to the built-in pixel blobs, so nothing ever breaks.
/// </summary>
[CreateAssetMenu(fileName = "WeaponEffectArt", menuName = "Cockwar/Weapon Effect Art")]
public class WeaponEffectArt : ScriptableObject
{
    [Header("Shared")]
    [Tooltip("Flames: meteor trail, demon fire, burning players. Drawn sitting on their bottom edge.")]
    public Sprite[] fire;

    [Tooltip("Rising smoke wisps: rocket trails, landing dust, the Doom dud.")]
    public Sprite[] smoke;

    [Header("RENEITOR")]
    [Tooltip("The meteor impact.")]
    public Sprite[] meteorImpact;

    [Header("SATELASER")]
    [Tooltip("The beam striking down (played once while it burns into the ground).")]
    public Sprite[] beamStrike;

    [Tooltip("The beam crackling once it is fully down (loops).")]
    public Sprite[] beamLoop;

    [Tooltip("The beam fading out.")]
    public Sprite[] beamEnd;

    [Tooltip("Sparks where the beam touches, and the charge glow under the UFO (loops).")]
    public Sprite[] beamSpark;

    [Tooltip("The burst at the bottom of the beam when it finishes.")]
    public Sprite[] laserImpact;

    [Header("CALL AN AIRSTRIKE?!")]
    [Tooltip("Each rocket's explosion. Drawn sitting on the ground.")]
    public Sprite[] rocketBlast;

    [Header("DOOOOOOOOOM!")]
    [Tooltip("The mushroom cloud.")]
    public Sprite[] nukeCloud;

    private static WeaponEffectArt instance;
    private static bool looked;

    /// <summary>The asset in Assets/Resources, or null if it is missing (effects then use the built-in blobs).</summary>
    public static WeaponEffectArt Get()
    {
        if (instance == null && !looked)
        {
            looked = true;
            instance = Resources.Load<WeaponEffectArt>("WeaponEffectArt");
            if (instance == null) Debug.LogWarning("WeaponEffectArt: no Assets/Resources/WeaponEffectArt asset, using the built-in effect art.");
        }
        return instance;
    }

    /// <summary>True if this list has at least one usable frame.</summary>
    public static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        instance = null;
        looked = false;
    }
}
