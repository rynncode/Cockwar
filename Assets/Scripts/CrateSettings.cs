using System.Collections.Generic;
using UnityEngine;

/// <summary>One weapon a supply crate can contain, and how likely it is.</summary>
[System.Serializable]
public class CrateLoot
{
    [Tooltip("The weapon asset (Assets/Weapons). The collector gets it added to their weapon panel, or gets extra ammo if they already have it.")]
    public WeaponData weapon;

    [Tooltip("Untick to take this weapon out of crates without deleting the row.")]
    public bool enabled = true;

    [Tooltip("Relative chance. A weapon with weight 30 is 6x as common as one with weight 5. Low = rare, but never impossible (only 0 or unticked is impossible).")]
    [Min(0f)] public float weight = 10f;

    [Tooltip("Shots the crate gives.")]
    [Min(1)] public int ammo = 1;

    [Tooltip("Worked out automatically from all the weights: this weapon's real chance per crate.")]
    public string chance = "";
}

/// <summary>
/// All the supply crate settings in one asset (Assets/Resources/CrateSettings), so balancing
/// never needs a code change. CrateDropManager loads it automatically.
/// Make another with Assets > Create > Cockwar > Crate Settings and drag it onto a
/// CrateDropManager to give a scene its own rules.
/// </summary>
[CreateAssetMenu(fileName = "CrateSettings", menuName = "Cockwar/Crate Settings")]
public class CrateSettings : ScriptableObject
{
    [Header("Crate contents (weighted random)")]
    public List<CrateLoot> loot = new List<CrateLoot>();

    [Header("Starting crates")]
    [Min(0)] public int minStartingCrates = 2;
    [Min(0)] public int maxStartingCrates = 4;

    [Header("Placement (world units; the map is 500 wide, a cockroach about 5)")]
    [Tooltip("Crates are never placed closer than this to each other.")]
    public float minimumCrateDistance = 40f;

    [Tooltip("Crates are never placed (or dropped) closer than this to a living player.")]
    public float minimumPlayerDistance = 25f;

    [Tooltip("Keep crates this far in from the left and right edges of the map.")]
    public float edgeMargin = 30f;

    [Tooltip("The ground under a crate must be flat for this far to each side...")]
    public float flatHalfWidth = 3f;

    [Tooltip("...within this much height difference.")]
    public float maxSlope = 3f;

    [Tooltip("Never place a crate on ground lower than this above the acid.")]
    public float minHeightAboveAcid = 6f;

    [Header("Plane drops between turns")]
    [Tooltip("Chance of a plane drop after each finished turn. 0.5 = 50%.")]
    [Range(0f, 1f)] public float dropChance = 0.5f;

    [Tooltip("No more plane drops while this many crates are already lying around. 0 = no limit.")]
    [Min(0)] public int maxCratesOnMap = 6;

    [Tooltip("How many earlier drop spots are remembered, so the next drops go somewhere else.")]
    [Min(0)] public int rememberRecentDrops = 3;

    [Tooltip("How strongly drops avoid landing near the player who moves next (they would reach it first). 0 = ignore turn order.")]
    [Min(0f)] public float turnOrderFairness = 0.5f;

    [Tooltip("Randomness added when choosing between good drop spots. Higher = less predictable.")]
    [Min(0f)] public float dropRandomness = 60f;

    [Tooltip("Plane speed, in world units per second.")]
    public float aircraftSpeed = 90f;

    [Tooltip("How far above the highest ground on the map the plane flies.")]
    public float aircraftHeightAboveGround = 35f;

    [Tooltip("How far before the drop spot the plane appears (and how far past it it flies before vanishing).")]
    public float aircraftApproachDistance = 140f;

    [Tooltip("Pause before the plane appears, after the turn has ended.")]
    public float delayBeforeAircraft = 0.4f;

    [Tooltip("Seconds after the crate is released before the next turn starts. The crate keeps falling during the next turn.")]
    public float delayAfterDrop = 2f;

    [Tooltip("Safety limit: the next turn starts after this many seconds even if the crate never landed.")]
    public float maxDropSeconds = 12f;

    [Tooltip("Point the camera at the plane and the falling crate.")]
    public bool cameraFollowsDrop = true;

    [Header("Crates")]
    [Tooltip("Width of a crate in world units.")]
    public float crateSize = 6f;

    [Tooltip("Fall speed while the parachute is open.")]
    public float parachuteFallSpeed = 18f;

    [Tooltip("Crates disappear after this many turns if nobody collects them. 0 = they stay until collected.")]
    [Min(0)] public int crateLifetimeTurns = 0;

    [Tooltip("Show the weapon name over the crate when it is picked up.")]
    public bool showPickupText = true;

    [Tooltip("World size of one pixel of the pickup text.")]
    public float pickupTextPixelSize = 0.4f;

    [Header("Location ping")]
    [Tooltip("At the start of each turn, every crate pulses a ring and off-screen crates get an arrow at the screen edge, for this many seconds. 0 = no ping.")]
    [Min(0f)] public float pingSeconds = 4f;

    [Header("Art (optional; empty = built-in pixel art)")]
    public Sprite crateSprite;
    public Sprite parachuteSprite;

    [Tooltip("Should face right; it is flipped when flying left.")]
    public Sprite aircraftSprite;

    [Header("Sounds (optional)")]
    [Tooltip("Played once when the plane appears (an engine fly-by). Stops when the plane leaves.")]
    public AudioClip aircraftSound;
    [Range(0f, 1f)] public float aircraftVolume = 0.6f;

    public AudioClip dropSound;
    public AudioClip landSound;
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float crateVolume = 0.8f;

    [Header("Starting inventory")]
    [Tooltip("These weapons show in every player's weapon panel from the start, greyed out with no ammo, until a crate gives them some.")]
    public List<WeaponData> showEmptyAtStart = new List<WeaponData>();

    [Header("Testing")]
    [Tooltip("Every player gets one shot of each of these weapons when the match starts (on top of their normal weapons). Handy for trying out the special weapons without finding crates or editing the scene. Empty it again for real games.")]
    public List<WeaponData> giveEveryPlayerAtStart = new List<WeaponData>();

    /// <summary>The settings in Assets/Resources/CrateSettings, or built-in defaults if that asset is missing.</summary>
    public static CrateSettings Load()
    {
        CrateSettings settings = Resources.Load<CrateSettings>("CrateSettings");
        if (settings != null) return settings;

        Debug.LogWarning("CrateSettings: no Assets/Resources/CrateSettings asset found. Using defaults with no weapons, so crates are switched off.");
        return CreateInstance<CrateSettings>();
    }

    private static bool CanDrop(CrateLoot entry) => entry != null && entry.enabled && entry.weapon != null;

    /// <summary>True if at least one weapon can come out of a crate.</summary>
    public bool HasAnyLoot()
    {
        foreach (CrateLoot entry in loot)
            if (CanDrop(entry)) return true;
        return false;
    }

    /// <summary>
    /// Weighted random pick: each weapon's chance is its weight divided by the total.
    /// If every weight is 0, all enabled weapons are equally likely. Null if there are none.
    /// </summary>
    public CrateLoot PickLoot()
    {
        float total = 0f;
        int usable = 0;

        foreach (CrateLoot entry in loot)
        {
            if (!CanDrop(entry)) continue;
            usable++;
            total += Mathf.Max(0f, entry.weight);
        }

        if (usable == 0) return null;

        bool equalChances = total <= 0f;
        float roll = Random.value * (equalChances ? usable : total);

        CrateLoot last = null;
        foreach (CrateLoot entry in loot)
        {
            if (!CanDrop(entry)) continue;

            roll -= equalChances ? 1f : Mathf.Max(0f, entry.weight);
            if (roll < 0f) return entry;
            last = entry;
        }

        return last; // only reached through float rounding at the very top of the range
    }

    private void OnValidate()
    {
        if (maxStartingCrates < minStartingCrates) maxStartingCrates = minStartingCrates;

        float total = 0f;
        int usable = 0;
        foreach (CrateLoot entry in loot)
        {
            if (!CanDrop(entry)) continue;
            usable++;
            total += Mathf.Max(0f, entry.weight);
        }

        foreach (CrateLoot entry in loot)
        {
            if (entry == null) continue;

            if (!CanDrop(entry)) entry.chance = "never";
            else if (total <= 0f) entry.chance = (100f / usable).ToString("0.#") + "% (all weights are 0)";
            else entry.chance = (100f * Mathf.Max(0f, entry.weight) / total).ToString("0.#") + "%";
        }
    }
}
