using UnityEngine;

/// <summary>
/// Knockback animation: when a cockroach is blasted hard enough, it plays
///   1. tumbling through the air,
///   2. lying on the ground where it landed,
///   3. dizzy spiral stars (loops for a moment),
///   4. back to the normal idle animation.
/// A slight push (below Min Knockback Strength) plays nothing.
///
/// If gravity carries the cockroach off the ground again mid-sequence (e.g. it
/// landed right at the edge of a crater the same blast just opened up, or it's
/// bouncing per CockroachMovement's bounce settings), Landed and Dizzy both drop
/// back to Flying instead of freezing their pose in mid-air.
///
/// This script swaps the sprite itself every frame while the sequence plays, so you do not
/// need to touch the Animator Controller. When it ends, the Animator takes over again.
/// The HealthBar waits for this to finish before it counts the health down.
///
/// Add this component to each cockroach, then drag the 12 sprites of the knockback sheet
/// onto its Frames field.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
public class KnockbackAnimation : MonoBehaviour
{
    [Header("Sprite Renderer")]
    [Tooltip("The SpriteRenderer this script draws the knockback frames on. Drag the main BODY sprite renderer here.")]
    public SpriteRenderer spriteRenderer;

    [Header("Frames")]
    [Tooltip("All 12 sprites from the sliced knockback sheet. Select them all in the Project window and drag them onto this field.")]
    public Sprite[] frames;

    [Tooltip("How many frames at the start are the tumbling-in-the-air loop. Frames 0, 1 and 2 = 3.")]
    public int flyFrameCount = 3;

    [Tooltip("Which frame is the cockroach lying on the ground (frame 3 on your sheet).")]
    public int landedFrameIndex = 3;

    [Tooltip("First frame of the dizzy stars loop. It runs from here to the last frame (frames 4 to 11 on your sheet).")]
    public int firstDizzyFrame = 4;

    [Header("When it plays")]
    [Tooltip("The push has to be at least this strong to play the animation. Slighter pushes are ignored.")]
    public float minKnockbackStrength = 20f;

    [Tooltip("Print each push's strength in the Console, to help you tune Min Knockback Strength.")]
    public bool logKnockbackStrength = true;

    [Header("Timing")]
    [Tooltip("Speed of the tumbling frames while flying.")]
    public float flyFramesPerSecond = 10f;

    [Tooltip("Seconds the cockroach lies still on the 'landed' frame before the stars start.")]
    public float landedHoldTime = 0.2f;

    [Tooltip("Speed of the spiral stars loop.")]
    public float dizzyFramesPerSecond = 10f;

    [Tooltip("Seconds the dizzy stars play before going back to idle.")]
    public float dizzyDuration = 1.6f;

    [Tooltip("Safety: the animation always ends after this many seconds, even if the cockroach never lands.")]
    public float maxAnimationSeconds = 10f;

    [Header("Facing")]
    [Tooltip("When on, the cockroach faces the blast while it tumbles and lies there (it is thrown backwards). Off = it keeps whichever way it was facing.")]
    public bool faceTowardBlast = true;

    [Tooltip("Which way the sprites on the sheet face as drawn. Your sheet faces LEFT, so leave this off.")]
    public bool sheetFacesRight = false;

    [Header("Collider Settings")]
    [Tooltip("When true, switches the CapsuleCollider2D to horizontal while the knockback/dizzy sequence plays.")]
    public bool adjustColliderOnKnockback = true;

    [Tooltip("Size of the horizontal collider while lying down / dizzy (Width, Height).")]
    public Vector2 horizontalColliderSize = new Vector2(2.4f, 1.0f);

    [Tooltip("Offset of the horizontal collider while lying down / dizzy.")]
    public Vector2 horizontalColliderOffset = new Vector2(0f, -0.1f);

    private enum State { Inactive, Flying, Landed, Dizzy }

    private CockroachMovement movement;
    private Health health;

    private Animator animator;
    private SpriteRenderer[] allRenderers;

    private CapsuleCollider2D capsuleCollider;
    private CapsuleDirection2D originalDirection;
    private Vector2 originalSize;
    private Vector2 originalOffset;

    private State state = State.Inactive;
    private float stateTime;
    private float totalTime;

    private Sprite spriteBefore;
    private bool flipXBefore;

    private bool ready;

    /// <summary>True from the moment a strong push lands until the cockroach is back to idle.</summary>
    public bool IsPlaying => state != State.Inactive;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        health = GetComponent<Health>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Cache Animator and all child SpriteRenderers
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        allRenderers = GetComponentsInChildren<SpriteRenderer>();

        // Cache CapsuleCollider2D and store default standing values
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (capsuleCollider != null)
        {
            originalDirection = capsuleCollider.direction;
            originalSize = capsuleCollider.size;
            originalOffset = capsuleCollider.offset;
        }

        ready = CheckSetup();
    }

    private bool CheckSetup()
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning("KnockbackAnimation on " + name + ": no SpriteRenderer found, so the animation is switched off.");
            return false;
        }

        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("KnockbackAnimation on " + name + ": the Frames list is empty. Drag the sliced knockback sprites onto it.");
            return false;
        }

        foreach (Sprite frame in frames)
        {
            if (frame == null)
            {
                Debug.LogWarning("KnockbackAnimation on " + name + ": one of the Frames is empty. The animation is switched off.");
                return false;
            }
        }

        SortFramesByNumber();

        if (flyFrameCount < 1 || landedFrameIndex < 0 || landedFrameIndex >= frames.Length ||
            firstDizzyFrame < 0 || firstDizzyFrame >= frames.Length || flyFrameCount > frames.Length)
        {
            Debug.LogWarning("KnockbackAnimation on " + name + ": the frame numbers don't fit the " + frames.Length + " frames given.");
            return false;
        }

        return true;
    }

    private void SortFramesByNumber()
    {
        foreach (Sprite frame in frames)
        {
            if (!TryGetFrameNumber(frame, out _))
                return;
        }

        System.Array.Sort(frames, (a, b) =>
        {
            TryGetFrameNumber(a, out int numberA);
            TryGetFrameNumber(b, out int numberB);
            return numberA.CompareTo(numberB);
        });
    }

    private static bool TryGetFrameNumber(Sprite sprite, out int number)
    {
        number = 0;
        string spriteName = sprite.name;
        int underscore = spriteName.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out number);
    }

    private void OnEnable()
    {
        movement.OnKnockedBack += HandleKnockedBack;
    }

    private void OnDisable()
    {
        movement.OnKnockedBack -= HandleKnockedBack;
        EndAnimation();
    }

    private void HandleKnockedBack(Vector2 push)
    {
        float strength = push.magnitude;

        if (logKnockbackStrength)
        {
            Debug.Log(name + " was pushed with strength " + strength.ToString("0.0") +
                      (strength >= minKnockbackStrength ? " (plays the knockback animation)" : " (too slight, no animation)") + ".");
        }

        if (!ready || strength < minKnockbackStrength)
            return;

        if (health != null && health.IsDead)
            return;

        if (state == State.Inactive)
        {
            spriteBefore = spriteRenderer.sprite;
            flipXBefore = spriteRenderer.flipX;

            // --- Disable Animator so it stops fighting the script ---
            if (animator != null)
                animator.enabled = false;

            // --- Hide secondary child sprite renderers (arms/weapons) ---
            foreach (var sr in allRenderers)
            {
                if (sr != null && sr != spriteRenderer)
                    sr.enabled = false;
            }

            SetColliderHorizontal(true);
        }

        ApplyFacing(push);

        state = State.Flying;
        stateTime = 0f;
        totalTime = 0f;
    }

    private void ApplyFacing(Vector2 push)
    {
        spriteRenderer.flipX = flipXBefore;

        if (!faceTowardBlast || Mathf.Abs(push.x) < 0.01f)
            return;

        bool wantFacingRight = push.x < 0f;
        bool facingRightWithoutFlip = sheetFacesRight != (transform.lossyScale.x < 0f);

        spriteRenderer.flipX = wantFacingRight != facingRightWithoutFlip;
    }

    private void LateUpdate()
    {
        if (state == State.Inactive)
            return;

        if (health != null && health.IsDead)
        {
            EndAnimation();
            return;
        }

        stateTime += Time.deltaTime;
        totalTime += Time.deltaTime;

        if (totalTime > maxAnimationSeconds)
        {
            EndAnimation();
            return;
        }

        switch (state)
        {
            case State.Flying:
                ShowFrame((int)(stateTime * flyFramesPerSecond) % flyFrameCount);

                if (!movement.IsKnockedBack)
                {
                    state = State.Landed;
                    stateTime = 0f;
                }
                break;

            case State.Landed:
                if (!movement.IsGrounded)
                {
                    state = State.Flying;
                    stateTime = 0f;
                    break;
                }

                ShowFrame(landedFrameIndex);

                if (stateTime >= landedHoldTime)
                {
                    state = State.Dizzy;
                    stateTime = 0f;
                }
                break;

            case State.Dizzy:
                if (!movement.IsGrounded)
                {
                    state = State.Flying;
                    stateTime = 0f;
                    break;
                }

                int dizzyCount = frames.Length - firstDizzyFrame;
                ShowFrame(firstDizzyFrame + (int)(stateTime * dizzyFramesPerSecond) % dizzyCount);

                if (stateTime >= dizzyDuration)
                {
                    EndAnimation();
                }
                break;
        }
    }

    private void ShowFrame(int index)
    {
        spriteRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    public void Skip()
    {
        EndAnimation();
    }

    private void EndAnimation()
    {
        if (state == State.Inactive)
            return;

        state = State.Inactive;

        SetColliderHorizontal(false);

        // --- Re-enable Animator and child renderers ---
        if (animator != null)
            animator.enabled = true;

        if (allRenderers != null)
        {
            foreach (var sr in allRenderers)
            {
                if (sr != null)
                    sr.enabled = true;
            }
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipXBefore;
            if (spriteBefore != null)
                spriteRenderer.sprite = spriteBefore;
        }
    }

    /// <summary>
    /// Swaps the CapsuleCollider2D between horizontal (lying down) and vertical (standing up).
    /// </summary>
    private void SetColliderHorizontal(bool enableHorizontal)
    {
        if (capsuleCollider == null || !adjustColliderOnKnockback) return;

        if (enableHorizontal)
        {
            capsuleCollider.direction = CapsuleDirection2D.Horizontal;
            capsuleCollider.size = horizontalColliderSize;
            capsuleCollider.offset = horizontalColliderOffset;
        }
        else
        {
            capsuleCollider.direction = originalDirection;
            capsuleCollider.size = originalSize;
            capsuleCollider.offset = originalOffset;
        }
    }
}