using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 13b: the single aim indicator system.
/// Add this ONE component to each cockroach. It shows whichever indicator the
/// currently selected weapon asks for (WeaponData > Aim Indicator section),
/// places it on the aim orbit, and hides it when it is not this cockroach's turn.
/// If a weapon has no indicator prefab, the cockroach's existing Crosshair
/// (CockroachAim > Crosshair) is shown instead, exactly like before.
/// CockroachAim still does the aim math; it just stops moving the crosshair
/// itself once this component is present.
/// </summary>
[RequireComponent(typeof(CockroachAim))]
[RequireComponent(typeof(CockroachMovement))]
public class AimIndicator : MonoBehaviour
{
    private CockroachAim aim;
    private CockroachMovement movement;
    private CockroachShooting shooting;

    // One spawned indicator per weapon, created the first time that weapon is selected.
    private readonly Dictionary<WeaponData, Transform> spawned = new Dictionary<WeaponData, Transform>();
    private readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        aim = GetComponent<CockroachAim>();
        movement = GetComponent<CockroachMovement>();
        shooting = GetComponent<CockroachShooting>();
    }

    private void LateUpdate()
    {
        // LateUpdate so the aim direction from CockroachAim.Update is already fresh.
        WeaponData weapon = shooting != null ? shooting.CurrentWeapon : null;
        Transform current = null;

        if (weapon != null && weapon.aimIndicatorPrefab != null)
        {
            current = GetOrSpawn(weapon);
        }
        else if (aim.crosshair != null)
        {
            current = aim.crosshair;
            if (!baseScales.ContainsKey(current)) baseScales[current] = current.localScale;
        }

        bool visible = movement.isMyTurn;

        // Show only the current indicator; hide the rest, including the fallback crosshair.
        foreach (Transform t in spawned.Values) SetVisible(t, visible && t == current);
        if (aim.crosshair != null) SetVisible(aim.crosshair, visible && aim.crosshair == current);

        if (!visible || current == null) return;

        float distance = (weapon != null && weapon.indicatorDistance > 0f) ? weapon.indicatorDistance : aim.crosshairDistance;
        current.position = aim.AimOrigin + aim.AimDirection * distance;

        bool rotate = weapon != null && weapon.aimIndicatorPrefab != null && weapon.rotateIndicatorWithAim;
        if (rotate) current.rotation = Quaternion.Euler(0f, 0f, aim.AimAngle);

        float scaleMultiplier = 1f;
        if (weapon != null && weapon.aimIndicatorPrefab != null && shooting != null)
        {
            scaleMultiplier = Mathf.Lerp(1f, weapon.chargeScaleMultiplier, shooting.ChargeRatio01);
        }
        current.localScale = baseScales[current] * scaleMultiplier;
    }

    private Transform GetOrSpawn(WeaponData weapon)
    {
        if (spawned.TryGetValue(weapon, out Transform existing) && existing != null) return existing;

        // Not parented to the cockroach: the cockroach flips its scale to face left or right,
        // which would mirror the indicator (same reason the original crosshair is not a child).
        GameObject go = Instantiate(weapon.aimIndicatorPrefab);
        go.name = weapon.displayName + " Indicator (" + name + ")";
        go.SetActive(false);

        spawned[weapon] = go.transform;
        baseScales[go.transform] = go.transform.localScale;
        return go.transform;
    }

    private static void SetVisible(Transform t, bool visible)
    {
        if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
    }

    private void OnDisable()
    {
        // Covers death (CockroachDeath turns this cockroach off): never leave an indicator behind.
        foreach (Transform t in spawned.Values) SetVisible(t, false);
        if (aim != null) SetVisible(aim.crosshair, false);
    }

    private void OnDestroy()
    {
        foreach (Transform t in spawned.Values)
        {
            if (t != null) Destroy(t.gameObject);
        }
    }
}
