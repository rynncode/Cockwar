using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Physics demo mode, for presentations and video recording.
/// Press the toggle key (set it to ANY key in the Inspector) to show or hide it.
///
/// While on, it shows:
///  - a panel with the live aim angle, charge, initial velocity and its components,
///    the projectile's mass and gravity, momentum and kinetic energy, and the ideal
///    (flat-ground) range, maximum height and flight time for the current aim;
///  - after a shot, the MEASURED flight: time, position, velocity, maximum height,
///    path length and the energy split (kinetic, potential, total) next to the ideal numbers;
///  - a yellow line predicting the path (pure projectile-motion formula, stopping at terrain)
///    and a cyan line tracing the path the projectile really took.
///
/// Add this to ONE empty GameObject in the scene. Everything is drawn from code.
/// It only reads from the game; it never changes any physics.
/// </summary>
public class PhysicsDemoMode : MonoBehaviour
{
    [Header("Key")]
    [Tooltip("Press this key to turn the demo mode on or off. Pick any key. None = no key (use Start Enabled).")]
    public KeyCode toggleKey = KeyCode.F1;

    [Tooltip("Start with the demo mode already on.")]
    public bool startEnabled = false;

    [Header("Units")]
    [Tooltip("How many world units make 1 metre in the readouts. 1 matches the physics engine (gravity 9.81 units/s^2 = 9.81 m/s^2). Your distance HUD uses 10, which would not match the physics.")]
    public float unitsPerMeter = 10f;

    [Header("Panel")]
    [Tooltip("Distance of the panel from the top-left corner of the screen, in pixels.")]
    public Vector2 panelOffset = new Vector2(20f, 20f);

    [Tooltip("Text size as a fraction of the screen height. Raise it for a video recorded at low resolution.")]
    [Range(0.012f, 0.045f)]
    public float fontScale = 0.02f;

    [Header("Path lines")]
    [Tooltip("Yellow line: the path predicted by the projectile-motion formula, while it is your turn.")]
    public bool showPredictedPath = true;

    [Tooltip("Cyan line: the path the projectile actually took.")]
    public bool showActualPath = true;

    public Color predictedColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    public Color actualColor = new Color(0.3f, 0.9f, 1f, 1f);

    [Tooltip("Line thickness as a fraction of the camera's half-height, so it looks the same at any zoom.")]
    public float lineWidthFraction = 0.006f;

    [Tooltip("Optional. Material for the lines. Leave empty and a default sprite material is found automatically.")]
    public Material lineMaterial;

    [Tooltip("Draw order of the lines. Raise it if they hide behind the terrain.")]
    public int sortingOrder = 60;

    private bool demoOn;
    private TurnManager turnManager;
    private readonly List<CockroachShooting> shooters = new List<CockroachShooting>();

    // --- Measured flight of the last shot ---
    private bool tracking;
    private bool hasShot;
    private bool shotFinished;
    private Projectile tracked;
    private Rigidbody2D trackedBody;
    private string shotWeaponName;
    private Vector2 launchPos;
    private Vector2 lastPos;
    private Vector2 lastVel;
    private float launchTime;
    private float flightTime;
    private float measuredSpeed0;
    private float measuredAngle0;
    private float maxHeightReached;
    private float pathLength;
    private float shotMass = 1f;
    private float shotG;
    private float shotDrag;
    private bool idealValid;
    private float idealRange, idealHeight, idealTime;
    private readonly List<Vector3> trail = new List<Vector3>();

    // --- Drawing ---
    private LineRenderer predictedLine;
    private LineRenderer actualLine;
    private Material createdMaterial;
    private readonly List<Vector3> predictedPoints = new List<Vector3>();
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    private ContactFilter2D hitFilter;

    private GUIStyle boxStyle;
    private GUIStyle textStyle;
    private Texture2D boxTexture;
    private readonly StringBuilder sb = new StringBuilder();

    private void Start()
    {
        turnManager = FindAnyObjectByType<TurnManager>();
        demoOn = startEnabled;

        hitFilter = new ContactFilter2D();
        hitFilter.NoFilter();

        // Listen to every cockroach so we hear about each shot as it is fired.
        foreach (CockroachShooting s in FindObjectsByType<CockroachShooting>(FindObjectsSortMode.None))
        {
            s.OnProjectileLaunched += HandleProjectileLaunched;
            shooters.Add(s);
        }
    }

    private void OnDestroy()
    {
        foreach (CockroachShooting s in shooters)
        {
            if (s != null) s.OnProjectileLaunched -= HandleProjectileLaunched;
        }
        if (predictedLine != null) Destroy(predictedLine.gameObject);
        if (actualLine != null) Destroy(actualLine.gameObject);
        if (createdMaterial != null) Destroy(createdMaterial);
        if (boxTexture != null) Destroy(boxTexture);
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            demoOn = !demoOn;
            if (!demoOn) HideLines();
        }

        if (!demoOn) return;

        TrackProjectile();
        UpdatePredictedPath();
        UpdateActualPath();
    }

    // =====================================================================
    // Measuring the shot
    // =====================================================================

    private void HandleProjectileLaunched(Projectile projectile)
    {
        if (!demoOn || projectile == null) return;

        trackedBody = projectile.GetComponent<Rigidbody2D>();
        if (trackedBody == null) return;

        tracked = projectile;
        tracking = true;
        hasShot = true;
        shotFinished = false;

        CockroachMovement player = turnManager != null ? turnManager.CurrentPlayer : null;
        CockroachShooting shooter = player != null ? player.GetComponent<CockroachShooting>() : null;
        WeaponData weapon = shooter != null ? shooter.CurrentWeapon : null;
        shotWeaponName = weapon != null ? weapon.displayName : "Projectile";

        launchPos = projectile.transform.position;
        lastPos = launchPos;
        launchTime = Time.time;
        flightTime = 0f;
        maxHeightReached = 0f;
        pathLength = 0f;

        // Projectile.Launch has already set the velocity, so this is the true initial velocity.
        lastVel = trackedBody.linearVelocity;
        measuredSpeed0 = lastVel.magnitude;
        measuredAngle0 = Mathf.Atan2(lastVel.y, Mathf.Abs(lastVel.x)) * Mathf.Rad2Deg;

        shotMass = trackedBody.mass;
        shotG = Mathf.Abs(Physics2D.gravity.y) * trackedBody.gravityScale;
        shotDrag = trackedBody.linearDamping;

        // Ideal flat-ground numbers for exactly this launch (needs an upward launch and gravity).
        idealValid = lastVel.y > 0.001f && shotG > 0.0001f;
        if (idealValid)
        {
            idealTime = 2f * lastVel.y / shotG;
            idealHeight = lastVel.y * lastVel.y / (2f * shotG);
            idealRange = Mathf.Abs(lastVel.x) * idealTime;
        }

        trail.Clear();
        trail.Add(launchPos);
    }

    private void TrackProjectile()
    {
        if (!tracking) return;

        if (tracked == null)
        {
            // The projectile exploded and was destroyed: freeze the numbers.
            tracking = false;
            shotFinished = true;
            trail.Add(lastPos);
            return;
        }

        Vector2 pos = tracked.transform.position;
        pathLength += Vector2.Distance(pos, lastPos);
        lastPos = pos;
        lastVel = trackedBody != null ? trackedBody.linearVelocity : Vector2.zero;
        flightTime = Time.time - launchTime;
        maxHeightReached = Mathf.Max(maxHeightReached, pos.y - launchPos.y);

        if (trail.Count < 3000) trail.Add(pos);
    }

    // =====================================================================
    // Lines
    // =====================================================================

    private void UpdateActualPath()
    {
        if (!showActualPath || trail.Count < 2)
        {
            if (actualLine != null) actualLine.enabled = false;
            return;
        }

        if (actualLine == null) actualLine = MakeLine("Physics Demo: actual path", actualColor);
        actualLine.enabled = true;
        actualLine.startWidth = actualLine.endWidth = CurrentLineWidth();
        actualLine.positionCount = trail.Count;
        actualLine.SetPositions(trail.ToArray());
    }

    private void UpdatePredictedPath()
    {
        CockroachMovement player = turnManager != null ? turnManager.CurrentPlayer : null;
        CockroachShooting shooter = player != null ? player.GetComponent<CockroachShooting>() : null;
        CockroachAim aim = player != null ? player.GetComponent<CockroachAim>() : null;

        bool show = showPredictedPath && player != null && shooter != null && aim != null && player.isMyTurn;

        if (!show)
        {
            if (predictedLine != null) predictedLine.enabled = false;
            return;
        }

        float speed = PreviewSpeed(shooter);
        GetProjectileProperties(shooter, out _, out float gravity, out _);

        // Walk along the formula in small time steps, and stop at the first terrain/cockroach hit.
        predictedPoints.Clear();
        Vector2 start = aim.AimOrigin;
        Vector2 velocity = aim.AimDirection * speed;
        Vector2 previous = start;
        predictedPoints.Add(start);

        const float step = 0.05f;
        const int maxSteps = 240;

        for (int i = 1; i <= maxSteps; i++)
        {
            float t = i * step;
            Vector2 point = start + velocity * t + new Vector2(0f, -0.5f * gravity * t * t);

            if (FirstHit(previous, point, player.transform, out Vector2 hitPoint))
            {
                predictedPoints.Add(hitPoint);
                break;
            }

            predictedPoints.Add(point);
            previous = point;
        }

        if (predictedLine == null) predictedLine = MakeLine("Physics Demo: predicted path", predictedColor);
        predictedLine.enabled = true;
        predictedLine.startWidth = predictedLine.endWidth = CurrentLineWidth();
        predictedLine.positionCount = predictedPoints.Count;
        predictedLine.SetPositions(predictedPoints.ToArray());
    }

    /// <summary>The nearest solid thing between two points, ignoring the shooter, triggers and projectiles.</summary>
    private bool FirstHit(Vector2 a, Vector2 b, Transform shooter, out Vector2 point)
    {
        point = b;
        int count = Physics2D.Linecast(a, b, hitFilter, hitBuffer);
        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.transform.IsChildOf(shooter)) continue;
            if (col.GetComponentInParent<Projectile>() != null) continue;

            if (hitBuffer[i].distance < best)
            {
                best = hitBuffer[i].distance;
                point = hitBuffer[i].point;
                found = true;
            }
        }
        return found;
    }

    private float CurrentLineWidth()
    {
        Camera cam = Camera.main;
        return cam != null ? Mathf.Max(0.05f, cam.orthographicSize * lineWidthFraction) : 0.3f;
    }

    private LineRenderer MakeLine(string lineName, Color color)
    {
        GameObject go = new GameObject(lineName);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.numCapVertices = 2;
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = color;
        lr.endColor = color;
        lr.positionCount = 0;

        Material mat = lineMaterial;
        if (mat == null)
        {
            if (createdMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) createdMaterial = new Material(shader);
                else Debug.LogWarning("PhysicsDemoMode: no sprite shader found for the lines. Assign a Material in the Line Material field.");
            }
            mat = createdMaterial;
        }
        if (mat != null) lr.sharedMaterial = mat;

        return lr;
    }

    private void HideLines()
    {
        if (predictedLine != null) predictedLine.enabled = false;
        if (actualLine != null) actualLine.enabled = false;
    }

    // =====================================================================
    // Reading the current weapon
    // =====================================================================

    /// <summary>Launch speed shown/predicted: the live value while charging, full power when idle.</summary>
    private static float PreviewSpeed(CockroachShooting shooter)
    {
        WeaponData weapon = shooter.CurrentWeapon;
        float low = weapon != null ? weapon.minPower : shooter.minPower;
        float high = weapon != null ? weapon.maxPower : shooter.maxPower;
        float t = shooter.IsCharging ? shooter.ChargeRatio01 : 1f;
        return Mathf.Lerp(low, high, t);
    }

    /// <summary>Mass, effective gravity (g * gravity scale) and drag of the current weapon's projectile prefab.</summary>
    private static void GetProjectileProperties(CockroachShooting shooter, out float mass, out float gravity, out float drag)
    {
        WeaponData weapon = shooter.CurrentWeapon;
        GameObject prefab = weapon != null ? weapon.projectilePrefab : shooter.projectilePrefab;
        Rigidbody2D body = prefab != null ? prefab.GetComponent<Rigidbody2D>() : null;

        mass = body != null ? body.mass : 1f;
        float scale = body != null ? body.gravityScale : 1f;
        gravity = Mathf.Abs(Physics2D.gravity.y) * scale;
        drag = body != null ? body.linearDamping : 0f;
    }

    // =====================================================================
    // The panel
    // =====================================================================

    private void OnGUI()
    {
        if (!demoOn) return;

        EnsureStyles();
        string text = BuildText();

        float width = Mathf.Max(380f, Screen.height * 0.6f);
        GUIContent content = new GUIContent(text);
        float height = textStyle.CalcHeight(content, width - 24f) + 24f;

        Rect rect = new Rect(panelOffset.x, panelOffset.y, width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, width - 24f, height - 24f), content, textStyle);
    }

    private void EnsureStyles()
    {
        if (boxTexture == null)
        {
            boxTexture = new Texture2D(1, 1);
            boxTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            boxTexture.Apply();
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = boxTexture;
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.richText = true;
            textStyle.wordWrap = false;
            textStyle.normal.textColor = Color.white;
        }

        textStyle.fontSize = Mathf.Max(13, Mathf.RoundToInt(Screen.height * fontScale));
    }

    private float M(float units) { return units / Mathf.Max(0.0001f, unitsPerMeter); }

    private static string Head(string title) { return "<color=#7CFC9A><b>" + title + "</b></color>"; }

    private string BuildText()
    {
        sb.Clear();
        sb.Append(Head("PHYSICS DEMO"));
        if (toggleKey != KeyCode.None) sb.Append("   (" + toggleKey + " to hide)");
        sb.AppendLine();

        CockroachMovement player = turnManager != null ? turnManager.CurrentPlayer : null;
        CockroachShooting shooter = player != null ? player.GetComponent<CockroachShooting>() : null;
        CockroachAim aim = player != null ? player.GetComponent<CockroachAim>() : null;

        if (shooter == null || aim == null)
        {
            sb.AppendLine("Waiting for a player's turn...");
        }
        else
        {
            AppendLive(shooter, aim, player);
        }

        if (hasShot) AppendMeasured();

        sb.AppendLine();
        sb.Append("<color=#AAAAAA>Units: " + (Mathf.Approximately(unitsPerMeter, 1f) ? "1 unit = 1 m" : unitsPerMeter + " units = 1 m")
                  + ".  Ideal values assume flat ground, no air resistance.</color>");
        return sb.ToString();
    }

    private void AppendLive(CockroachShooting shooter, CockroachAim aim, CockroachMovement player)
    {
        WeaponData weapon = shooter.CurrentWeapon;
        float low = weapon != null ? weapon.minPower : shooter.minPower;
        float high = weapon != null ? weapon.maxPower : shooter.maxPower;

        Vector2 dir = aim.AimDirection;
        float elevation = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        string side = dir.x >= 0f ? "right" : "left";

        float speed = PreviewSpeed(shooter);
        float rad = elevation * Mathf.Deg2Rad;
        float vx = speed * Mathf.Cos(rad);
        float vy = speed * Mathf.Sin(rad);

        GetProjectileProperties(shooter, out float mass, out float gravity, out float drag);
        float gMetric = M(gravity);

        sb.AppendLine();
        sb.AppendLine(Head("AIM") + "   " + (weapon != null ? weapon.displayName : "Projectile") + "  (" + player.name + ")");
        sb.AppendLine("Angle above horizontal:  <b>" + elevation.ToString("F1") + "°</b>  aiming " + side);
        sb.AppendLine("Angle from +x axis:  " + aim.AimAngle.ToString("F1") + "°");

        sb.AppendLine();
        sb.AppendLine(Head("LAUNCH"));
        if (shooter.IsCharging)
        {
            float held = shooter.ChargeRatio01 * shooter.maxChargeTime;
            sb.AppendLine("Charge:  " + (shooter.ChargeRatio01 * 100f).ToString("F0") + "%   (" + held.ToString("F2") + " s of " + shooter.maxChargeTime.ToString("F1") + " s)");
            sb.AppendLine("Initial velocity v0:  <b>" + M(speed).ToString("F1") + " m/s</b>  (live)");
        }
        else
        {
            sb.AppendLine("Charge:  0%  (hold the mouse)");
            sb.AppendLine("Initial velocity v0:  <b>" + M(speed).ToString("F1") + " m/s</b>  (full-power preview; range "
                          + M(low).ToString("F0") + " to " + M(high).ToString("F0") + ")");
        }
        sb.AppendLine("Components:  vx = " + M(vx).ToString("F1") + " m/s,  vy = " + M(vy).ToString("F1") + " m/s");

        sb.AppendLine();
        sb.AppendLine(Head("PROJECTILE"));
        sb.AppendLine("Mass:  " + mass.ToString("F2") + " kg");
        sb.AppendLine("Gravity:  " + gMetric.ToString("F2") + " m/s²   (9.81 x gravity scale " + (gravity / Mathf.Max(0.0001f, Mathf.Abs(Physics2D.gravity.y))).ToString("F2") + ")");
        sb.AppendLine("Air resistance:  " + (drag > 0.0001f ? "drag " + drag.ToString("F2") + " (not in the ideal values)" : "none"));
        sb.AppendLine("Momentum p = m v0:  " + (mass * M(speed)).ToString("F1") + " kg m/s");
        sb.AppendLine("Kinetic energy at launch:  " + (0.5f * mass * M(speed) * M(speed)).ToString("F0") + " J");

        sb.AppendLine();
        sb.AppendLine(Head("IDEAL (flat ground)"));
        if (elevation > 0.01f && gMetric > 0.0001f)
        {
            float v = M(speed);
            float range = v * v * Mathf.Sin(2f * rad) / gMetric;
            float height = Mathf.Pow(v * Mathf.Sin(rad), 2f) / (2f * gMetric);
            float time = 2f * v * Mathf.Sin(rad) / gMetric;
            sb.AppendLine("Range R = v0² sin(2a) / g:  " + range.ToString("F1") + " m");
            sb.AppendLine("Max height H = (v0 sin a)² / 2g:  " + height.ToString("F1") + " m");
            sb.AppendLine("Flight time T = 2 v0 sin a / g:  " + time.ToString("F2") + " s");
        }
        else
        {
            sb.AppendLine("Aim upward to see range, height and time.");
        }
    }

    private void AppendMeasured()
    {
        float dx = lastPos.x - launchPos.x;
        float dy = lastPos.y - launchPos.y;
        float speed = lastVel.magnitude;

        sb.AppendLine();
        sb.AppendLine(Head("LAST SHOT") + "   " + shotWeaponName + (shotFinished ? "   (landed)" : "   (in flight)"));
        sb.AppendLine("Measured v0:  " + M(measuredSpeed0).ToString("F1") + " m/s at " + measuredAngle0.ToString("F1") + "°");
        sb.AppendLine("Time:  " + flightTime.ToString("F2") + " s");
        sb.AppendLine("Position:  dx = " + M(dx).ToString("F1") + " m,  dy = " + M(dy).ToString("F1") + " m");
        sb.AppendLine("Velocity:  vx = " + M(lastVel.x).ToString("F1") + ",  vy = " + M(lastVel.y).ToString("F1") + ",  speed = " + M(speed).ToString("F1") + " m/s");
        sb.AppendLine("Max height reached:  " + M(maxHeightReached).ToString("F1") + " m");
        sb.AppendLine("Path length:  " + M(pathLength).ToString("F1") + " m");

        float v = M(speed);
        float g = M(shotG);
        float ke = 0.5f * shotMass * v * v;
        float pe = shotMass * g * M(dy);
        sb.AppendLine("Energy:  KE " + ke.ToString("F0") + " J  +  PE " + pe.ToString("F0") + " J  =  <b>" + (ke + pe).ToString("F0") + " J</b>");

        if (idealValid)
        {
            sb.AppendLine("Ideal for this launch:  R " + M(idealRange).ToString("F1") + " m,  H " + M(idealHeight).ToString("F1")
                          + " m,  T " + idealTime.ToString("F2") + " s");
        }
        if (shotDrag > 0.0001f) sb.AppendLine("<color=#AAAAAA>This projectile has drag, so it will not match the ideal values.</color>");
    }
}
