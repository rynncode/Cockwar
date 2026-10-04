using UnityEngine;

/// <summary>
/// Step 13: one weapon. Create one asset per weapon with
/// Assets > Create > Cockwar > Weapon, then drag the assets into the
/// "Weapons" list on each cockroach's CockroachShooting.
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

    [Tooltip("Launch speed when fully charged.")]
    public float maxPower = 70f;

    [Header("Turn System")]
    [Tooltip("Whether firing this weapon ends the turn (after the shot lands).")]
    public bool endsTurnOnFire = true;

    [Header("Ammo")]
    [Tooltip("Shots per cockroach for the whole match. -1 = unlimited.")]
    public int ammo = -1;
}
