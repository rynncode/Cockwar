using UnityEngine;

/// <summary>
/// The grappling hook's rope, on the cockroach hanging from it. Added by GrappleHookAttack.
///
/// While attached, a DistanceJoint2D keeps the cockroach within the rope's length of the hook
/// (the rope can go slack, but never stretch), and CockroachMovement hands the body over to
/// physics (ExternalControl). Keys: W / Up climb, S / Down lower, A / D swing, Jump lets go.
/// Letting go keeps the speed you had, so you can fling yourself.
///
/// It lets go by itself when the turn ends, the cockroach dies, or the ground the hook is in
/// gets blown away.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GrappleRope : MonoBehaviour
{
    private GrappleHookAttack hook;
    private CockroachMovement movement;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Health health;
    private DistanceJoint2D joint;

    private Vector2 anchor;          // where the claw is
    private Vector2 surfaceNormal;   // points out of the ground at the claw
    private bool checkGround;        // false if no solid ground could be found behind the claw at all
    private float swingInput;
    private float groundCheckTimer;

    private SpriteRenderer ropeSprite;
    private SpriteRenderer clawSprite;

    /// <summary>True while hanging from the rope.</summary>
    public bool IsAttached => joint != null && joint.enabled;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<Health>();
    }

    /// <summary>Hooks onto a point (or moves the hook there, if already hanging).</summary>
    public void Attach(GrappleHookAttack attack, Vector2 point, Vector2 surfaceNormal)
    {
        hook = attack;
        anchor = point;
        this.surfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector2.up;

        // The collision outline is a smoothed copy of the pixel ground, so the claw can sit a little
        // outside the real pixels. Only watch for the ground being blown away if there is solid
        // ground behind the claw right now; otherwise it would let go the moment it hooked.
        checkGround = GroundBehindClaw();

        if (joint == null)
        {
            joint = gameObject.AddComponent<DistanceJoint2D>();
            joint.autoConfigureDistance = false;
            joint.autoConfigureConnectedAnchor = false;
            joint.maxDistanceOnly = true;   // a rope: it can go slack but never stretch
            joint.enableCollision = true;
            joint.connectedBody = null;     // hooked to a fixed point in the world
        }

        // Hang from the middle of the body, not the feet.
        Vector2 centre = BodyCentre();
        joint.anchor = transform.InverseTransformPoint(centre);
        joint.connectedAnchor = anchor;
        joint.distance = Mathf.Clamp(Vector2.Distance(centre, anchor), hook.MinLength, hook.MaxLength);
        joint.enabled = true;

        if (movement != null) movement.ExternalControl = true;
        EnsureVisuals();
        groundCheckTimer = 0f;
    }

    /// <summary>Lets go of the rope, keeping the current speed.</summary>
    public void Release()
    {
        if (!IsAttached) return;

        joint.enabled = false;
        if (movement != null)
        {
            movement.ExternalControl = false;
            movement.BeginFreeFall();
        }

        if (ropeSprite != null) ropeSprite.enabled = false;
        if (clawSprite != null) clawSprite.enabled = false;
        if (hook != null) hook.PlayRelease();
    }

    private void Update()
    {
        if (!IsAttached) return;

        // Let go by itself when the turn is over or we died.
        bool dead = health != null && health.IsDead;
        if (movement == null || !movement.isMyTurn || dead)
        {
            Release();
            return;
        }

        // The ground holding the hook has been blown away.
        groundCheckTimer -= Time.deltaTime;
        if (groundCheckTimer <= 0f)
        {
            groundCheckTimer = 0.1f;
            if (checkGround && !GroundBehindClaw())
            {
                Release();
                return;
            }
        }

        if (GameMenu.IsPaused) return;

        if (Input.GetKeyDown(GameSettings.Key(GameAction.Jump)))
        {
            Release();
            return;
        }

        // Climb and lower.
        float climb = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) climb -= 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) climb += 1f;
        if (climb != 0f)
            joint.distance = Mathf.Clamp(joint.distance + climb * hook.ClimbSpeed * Time.deltaTime, hook.MinLength, hook.MaxLength);

        // Swing (applied in FixedUpdate).
        swingInput = 0f;
        if (Input.GetKey(GameSettings.Key(GameAction.MoveLeft))) swingInput -= 1f;
        if (Input.GetKey(GameSettings.Key(GameAction.MoveRight))) swingInput += 1f;
    }

    private void FixedUpdate()
    {
        if (!IsAttached) return;

        // On the ground (rope slack): A / D walk as normal, so you can still move around while hooked.
        if (movement != null && movement.IsGrounded)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = swingInput * movement.walkSpeed;
            body.linearVelocity = velocity;
            return;
        }

        // In the air: A / D swing.
        if (swingInput != 0f)
            body.AddForce(Vector2.right * swingInput * hook.SwingForce * body.mass, ForceMode2D.Force);
    }

    private void LateUpdate()
    {
        if (!IsAttached || ropeSprite == null) return;

        // Rope: a thin bar from the body to the claw.
        Vector2 from = BodyCentre();
        Vector2 delta = anchor - from;
        ropeSprite.enabled = true;
        ropeSprite.transform.position = (from + anchor) * 0.5f;
        ropeSprite.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        ropeSprite.transform.localScale = new Vector3(delta.magnitude, hook.RopeWidth, 1f);

        // Claw, pointing into the ground along the rope.
        clawSprite.enabled = true;
        clawSprite.transform.position = anchor;
        clawSprite.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
    }

    /// <summary>True if there is solid ground at the claw or a little way behind it (into the surface).</summary>
    private bool GroundBehindClaw()
    {
        TerrainGenerator terrain = TerrainGenerator.Instance;
        if (terrain == null) return true;

        for (float depth = 0f; depth <= 3f; depth += 0.5f)
        {
            if (terrain.IsSolidAt(anchor - surfaceNormal * depth)) return true;
        }
        return false;
    }

    private Vector2 BodyCentre() => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;

    private void EnsureVisuals()
    {
        // Not children of the cockroach, so its turning around (a negative scale) never mirrors them.
        if (ropeSprite == null)
        {
            ropeSprite = WeaponFx.MakeSprite("Grapple Rope", WeaponArt.Pixel(), anchor, 1f, 18);
            ropeSprite.color = hook.RopeColor;
        }

        if (clawSprite == null)
            clawSprite = WeaponFx.MakeSprite("Grapple Claw", hook.ClawSprite, anchor, hook.ClawSize, 19);
    }

    private void OnDisable()
    {
        // Covers death (CockroachDeath turns things off) and leaving the scene.
        Release();
    }

    private void OnDestroy()
    {
        if (ropeSprite != null) Destroy(ropeSprite.gameObject);
        if (clawSprite != null) Destroy(clawSprite.gameObject);
    }
}
