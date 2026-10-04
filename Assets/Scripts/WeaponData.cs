using UnityEngine;

/// <summary>How a weapon shows its aim while it is the active player's turn.</summary>
public enum AimStyle
{
    /// <summary>Only the crosshair. Nothing else is drawn.</summary>
    CrosshairOnly,

    /// <summary>A cone grows from the cockroach toward the crosshair while the shot charges.</summary>
    ChargeCone,

    /// <summary>A straight line from the cockroach along the aim direction (sniper, laser).</summary>
    Line
}

/// <summary>
/// Step 13: one weapon. Create one asset per weapon with
/// Assets > Create > Cockwar > Weapon, then drag the assets into the
/// "Weapons" list on each cockroach's CockroachShooting.
/// (Or run Cockwar > Create Starter Weapons to generate a ready-made set.)
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Cockwar/Weapon")]
public class WeaponData : ScriptableObject
{
    [Tooltip("Name shown on the Weapons button and in the panel.")]
    public string displayName = "Weapon";

    [Tooltip("Optional. Shown next to the name in the panel.")]
    public Sprite icon;

    [Tooltip("Prefab that has the Projectile script on it.")]
    public GameObject projectilePrefab;

    [Header("Power")]
    [Tooltip("Launch speed on an instant click-and-release.")]
    public float minPower = 10f;

    [Tooltip("Launch speed when fully charged. Set equal to Min Power for a weapon with fixed power.")]
    public float maxPower = 70f;

    [Header("Held Weapon")]
    [Tooltip("Optional. Sprite drawn in front of the cockroach, rotating to point at the crosshair (needs the AimGun component on the cockroach). The art should point right.")]
    public Sprite heldSprite;

    [Header("Aim Indicator")]
    [Tooltip("How this weapon shows its aim: crosshair only, a charge cone, or a straight line.")]
    public AimStyle aimStyle = AimStyle.CrosshairOnly;

    [Tooltip("Crosshair shown for this weapon. Leave empty to use the cockroach's default Crosshair.")]
    public GameObject aimIndicatorPrefab;

    [Tooltip("Distance from the cockroach to the crosshair. 0 = use the default from CockroachAim. (Line style puts the crosshair where the line ends instead.)")]
    public float indicatorDistance = 0f;

    [Tooltip("Turn the crosshair to point along the aim direction (art should point right). Needs a crosshair prefab.")]
    public bool rotateIndicatorWithAim = false;

    [Tooltip("Cone/line color when the charge is low (cone), or the line color (line).")]
    public Color aimColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Cone color at full charge. Not used by Line style.")]
    public Color aimColorFull = new Color(1f, 0.35f, 0.05f, 1f);

    [Header("Charge Cone")]
    [Tooltip("Width of the cone at its far end, in world units. 0 = match the crosshair's size.")]
    public float coneWidth = 0f;

    [Tooltip("How far from the cockroach the cone starts, in world units, so it does not cover the body.")]
    public float coneStartOffset = 1.5f;

    [Header("Line")]
    [Tooltip("Thickness of the line, in world units.")]
    public float lineWidth = 0.25f;

    [Tooltip("Longest the line can be, in world units. It stops earlier if it hits terrain or a cockroach.")]
    public float lineMaxLength = 150f;

    [Header("Turn System")]
    [Tooltip("Whether firing this weapon ends the turn (after the shot lands).")]
    public bool endsTurnOnFire = true;

    [Header("Ammo")]
    [Tooltip("Shots per cockroach for the whole match. -1 = unlimited.")]
    public int ammo = -1;

    [Header("Multi-Shot")]
    [Tooltip("How many projectiles one shot fires. 1 = a normal single shot.")]
    public int multiShotCount = 1;

    [Tooltip("Total angle, in degrees, the projectiles are fanned across, centered on the aim direction. Ignored when Multi Shot Count is 1.")]
    public float multiShotSpreadDegrees = 12f;
}
