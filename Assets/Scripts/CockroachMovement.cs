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
    [Tooltip("Walk speed in units per second. Keep this slow, artillery games are not platformers.")]
    public float walkSpeed = 3f;

    [Header("Jumping")]
    [Tooltip("Upward force applied once when W is pressed.")]
    public float jumpForce = 6f;

    [Header("Ground Check")]
    [Tooltip("Empty child object placed at the cockroach's feet.")]
    public Transform groundCheckPoint;

    [Tooltip("Radius of the circle used to look for ground. Roughly half the body width.")]
    public float groundCheckRadius = 0.15f;

    [Tooltip("Which layers count as ground. Set this to your Ground layer.")]
    public LayerMask groundLayer;

    [Header("Turn Control")]
    [Tooltip("When false the player ignores all input. The turn system will control this later.")]
    public bool isMyTurn = true;

    // Filled in automatically.
    private Rigidbody2D body;

    // True when the feet are touching ground. Updated once per frame.
    private bool isGrounded;

    // -1 = walking left, 0 = standing still, 1 = walking right.
    private float moveDirection;

    // --- Read-only state for other scripts (the animator reads these) ---

    /// <summary>True while the feet are touching ground.</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>0 when standing still, 1 when walking. Ignores direction.</summary>
    public float WalkAmount => Mathf.Abs(moveDirection);

    /// <summary>Current vertical speed. Positive is rising, negative is falling.</summary>
    public float VerticalSpeed => body != null ? body.linearVelocity.y : 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Read input in Update so a key press is never missed between physics steps.
        if (!isMyTurn)
        {
            moveDirection = 0f;
            return;
        }

        moveDirection = 0f;
        if (Input.GetKey(KeyCode.A)) moveDirection = -1f;
        if (Input.GetKey(KeyCode.D)) moveDirection = 1f;

        if (Input.GetKeyDown(KeyCode.W) && isGrounded)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        // Physics work belongs in FixedUpdate.
        CheckGrounded();
        ApplyWalkVelocity();
        FaceMoveDirection();
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
    /// Sets horizontal speed directly but leaves vertical speed alone,
    /// so gravity and jumping still behave normally.
    /// </summary>
    private void ApplyWalkVelocity()
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveDirection * walkSpeed;
        body.linearVelocity = velocity;
    }

    private void Jump()
    {
        body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Flips the sprite so the cockroach faces the way it is walking.
    /// Does nothing while standing still, so it keeps its last facing.
    /// </summary>
    private void FaceMoveDirection()
    {
        if (moveDirection == 0f) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveDirection);
        transform.localScale = scale;
    }

    /// <summary>
    /// Draws the ground check circle in the Scene view so you can see and size it.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }
}
