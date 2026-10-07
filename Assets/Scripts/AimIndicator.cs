using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 13b: the single aim indicator system.
/// Add this ONE component to each cockroach. For the currently selected weapon it shows:
///  - the weapon's crosshair (its own prefab, or the cockroach's default Crosshair), and
///  - the weapon's aim visual: a charge cone, a straight line, or nothing (WeaponData > Aim Style).
/// Everything hides when it is not this cockroach's turn, and when the cockroach dies.
/// CockroachAim still does the aim math; it stops moving the crosshair itself once this is present.
/// </summary>
[RequireComponent(typeof(CockroachAim))]
[RequireComponent(typeof(CockroachMovement))]
public class AimIndicator : MonoBehaviour
{
    [Header("Cone / line drawing")]
    [Tooltip("Optional. Material for the cone and line. Leave empty and a default sprite material is found automatically.")]
    public Material visualMaterial;

    [Tooltip("Draw order of the cone and line. Raise it if they hide behind the terrain.")]
    public int sortingOrder = 50;

    private CockroachAim aim;
    private CockroachMovement movement;
    private CockroachShooting shooting;

    // One crosshair per weapon that has its own prefab, created the first time it is selected.
    private readonly Dictionary<WeaponData, Transform> spawned = new Dictionary<WeaponData, Transform>();

    // The cone/line drawer, shared by all weapons of this cockroach.
    private LineRenderer visual;
    private Material createdMaterial;

    // Reused for the line raycast so it does not allocate every frame.
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[32];
    private ContactFilter2D hitFilter;

    private void Awake()
    {
        aim = GetComponent<CockroachAim>();
        movement = GetComponent<CockroachMovement>();
        shooting = GetComponent<CockroachShooting>();

        hitFilter = new ContactFilter2D();
        hitFilter.NoFilter();
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
        }

        // Targeted and right-click special weapons (Satelaser, Airstrike...) don't aim along a line,
        // so the crosshair is hidden for them (Satelaser shows its own target marker instead).
        bool visible = movement.isMyTurn && (shooting == null || !shooting.CannotFire) && !SpecialAttack.HidesCrosshair(weapon);

        // Show only the current crosshair; hide the rest, including the fallback one.
        foreach (Transform t in spawned.Values) SetVisible(t, visible && t == current);
        if (aim.crosshair != null) SetVisible(aim.crosshair, visible && aim.crosshair == current);

        if (!visible)
        {
            HideVisual();
            return;
        }

        Vector2 origin = aim.AimOrigin;
        Vector2 direction = aim.AimDirection;
        float distance = (weapon != null && weapon.indicatorDistance > 0f) ? weapon.indicatorDistance : aim.crosshairDistance;

        // A special weapon can park the crosshair where its effect ends (the Blowtorch: the far end of the tunnel).
        SpecialAttack special = SpecialAttack.Of(weapon);
        if (special != null && special.CrosshairDistance > 0f) distance = special.CrosshairDistance;
        Vector2 crosshairPosition = origin + direction * distance;

        AimStyle style = weapon != null ? weapon.aimStyle : AimStyle.CrosshairOnly;
        switch (style)
        {
            case AimStyle.ChargeCone:
                DrawCone(weapon, origin, direction, distance, current);
                break;
            case AimStyle.Line:
                crosshairPosition = DrawLine(weapon, origin, direction, crosshairPosition);
                break;
            default:
                HideVisual();
                break;
        }

        if (current == null) return;

        current.position = crosshairPosition;

        bool rotate = weapon != null && weapon.aimIndicatorPrefab != null && weapon.rotateIndicatorWithAim;
        if (rotate) current.rotation = Quaternion.Euler(0f, 0f, aim.AimAngle);
    }

    // --- Charge cone ---

    /// <summary>
    /// A wedge from just outside the body toward the crosshair. Its length follows the
    /// charge, reaching the crosshair at full power. It only shows while charging.
    /// </summary>
    private void DrawCone(WeaponData weapon, Vector2 origin, Vector2 direction, float distance, Transform crosshair)
    {
        float charge = (shooting != null && shooting.IsCharging) ? shooting.ChargeRatio01 : 0f;
        if (charge <= 0.001f)
        {
            HideVisual();
            return;
        }

        float start = Mathf.Min(weapon.coneStartOffset, distance * 0.9f);
        float fullLength = Mathf.Max(0.01f, distance - start);

        float farWidth = weapon.coneWidth > 0f ? weapon.coneWidth : CrosshairSize(crosshair);
        float nearWidth = farWidth * 0.1f;
        float endWidth = Mathf.Lerp(nearWidth, farWidth, charge);

        Vector2 a = origin + direction * start;
        Vector2 b = a + direction * (fullLength * charge);

        Color c = Color.Lerp(weapon.aimColor, weapon.aimColorFull, charge);
        Color fadedStart = new Color(c.r, c.g, c.b, c.a * 0.2f);

        SetVisual(a, b, nearWidth, endWidth, fadedStart, c);
    }

    private static float CrosshairSize(Transform crosshair)
    {
        if (crosshair == null) return 3f;
        SpriteRenderer sr = crosshair.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 3f;
        return sr.sprite.bounds.size.y * Mathf.Abs(sr.transform.lossyScale.y);
    }

    // --- Straight line (sniper) ---

    /// <summary>
    /// Draws a line along the aim direction, stopping at the first terrain or cockroach.
    /// Returns where the crosshair should go: the hit point, or the default spot if nothing is hit.
    /// </summary>
    private Vector2 DrawLine(WeaponData weapon, Vector2 origin, Vector2 direction, Vector2 defaultCrosshair)
    {
        float maxLength = Mathf.Max(1f, weapon.lineMaxLength);
        Vector2 end = origin + direction * maxLength;
        Vector2 crosshairPosition = defaultCrosshair;

        int count = Physics2D.Raycast(origin, direction, hitFilter, hitBuffer, maxLength);
        float best = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.transform.IsChildOf(transform)) continue;                 // the shooter itself
            if (col.GetComponentInParent<Projectile>() != null) continue;     // shells in flight

            if (hitBuffer[i].distance < best)
            {
                best = hitBuffer[i].distance;
                end = hitBuffer[i].point;
                crosshairPosition = hitBuffer[i].point;
            }
        }

        SetVisual(origin, end, weapon.lineWidth, weapon.lineWidth, weapon.aimColor, weapon.aimColor);
        return crosshairPosition;
    }

    // --- Drawing helpers ---

    private void SetVisual(Vector2 a, Vector2 b, float startWidth, float endWidth, Color startColor, Color endColor)
    {
        EnsureVisual();
        if (visual == null) return;

        visual.enabled = true;
        visual.SetPosition(0, a);
        visual.SetPosition(1, b);
        visual.startWidth = startWidth;
        visual.endWidth = endWidth;
        visual.startColor = startColor;
        visual.endColor = endColor;
    }

    private void HideVisual()
    {
        if (visual != null && visual.enabled) visual.enabled = false;
    }

    private void EnsureVisual()
    {
        if (visual != null) return;

        // Not parented to the cockroach: it flips its scale to face left or right.
        GameObject go = new GameObject("Aim Visual (" + name + ")");
        visual = go.AddComponent<LineRenderer>();
        visual.useWorldSpace = true;
        visual.positionCount = 2;
        visual.numCapVertices = 0;
        visual.sortingOrder = sortingOrder;
        visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visual.receiveShadows = false;

        Material mat = visualMaterial;
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                createdMaterial = new Material(shader);
                mat = createdMaterial;
            }
            else
            {
                Debug.LogWarning("AimIndicator: no sprite shader found for the cone/line. Assign a Material in the Visual Material field.");
            }
        }
        if (mat != null) visual.sharedMaterial = mat;

        visual.enabled = false;
    }

    // --- Crosshair spawning ---

    private Transform GetOrSpawn(WeaponData weapon)
    {
        if (spawned.TryGetValue(weapon, out Transform existing) && existing != null) return existing;

        // Not parented to the cockroach: it flips its scale, which would mirror the crosshair.
        GameObject go = Instantiate(weapon.aimIndicatorPrefab);
        go.name = weapon.displayName + " Crosshair (" + name + ")";
        go.SetActive(false);

        spawned[weapon] = go.transform;
        return go.transform;
    }

    private static void SetVisible(Transform t, bool visible)
    {
        if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
    }

    private void OnDisable()
    {
        // Covers death (CockroachDeath turns this cockroach off): never leave anything behind.
        foreach (Transform t in spawned.Values) SetVisible(t, false);
        if (aim != null) SetVisible(aim.crosshair, false);
        HideVisual();
    }

    private void OnDestroy()
    {
        foreach (Transform t in spawned.Values)
        {
            if (t != null) Destroy(t.gameObject);
        }
        if (visual != null) Destroy(visual.gameObject);
        if (createdMaterial != null) Destroy(createdMaterial);
    }
}
