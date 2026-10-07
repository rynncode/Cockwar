using System.Collections.Generic;
using UnityEngine;

/// <summary>The kinds of supply crate. Each has its own look and contents.</summary>
public enum CrateKind
{
    /// <summary>A normal weapon (wooden crate).</summary>
    Weapon,

    /// <summary>A special weapon: Reneitor, Satelaser... (black and gold crate).</summary>
    Special,

    /// <summary>Restores health (white crate, red cross).</summary>
    Health,

    /// <summary>Raises max health and fills it (steel blue crate, shield).</summary>
    Shield,

    /// <summary>A tool for the Utilities row: Teleporter, Blowtorch (green crate, wrench).</summary>
    Tool
}

/// <summary>How likely one kind of crate is.</summary>
[System.Serializable]
public class CrateKindChance
{
    public CrateKind kind;

    [Tooltip("Untick to stop this kind of crate appearing.")]
    public bool enabled = true;

    [Tooltip("Relative chance against the other crate kinds.")]
    [Min(0f)] public float weight = 10f;

    [Tooltip("Worked out automatically: this kind's real chance per crate.")]
    public string chance = "";
}

/// <summary>One weapon (or tool) a supply crate can contain, and how likely it is.</summary>
[System.Serializable]
public class CrateLoot
{
    [Tooltip("The weapon asset (Assets/Weapons). The collector gets it added to their weapon panel, or gets extra ammo if they already have it.")]
    public WeaponData weapon;

    [Tooltip("Untick to take this weapon out of crates without deleting the row.")]
    public bool enabled = true;

    [Tooltip("Relative chance inside its list. A weapon with weight 30 is 6x as common as one with weight 5. Low = rare, but never impossible (only 0 or unticked is impossible).")]
    [Min(0f)] public float weight = 10f;

    [Tooltip("Shots the crate gives.")]
    [Min(1)] public int ammo = 1;

    [Tooltip("Worked out automatically from all the weights: this weapon's real chance per crate of its kind.")]
    public string chance = "";
}

/// <summary>What one particular crate holds. Rolled when the crate is created.</summary>
public struct CrateContents
{
    public CrateKind kind;
    public CrateLoot loot;   // Weapon, Special and Tool crates
    public int amount;       // Health and Shield crates: points of health
}

/// <summary>
/// All the supply crate settings in one asset (Assets/Resources/CrateSettings), so balancing
/// never needs a code change. CrateDropManager loads it automatically.
/// Each crate first rolls its kind (Crate Kinds), then what is inside (the matching list, or
/// a health/shield amount). Make another with Assets > Create > Cockwar > Crate Settings and
/// drag it onto a CrateDropManager to give a scene its own rules.
/// </summary>
[CreateAssetMenu(fileName = "CrateSettings", menuName = "Cockwar/Crate Settings")]
public class CrateSettings : ScriptableObject
{
    [Header("Crate kinds (weighted random)")]
    public List<CrateKindChance> crateKinds = new List<CrateKindChance>
    {
        new CrateKindChance { kind = CrateKind.Weapon, weight = 45f },
        new CrateKindChance { kind = CrateKind.Health, weight = 22f },
        new CrateKindChance { kind = CrateKind.Special, weight = 12f },
        new CrateKindChance { kind = CrateKind.Shield, weight = 11f },
        new CrateKindChance { kind = CrateKind.Tool, weight = 10f },
    };

    [Header("Weapon crate contents (weighted random)")]
    public List<CrateLoot> loot = new List<CrateLoot>();

    [Header("Special crate contents (weighted random)")]
    public List<CrateLoot> specialLoot = new List<CrateLoot>();

    [Header("Tool crate contents (weighted random)")]
    public List<CrateLoot> toolLoot = new List<CrateLoot>();

    [Header("Health crate")]
    [Tooltip("Health restored, rolled between these (never above the cockroach's max).")]
    [Min(1)] public int healMin = 25;
    [Min(1)] public int healMax = 40;

    [Header("Shield crate")]
    [Tooltip("Max health added (and filled), rolled between these.")]
    [Min(1)] public int shieldMin = 25;
    [Min(1)] public int shieldMax = 50;

    [Tooltip("Shields can never raise a cockroach's max health more than this above its starting max, however many it collects.")]
    [Min(0)] public int maxShieldBonus = 50;

    [Header("Tank (dropped by the plane, only one at a time)")]
    [Tooltip("Let the plane drop a tank.")]
    public bool tankEnabled = true;

    [Tooltip("Chance, after each finished turn while there is no tank on the map, that the plane brings a tank instead of a crate. 0.25 = 25%.")]
    [Range(0f, 1f)] public float tankDropChance = 0.25f;

    [Tooltip("Width of the tank in world units (a cockroach is about 5).")]
    public float tankWidth = 26f;

    [Tooltip("The tank's health. The driver can't be hurt while inside; the tank takes the hits instead.")]
    [Min(1)] public int tankHealth = 150;

    [Tooltip("Driving speed, in world units per second.")]
    public float tankSpeed = 10f;

    [Tooltip("How close (world units, from the tank's edge) a cockroach must be to climb in.")]
    public float tankEnterRange = 4f;

    [Tooltip("The shell the cannon fires (a projectile prefab; its explosion decides the damage).")]
    public GameObject tankShellPrefab;

    [Tooltip("Shell speed on a quick click, and when fully charged.")]
    public float tankMinPower = 20f;
    public float tankMaxPower = 100f;

    [Tooltip("Seconds of holding the mouse to reach full power.")]
    public float tankChargeSeconds = 1.3f;

    [Tooltip("Explosion when the tank is destroyed (its numbers are replaced by the ones below).")]
    public GameObject tankExplosionPrefab;
    public int tankExplosionDamage = 35;
    public float tankExplosionRadius = 22f;

    [Tooltip("Damage the driver takes when the tank is destroyed with them inside.")]
    public int tankEjectDamage = 20;

    [Tooltip("The ground under a dropped tank must be flat for this far to each side.")]
    public float tankFlatHalfWidth = 10f;

    [Tooltip("Optional art (empty = built-in pixel tank).")]
    public Sprite tankHullSprite;
    public Sprite tankBarrelSprite;

    public AudioClip tankFireSound;
    public AudioClip tankEnterSound;
    public AudioClip tankDestroyedSound;

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
    [Tooltip("Crate art per kind. Empty = built-in pixel art (wooden, black and gold, white with a red cross, steel blue with a shield, green with a wrench).")]
    public Sprite crateSprite;
    public Sprite specialCrateSprite;
    public Sprite healthCrateSprite;
    public Sprite shieldCrateSprite;
    public Sprite toolCrateSprite;
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

        Debug.LogWarning("CrateSettings: no Assets/Resources/CrateSettings asset found. Using defaults with no weapons (only health and shield crates).");
        return CreateInstance<CrateSettings>();
    }

    private static bool CanDrop(CrateLoot entry) => entry != null && entry.enabled && entry.weapon != null;

    private static bool AnyDroppable(List<CrateLoot> list)
    {
        if (list == null) return false;
        foreach (CrateLoot entry in list)
            if (CanDrop(entry)) return true;
        return false;
    }

    /// <summary>The weapon list for a kind of crate, or null for Health and Shield.</summary>
    public List<CrateLoot> LootFor(CrateKind kind)
    {
        switch (kind)
        {
            case CrateKind.Weapon: return loot;
            case CrateKind.Special: return specialLoot;
            case CrateKind.Tool: return toolLoot;
            default: return null;
        }
    }

    /// <summary>True if this kind of crate is switched on and has something to give.</summary>
    public bool CanSpawn(CrateKindChance entry)
    {
        if (entry == null || !entry.enabled) return false;
        if (entry.kind == CrateKind.Health || entry.kind == CrateKind.Shield) return true;
        return AnyDroppable(LootFor(entry.kind));
    }

    /// <summary>True if at least one kind of crate can appear.</summary>
    public bool HasAnyContent()
    {
        foreach (CrateKindChance entry in crateKinds)
            if (CanSpawn(entry)) return true;
        return false;
    }

    /// <summary>Rolls a whole crate: first its kind, then what is inside.</summary>
    public CrateContents RollContents()
    {
        CrateKindChance kindEntry = WeightedPick(crateKinds, CanSpawn, entry => entry.weight);
        CrateContents contents = new CrateContents { kind = kindEntry != null ? kindEntry.kind : CrateKind.Weapon };

        switch (contents.kind)
        {
            case CrateKind.Health:
                contents.amount = Random.Range(healMin, Mathf.Max(healMin, healMax) + 1);
                break;
            case CrateKind.Shield:
                contents.amount = Random.Range(shieldMin, Mathf.Max(shieldMin, shieldMax) + 1);
                break;
            default:
                contents.loot = WeightedPick(LootFor(contents.kind), CanDrop, entry => entry.weight);
                break;
        }

        return contents;
    }

    /// <summary>
    /// Weighted random pick: each entry's chance is its weight divided by the total of the
    /// usable entries. If every weight is 0, the usable entries are equally likely. Null if none.
    /// </summary>
    private static T WeightedPick<T>(List<T> list, System.Func<T, bool> usable, System.Func<T, float> weightOf) where T : class
    {
        if (list == null) return null;

        float total = 0f;
        int count = 0;
        foreach (T entry in list)
        {
            if (!usable(entry)) continue;
            count++;
            total += Mathf.Max(0f, weightOf(entry));
        }

        if (count == 0) return null;

        bool equalChances = total <= 0f;
        float roll = Random.value * (equalChances ? count : total);

        T last = null;
        foreach (T entry in list)
        {
            if (!usable(entry)) continue;

            roll -= equalChances ? 1f : Mathf.Max(0f, weightOf(entry));
            if (roll < 0f) return entry;
            last = entry;
        }

        return last; // only reached through float rounding at the very top of the range
    }

    private void OnValidate()
    {
        if (maxStartingCrates < minStartingCrates) maxStartingCrates = minStartingCrates;
        if (healMax < healMin) healMax = healMin;
        if (shieldMax < shieldMin) shieldMax = shieldMin;

        FillChances(crateKinds, CanSpawn, entry => entry.weight, (entry, text) => entry.chance = text);
        FillChances(loot, CanDrop, entry => entry.weight, (entry, text) => entry.chance = text);
        FillChances(specialLoot, CanDrop, entry => entry.weight, (entry, text) => entry.chance = text);
        FillChances(toolLoot, CanDrop, entry => entry.weight, (entry, text) => entry.chance = text);
    }

    /// <summary>Writes each entry's real percentage into its Chance field, for the Inspector.</summary>
    private static void FillChances<T>(List<T> list, System.Func<T, bool> usable, System.Func<T, float> weightOf, System.Action<T, string> write) where T : class
    {
        if (list == null) return;

        float total = 0f;
        int count = 0;
        foreach (T entry in list)
        {
            if (entry == null || !usable(entry)) continue;
            count++;
            total += Mathf.Max(0f, weightOf(entry));
        }

        foreach (T entry in list)
        {
            if (entry == null) continue;

            if (!usable(entry)) write(entry, "never");
            else if (total <= 0f) write(entry, (100f / count).ToString("0.#") + "% (all weights are 0)");
            else write(entry, (100f * Mathf.Max(0f, weightOf(entry)) / total).ToString("0.#") + "%");
        }
    }
}
