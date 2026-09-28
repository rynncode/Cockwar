using UnityEngine;

/// <summary>
/// Step 1: Player movement.
/// A / D walk left and right, W jumps.
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
    [Tooltip("Upward force applied once when W is pressed.")]
    public float jumpForce = 6f;

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

    // True while normal player movement is temporarily disabled
    // because the Rigidbody is being controlled by physics.
    private bool physicsMovementActive;

    // Filled in automatically.
    private Rigidbody2D body;

    // True when the feet are touching ground.
    private bool isGrounded;

    // Used to detect the moment the player lands.
    private bool wasGrounded;

    // -1 = walking left, 0 = standing still, 1 = walking right.
    private float moveDirection;

    private float lockedAirDirection;

    // --- Read-only state for other scripts ---

    /// <summary>True while the feet are touching ground.</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>0 when standing still, 1 when walking.</summary>
    public float WalkAmount => Mathf.Abs(moveDirection);

    /// <summary>Current vertical speed.</summary>
    public float VerticalSpeed => body != null ? body.linearVelocity.y : 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Read input in Update so key presses are not missed.
        if (!isMyTurn)
        {
            moveDirection = 0f;
            return;
        }

        moveDirection = 0f;

        if (Input.GetKey(KeyCode.A))
            moveDirection = -1f;

        if (Input.GetKey(KeyCode.D))
            moveDirection = 1f;

        if (Input.GetKeyDown(KeyCode.W) && isGrounded && !physicsMovementActive)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        CheckGrounded();

        // If physics movement was active and we have now landed,
        // return control to the player.
        if (physicsMovementActive && !wasGrounded && isGrounded)
        {
            physicsMovementActive = false;
        }

        if (!physicsMovementActive)
        {
            ApplyWalkVelocity();
        }

        FaceMoveDirection();

        wasGrounded = isGrounded;
    }

    /// <summary>
    /// Applies an external physics push.
    /// Normal walking is temporarily disabled until the player lands.
    /// </summary>
    public void ApplyKnockback(Vector2 impulse)
    {
        if (physicsMovementUntilGrounded)
        {
            physicsMovementActive = true;
        }

        // Reduce only the vertical component of the supplied impulse.
        impulse.y *= verticalPushMultiplier;

        body.AddForce(impulse, ForceMode2D.Impulse);
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
    private void ApplyWalkVelocity()
{
    Vector2 velocity = body.linearVelocity;

    if (isGrounded)
    {
        velocity.x = moveDirection * walkSpeed;
    }
    else
    {
        velocity.x = lockedAirDirection * walkSpeed;
    }

    body.linearVelocity = velocity;
}

    private void Jump()
    {
        lockedAirDirection = moveDirection;

        body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
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
        if (groundCheckPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            groundCheckPoint.position,
            groundCheckRadius);
    }
}

