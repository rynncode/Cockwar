using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 1: Player movement.
/// A / D walk left and right. Hold SPACE to charge a jump, release to jump.
/// Jump feel helpers: coyote time, input buffering, asymmetrical gravity,
/// apex hang time and corner slipping. Walking follows slopes so hills can be climbed.
/// Movement only works while isMyTurn is true, so the turn system (step 9)
/// can switch it on and off without changing this script.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CockroachMovement : MonoBehaviour
{
    [Header("Walking")]
    [Tooltip("Walk speed in units per second.")]
    public float walkSpeed = 3f;

    [Header("Jumping")]

    [Tooltip("Upward speed of the SMALLEST jump (a quick tap). Jump power can never go below this.")]
    public float minJumpPower = 10f;

    [Tooltip("Upward speed of the BIGGEST jump (fully charged). Jump power can never go above this.")]
    public float maxJumpPower = 22f;

    [Tooltip("Seconds you must hold the key to reach maxJumpPower.")]
    public float maxChargeTime = 0.8f;

    [Header("Air Control")]
    [Tooltip("If true, A / D steer the cockroach while it is moving UP in a jump.")]
    public bool steerWhileRising = true;

    [Tooltip("If true, A / D steer the cockroach while it is falling.")]
    public bool steerWhileFalling = true;

    [Tooltip("If true, the cockroach stops moving sideways in the air when no A / D key is held. If false, it keeps drifting the way it last went. Only applies in the phases (rising / falling) where steering is on.")]
    public bool stopInAirWhenNoInput = true;

    [Tooltip("How quickly sideways speed changes in the air (units per second, per second). LOW = weak, gradual steering that keeps your jump momentum. HIGH (e.g. 1000) = instant steering. With Walk Speed 20, a value of 40 takes about 1 second to reverse direction.")]
    public float airAcceleration = 40f;

    [Header("Jump Feel")]
    [Tooltip("Coyote time: seconds after walking off a ledge during which you can still jump. 0.1s is about 6 frames at 60 FPS.")]
    public float coyoteTime = 0.1f;

    [Tooltip("Input buffer: a jump pressed up to this many seconds BEFORE landing still happens when you land.")]
    public float jumpBufferTime = 0.12f;

    [Tooltip("Gravity multiplier while falling (1 = normal). Higher = snappier, less floaty fall.")]
    public float fallGravityMultiplier = 1.4f;

    [Tooltip("Apex hang: when vertical speed is within this range of zero (top of the arc), gravity is reduced. Uses speed units, so it needs to be big at this project's large world scale.")]
    public float apexThreshold = 3f;

    [Tooltip("Gravity multiplier at the top of the arc (1 = normal, 0.5 = half gravity = brief hang). Lower = more hang time.")]
    [Range(0f, 1f)]
    public float apexGravityMultiplier = 0.5f;

    [Header("Corner Slipping")]
    [Tooltip("If only this much (world units) of the head's outer edge clips a platform's corner, the cockroach is nudged around it instead of bonking. 0 = off.")]
    public float cornerSlipWidth = 1f;

    [Tooltip("How far (world units) the cockroach is nudged sideways per physics step while slipping around a corner.")]
    public float cornerNudgeAmount = 0.25f;

    [Tooltip("How far above the head (world units) to look for a platform. Added on top of the distance covered this physics step.")]
    public float cornerCheckDistance = 0.5f;

    [Header("Slopes")]
    [Tooltip("Steepest slope (degrees) the cockroach can walk up. Steeper than this counts as a wall. 0 = slope walking off.")]
    [Range(0f, 85f)]
    public float maxClimbAngle = 65f;

    [Header("Ground Check")]
    [Tooltip("Empty child object placed at the cockroach's feet.")]
    public Transform groundCheckPoint;

    [Tooltip("Radius of the circle used to look for ground.")]
    public float groundCheckRadius = 0.15f;

    [Tooltip("Which layers count as ground.")]
    public LayerMask groundLayer;

    [Header("Turn Control")]
    [Tooltip("When false the player ignores all input.")]
    public bool isMyTurn = true;

    [Header("Knockback")]
    [Tooltip("When true, normal walking is disabled while the player is airborne after a physics push.")]
    public bool physicsMovementUntilGrounded = true;

    [Tooltip("Multiplies the vertical part of an externally supplied physics push.")]
    [Range(0f, 1f)]
    public float verticalPushMultiplier = 0.5f;

    [Tooltip("Minimum seconds physics control stays active before it can hand control back, even if already grounded. Stops a weak push that never leaves the ground from clearing on the same physics step it was applied.")]
    public float minPhysicsMovementTime = 0.1f;

    [Tooltip("isGrounded must stay continuously true for this many seconds before knockback physics control is handed back. Stops a single-frame false 'grounded' reading (e.g. right as an explosion destroys the terrain underneath, or while bouncing over bumpy ground) from ending knockback while still visibly airborne.")]
    public float requiredGroundedTime = 0.12f;

    [Header("Bounce")]
    [Tooltip("When on, a hard landing from a knockback bounces instead of stopping dead — each bounce weaker than the last.")]
    public bool enableBounce = true;

    [Tooltip("Fraction of the incoming downward speed kept as the next bounce's upward speed. 0.45 means each bounce reaches about 45% of the previous one's height. Lower = bounces die out faster.")]
    [Range(0f, 1f)]
    public float bounciness = 0.45f;

    [Tooltip("Once a bounce's upward speed would be below this, it stops bouncing and settles instead, so it doesn't bounce forever on tiny amounts.")]
    public float minBounceSpeed = 3f;

    [Tooltip("Safety cap on how many times it can bounce, even if the math says it should keep going.")]
    public int maxBounces = 6;

    // True while normal player movement is temporarily disabled
    // because the Rigidbody is being controlled by physics.
    private bool physicsMovementActive;

    // Counts up while physicsMovementActive is true.
    private float physicsMovementElapsed;

    // Seconds isGrounded has been continuously true while physics-controlled.
    // Reset to 0 the instant isGrounded goes false, or a bounce fires.
    private float groundedStreak;

    // Fastest downward speed seen while airborne this knockback, used to size the next bounce.
    private float lastAirborneVerticalSpeed;

    // How many times we've bounced this knockback, capped by maxBounces.
    private int bounceCount;

    // Filled in automatically.
    private Rigidbody2D body;

    // Optional: null means this cockroach has unlimited stamina (jumping is free).
    private Stamina stamina;

    // True when the feet are touching ground.
    private bool isGrounded;

    // Slope walking: true while touching a slope we are allowed to walk on.
    private bool isOnSlope;
    private bool wasOnSlope;
    private Vector2 slopeNormal = Vector2.up;

    // After a jump, slope walking is switched off briefly so it cannot cancel the jump.
    private float slopeLockTimer;
    private const float SlopeLockTime = 0.3f;
    private const float MinSlopeAngle = 3f; // flatter than this is just flat ground

    private float defaultGravityScale;
    private ContactFilter2D groundFilter;
    private readonly List<ContactPoint2D> contactBuffer = new List<ContactPoint2D>();

    // -1 = walking left, 0 = standing still, 1 = walking right.
    private float moveDirection;

    private float lockedAirDirection;

    // True while the jump key is being held on the ground.
    private bool isChargingJump;

    // How many seconds the jump key has been held this charge.
    private float jumpChargeTime;

    // Counts DOWN. Refilled to coyoteTime while on the ground; while above 0 we may still jump.
    private float coyoteTimer;

    // Counts DOWN. Set when jump is pressed but we can't jump yet; while above 0 the jump is "remembered".
    private float jumpBufferTimer;

    // True if the key was already let go while the press was waiting in the buffer (a quick tap before landing).
    private bool bufferedKeyReleased;

    // Short lockout after a jump so the ground check can't count as "still grounded" and allow a second jump.
    private float postJumpLockTimer;
    private const float PostJumpLockTime = 0.1f;

    // The cockroach's own collider, used to find where its head is for corner slipping.
    private Collider2D bodyCollider;

    // --- Read-only state for other scripts ---

    /// <summary>True while the feet are touching ground.</summary>
    public bool IsGrounded => isGrounded || isOnSlope;

    /// <summary>0 when standing still, 1 when walking.</summary>
    public float WalkAmount => Mathf.Abs(moveDirection);

    /// <summary>
    /// True from the moment an external push (an explosion) lands until the cockroach is
    /// back on the ground and under normal control again. Stays true through any bounces.
    /// </summary>
    public bool IsKnockedBack => physicsMovementActive;

    /// <summary>
    /// Raised every time an external push is applied. The vector is the push as it is actually
    /// applied (after the vertical multiplier), so its length is the push strength.
    /// KnockbackAnimation listens to this.
    /// </summary>
    public event System.Action<Vector2> OnKnockedBack;

    /// <summary>Raised the moment a jump launches. CockroachSfx listens to this.</summary>
    public event System.Action OnJumped;

    /// <summary>True while the jump key is held and a jump is charging.</summary>
    public bool IsChargingJump => isChargingJump;

    /// <summary>Jump charge from 0 (nothing) to 1 (full). Used by JumpPowerBar.</summary>
    public float JumpCharge01 => isChargingJump ? Mathf.Clamp01(jumpChargeTime / maxChargeTime) : 0f;

    /// <summary>Current vertical speed.</summary>
    public float VerticalSpeed => body != null ? body.linearVelocity.y : 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        stamina = GetComponent<Stamina>();
        bodyCollider = GetComponent<Collider2D>();

        defaultGravityScale = body.gravityScale;

        // Only touching the Ground layer counts as a slope.
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;
    }

    private void Update()
    {
        // Paused: drop any jump charge so releasing the key in the menu can't leave it stuck.
        if (GameMenu.IsPaused)
        {
            CancelJumpCharge();
            return;
        }

        // Read input in Update so key presses are not missed.
        // On a rope (ExternalControl) the rope reads the keys itself.
        if (!isMyTurn || ExternalControl)
        {
            moveDirection = 0f;
            CancelJumpCharge(); // turn ended mid-charge: throw the charge away
            jumpBufferTimer = 0f;
            return;
        }

        moveDirection = 0f;

        // Keys come from the settings menu (GameSettings), so players can rebind them.
        if (Input.GetKey(GameSettings.Key(GameAction.MoveLeft)))
            moveDirection = -1f;

        if (Input.GetKey(GameSettings.Key(GameAction.MoveRight)))
            moveDirection = 1f;

        UpdateJumpTimers();
        HandleJumpInput();
    }

    private void FixedUpdate()
    {
        CheckGrounded();

        // Something else (the grappling hook) is moving the body: plain physics, normal gravity.
        if (ExternalControl)
        {
            body.gravityScale = defaultGravityScale;
            isOnSlope = false;
            wasOnSlope = false;
            return;
        }

        if (physicsMovementActive)
        {
            physicsMovementElapsed += Time.fixedDeltaTime;

            if (!isGrounded)
            {
                // Remember the fastest downward speed seen while still airborne, so the
                // bounce below is based on "how hard did it actually hit" rather than
                // whatever's left after a collision may have already slowed it down.
                lastAirborneVerticalSpeed = body.linearVelocity.y;
                groundedStreak = 0f;
            }
            else
            {
                // First physics step actually touching ground this cycle: maybe bounce.
                if (groundedStreak <= 0f)
                {
                    float incomingSpeed = Mathf.Max(0f, -lastAirborneVerticalSpeed);
                    float bounceSpeed = incomingSpeed * bounciness;

                    if (enableBounce && bounceCount < maxBounces && bounceSpeed >= minBounceSpeed)
                    {
                        Vector2 v = body.linearVelocity;
                        v.y = bounceSpeed;
                        body.linearVelocity = v;
                        bounceCount++;
                        groundedStreak = 0f;
                    }
                    else
                    {
                        groundedStreak += Time.fixedDeltaTime;
                    }
                }
                else
                {
                    groundedStreak += Time.fixedDeltaTime;
                }
            }

            // Hand control back once the minimum time has passed AND we have been
            // continuously grounded for requiredGroundedTime — a single-frame "grounded"
            // reading can be wrong (bumpy terrain, a collider that hasn't updated yet
            // after an explosion), and ending knockback on that would show the landed
            // pose, or stop bouncing, while still visibly airborne.
            bool minTimePassed = physicsMovementElapsed >= minPhysicsMovementTime;
            bool steadyOnGround = groundedStreak >= requiredGroundedTime;

            if (minTimePassed && steadyOnGround)
            {
                physicsMovementActive = false;
                bounceCount = 0;
            }
        }
        else
        {
            groundedStreak = 0f;
            bounceCount = 0;
        }

        if (!physicsMovementActive)
        {
            isOnSlope = UpdateSlopeContact();
            ApplyWalkVelocity(isOnSlope);

            // Jump gravity tweaks make no sense while walking along a slope.
            if (!isOnSlope)
                ApplyGravityShaping();

            HandleCornerSlip();
        }
        else
        {
            // Knockback is pure physics: normal gravity, no slope walking.
            body.gravityScale = defaultGravityScale;
            isOnSlope = false;
            wasOnSlope = false;
        }

        FaceMoveDirection();
    }

    /// <summary>
    /// Applies an external physics push.
    /// Normal walking is temporarily disabled until the player lands (and stops bouncing).
    /// </summary>
    public void ApplyKnockback(Vector2 impulse)
    {
        if (physicsMovementUntilGrounded)
        {
            physicsMovementActive = true;
            physicsMovementElapsed = 0f;
            groundedStreak = 0f;
            bounceCount = 0;
            lastAirborneVerticalSpeed = body.linearVelocity.y;
        }

        // Reduce only the vertical component of the supplied impulse.
        impulse.y *= verticalPushMultiplier;

        body.AddForce(impulse, ForceMode2D.Impulse);

        OnKnockedBack?.Invoke(impulse);
    }

    /// <summary>
    /// True while something else moves this cockroach (the grappling hook): walking, jumping and
    /// slope handling all stop, and the body is left to physics.
    /// </summary>
    public bool ExternalControl { get; set; }

    /// <summary>
    /// Lets the body fly freely, keeping its current speed, until it lands. Like knockback, but
    /// with no push and no knockback animation. Used when letting go of the grappling hook.
    /// </summary>
    public void BeginFreeFall()
    {
        if (!physicsMovementUntilGrounded) return;

        physicsMovementActive = true;
        physicsMovementElapsed = 0f;
        groundedStreak = 0f;
        bounceCount = maxBounces;   // a rope landing should not bounce
        lastAirborneVerticalSpeed = body.linearVelocity.y;
    }

    /// <summary>
    /// Looks for ground in a small circle at the feet.
    /// </summary>
    private void CheckGrounded()
    {
        if (groundCheckPoint == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheckPoint.position,
            groundCheckRadius,
            groundLayer);
    }

    /// <summary>
    /// Sets horizontal walking speed while preserving vertical physics.
    /// </summary>
    /// <summary>
    /// Finds out whether we are touching a walkable slope (flat enough to climb, but not flat ground).
    /// Looks at real contacts with the ground instead of the tiny feet circle, because on a
    /// steep slope the ground touches the SIDE of the capsule, not the point at the feet.
    /// Also puts gravity back to normal each step (slope standing turns it off).
    /// </summary>
    private bool UpdateSlopeContact()
    {
        slopeLockTimer -= Time.fixedDeltaTime;
        body.gravityScale = defaultGravityScale;

        if (slopeLockTimer > 0f || maxClimbAngle <= MinSlopeAngle || bodyCollider == null)
            return false;

        body.GetContacts(groundFilter, contactBuffer);

        float centerY = bodyCollider.bounds.center.y;
        float bestUp = 0f;
        Vector2 bestNormal = Vector2.up;
        bool found = false;

        for (int i = 0; i < contactBuffer.Count; i++)
        {
            ContactPoint2D contact = contactBuffer[i];

            // Only contacts on the lower half of the body can be ground.
            if (contact.point.y > centerY)
                continue;

            // Make the normal point upward whichever way Unity reports it.
            Vector2 normal = contact.normal;
            if (normal.y < 0f)
                normal = -normal;

            if (normal.y > bestUp)
            {
                bestUp = normal.y;
                bestNormal = normal;
                found = true;
            }
        }

        if (!found)
            return false;

        float angle = Vector2.Angle(bestNormal, Vector2.up);
        if (angle < MinSlopeAngle || angle > maxClimbAngle)
            return false;

        slopeNormal = bestNormal;
        return true;
    }

    private void ApplyWalkVelocity(bool onSlope)
    {
        Vector2 velocity = body.linearVelocity;

        if (onSlope)
        {
            if (moveDirection != 0f)
            {
                // Walk ALONG the slope (up or down it) instead of pushing into it.
                Vector2 tangent = new Vector2(slopeNormal.y, -slopeNormal.x);
                velocity = tangent * (moveDirection * walkSpeed);
            }
            else
            {
                // Standing still on a slope: stop and switch gravity off so we do not slide down.
                velocity = Vector2.zero;
                body.gravityScale = 0f;
            }

            lockedAirDirection = moveDirection;
            wasOnSlope = true;
            body.linearVelocity = velocity;
            return;
        }

        // Just stepped off a slope (e.g. the top of the hill): drop the upward speed we got
        // from climbing, otherwise we would be launched into the air.
        if (wasOnSlope)
        {
            if (velocity.y > 0f)
                velocity.y = 0f;

            wasOnSlope = false;
        }

        if (isGrounded)
        {
            velocity.x = moveDirection * walkSpeed;

            // Remember our walking direction so walking off a ledge carries on that way.
            lockedAirDirection = moveDirection;
        }
        else
        {
            // Air control: is steering allowed in the current phase of the jump?
            bool rising = velocity.y > 0f;
            bool canSteer = rising ? steerWhileRising : steerWhileFalling;

            if (canSteer)
            {
                // Pressing A / D sets the direction. With no key held we either
                // stop (0) or keep the last direction, depending on the checkbox.
                if (moveDirection != 0f || stopInAirWhenNoInput)
                    lockedAirDirection = moveDirection;
            }

            // Move sideways speed TOWARD the target instead of snapping to it.
            // The slower airAcceleration is, the weaker the steering feels.
            float targetSpeed = lockedAirDirection * walkSpeed;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, airAcceleration * Time.fixedDeltaTime);
        }

        body.linearVelocity = velocity;
    }

    /// <summary>
    /// Ticks the coyote and buffer timers. Called every frame during our turn.
    /// </summary>
    private void UpdateJumpTimers()
    {
        postJumpLockTimer -= Time.deltaTime;
        jumpBufferTimer -= Time.deltaTime;

        // On the ground: keep the coyote window full.
        // In the air: let it run out. (Not refilled right after a jump, see postJumpLockTimer.)
        if ((isGrounded || isOnSlope) && postJumpLockTimer <= 0f)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    /// <summary>
    /// True if a jump (or jump charge) is allowed right now.
    /// Uses the coyote timer instead of isGrounded, so you can still jump just after leaving a ledge.
    /// </summary>
    private bool CanJump()
    {
        // No stamina left = no jump. (A cockroach without a Stamina component can always jump.)
        bool hasStamina = stamina == null || stamina.CurrentStamina > 0f;

        return coyoteTimer > 0f && !physicsMovementActive && hasStamina;
    }

    /// <summary>
    /// Hold the jump key to charge, release to jump.
    /// A quick tap gives minJumpPower, a full hold gives maxJumpPower.
    /// Also handles the input buffer (a press just before landing is remembered).
    /// </summary>
    private void HandleJumpInput()
    {
        bool canJump = CanJump();
        KeyCode jumpKey = GameSettings.Key(GameAction.Jump);

        if (Input.GetKeyDown(jumpKey))
        {
            if (canJump)
            {
                StartJumpCharge();
            }
            else
            {
                // Can't jump yet (e.g. still in the air): remember the press for a moment.
                jumpBufferTimer = jumpBufferTime;
                bufferedKeyReleased = false;
            }
        }

        // The key was let go while the press was still waiting in the buffer.
        if (jumpBufferTimer > 0f && !isChargingJump && Input.GetKeyUp(jumpKey))
            bufferedKeyReleased = true;

        // A remembered press is used up the moment we are allowed to jump.
        if (jumpBufferTimer > 0f && !isChargingJump && canJump)
        {
            jumpBufferTimer = 0f;

            if (bufferedKeyReleased)
                FireJump(0f);           // tapped before landing: instant small hop on landing
            else if (Input.GetKey(jumpKey))
                StartJumpCharge();      // still holding: start charging on landing
        }

        if (!isChargingJump)
            return;

        // If a blast pushed us, or the coyote window ran out in the air, the charge is no longer valid.
        if (physicsMovementActive || coyoteTimer <= 0f)
        {
            CancelJumpCharge();
            return;
        }

        // Keep charging up to the maximum, then stay at full power until the key is released.
        jumpChargeTime = Mathf.Min(jumpChargeTime + Time.deltaTime, maxChargeTime);

        // The jump ONLY happens when the key is released.
        if (Input.GetKeyUp(jumpKey))
        {
            FireJump(JumpCharge01);
        }
    }

    private void StartJumpCharge()
    {
        isChargingJump = true;
        jumpChargeTime = 0f;
    }

    private void CancelJumpCharge()
    {
        isChargingJump = false;
        jumpChargeTime = 0f;
    }

    /// <summary>
    /// Launches the jump, spends stamina and resets the jump timers.
    /// </summary>
    private void FireJump(float charge01)
    {
        Jump(charge01);

        // Stronger jump = more stamina spent.
        if (stamina != null)
            stamina.SpendForJump(charge01);

        // Use up the coyote window and buffer so one press can never give two jumps.
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        postJumpLockTimer = PostJumpLockTime;

        // Switch slope walking off briefly so it cannot cancel the jump.
        slopeLockTimer = SlopeLockTime;
        isOnSlope = false;
        wasOnSlope = false;

        CancelJumpCharge();
    }

    /// <summary>
    /// Launches the jump. charge01 is 0..1; power is always kept between min and max.
    /// </summary>
    private void Jump(float charge01)
    {
        lockedAirDirection = moveDirection;

        float power = Mathf.Lerp(minJumpPower, maxJumpPower, charge01);
        // Safety clamp so the power can never leave the min/max range,
        // even if the Inspector values are entered the wrong way round.
        power = Mathf.Clamp(power, Mathf.Min(minJumpPower, maxJumpPower), Mathf.Max(minJumpPower, maxJumpPower));

        // Set the upward speed directly (instead of adding force) so the same
        // charge always gives the same jump height, however fast we were moving vertically.
        Vector2 velocity = body.linearVelocity;
        velocity.y = power;
        body.linearVelocity = velocity;

        OnJumped?.Invoke();
    }

    /// <summary>
    /// Changes how strong gravity is depending on where we are in the jump:
    ///  - near the top of the arc: weaker (apex hang time, a moment to adjust)
    ///  - falling: stronger (snappy, less floaty fall)
    ///  - rising: normal
    /// Works by adding or removing a bit of gravity on top of Unity's own.
    /// </summary>
    private void ApplyGravityShaping()
    {
        if (isGrounded)
            return;

        float verticalSpeed = body.linearVelocity.y;
        float multiplier = 1f;

        if (Mathf.Abs(verticalSpeed) < apexThreshold)
            multiplier = apexGravityMultiplier;
        else if (verticalSpeed < 0f)
            multiplier = fallGravityMultiplier;

        float extraGravity = Physics2D.gravity.y * body.gravityScale * (multiplier - 1f);
        body.linearVelocity += Vector2.up * extraGravity * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Corner slipping: while rising, if only a small sliver of the head's outer edge
    /// would clip the corner of a platform above, nudge sideways around it instead of bonking.
    /// </summary>
    private void HandleCornerSlip()
    {
        if (cornerSlipWidth <= 0f || bodyCollider == null)
            return;

        if (body.linearVelocity.y <= 0f)
            return;

        GetCornerProbes(out Bounds bounds, out float distance);

        // Outer rays sit right at the head's edges; inner rays sit cornerSlipWidth further in.
        bool outerLeft = HeadBlocked(bounds.min.x + EdgeInset, bounds.max.y, distance);
        bool innerLeft = HeadBlocked(bounds.min.x + cornerSlipWidth, bounds.max.y, distance);
        bool outerRight = HeadBlocked(bounds.max.x - EdgeInset, bounds.max.y, distance);
        bool innerRight = HeadBlocked(bounds.max.x - cornerSlipWidth, bounds.max.y, distance);

        // Left edge clips but the inner ray is clear = just a sliver: nudge right.
        // Never nudge if the other side is blocked too (that is a real ceiling).
        if (outerLeft && !innerLeft && !outerRight)
            body.position += Vector2.right * cornerNudgeAmount;
        else if (outerRight && !innerRight && !outerLeft)
            body.position += Vector2.left * cornerNudgeAmount;
    }

    // Tiny gap from the very edge so the outer rays do not start exactly on the collider's corner.
    private const float EdgeInset = 0.02f;

    private void GetCornerProbes(out Bounds bounds, out float distance)
    {
        bounds = bodyCollider.bounds;
        distance = cornerCheckDistance + Mathf.Max(0f, body.linearVelocity.y) * Time.fixedDeltaTime;
    }

    private bool HeadBlocked(float x, float y, float distance)
    {
        // Only the ground layer counts, so the cockroach's own collider is never hit.
        return Physics2D.Raycast(new Vector2(x, y), Vector2.up, distance, groundLayer).collider != null;
    }

    /// <summary>
    /// Flips the sprite so the cockroach faces the direction it is walking.
    /// </summary>
    private void FaceMoveDirection()
    {
        if (moveDirection == 0f)
            return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveDirection);
        transform.localScale = scale;
    }

    /// <summary>
    /// Draws the ground check circle in the Scene view.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                groundCheckPoint.position,
                groundCheckRadius);
        }

        // Corner slip probes: yellow = outer rays (head edges), cyan = inner rays (slip width).
        Collider2D col = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        if (col != null && cornerSlipWidth > 0f)
        {
            Bounds b = col.bounds;
            float y = b.max.y;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector2(b.min.x + EdgeInset, y), new Vector2(b.min.x + EdgeInset, y + cornerCheckDistance));
            Gizmos.DrawLine(new Vector2(b.max.x - EdgeInset, y), new Vector2(b.max.x - EdgeInset, y + cornerCheckDistance));

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector2(b.min.x + cornerSlipWidth, y), new Vector2(b.min.x + cornerSlipWidth, y + cornerCheckDistance));
            Gizmos.DrawLine(new Vector2(b.max.x - cornerSlipWidth, y), new Vector2(b.max.x - cornerSlipWidth, y + cornerCheckDistance));
        }
    }
}