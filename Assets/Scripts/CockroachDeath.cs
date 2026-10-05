using System.Collections;
using UnityEngine;

/// <summary>
/// Step 8: Death.
/// When Health.OnDeath fires, this plays a short death sequence instead of exploding at once:
///   1. input is switched off straight away (movement, aiming, shooting),
///   2. the camera glides and zooms in on the loser,
///   3. the health number finishes counting down to 0,
///   4. the cockroach waits until it is on the ground and not moving,
///   5. the dizzy "nalipong" animation plays (sprites from the Aseprite sheet),
///   6. the normal death animation (the explosion) plays,
///   7. the camera goes back (or on to the winner) and the GameObject is switched off.
/// TurnManager waits for IsPlaying to be false before it moves on.
/// The Rigidbody2D is left alone, so gravity still pulls the body down and it
/// settles on the ground naturally.
/// </summary>
[RequireComponent(typeof(Health))]
public class CockroachDeath : MonoBehaviour
{
    [Header("Death animation (the explosion)")]
    [Tooltip("How long to wait after the death animation starts before disabling this GameObject. Match this to your death animation clip's length.")]
    public float deathAnimationDuration = 1f;

    [Header("Dizzy ('nalipong') animation")]
    [Tooltip("The 16 sprites from the sliced nalipong sheet. Select them all in the Project window and drag them onto this field. They are put in order by their _0, _1 ... _15 numbers automatically. Leave empty to skip the dizzy stage.")]
    public Sprite[] dizzyFrames;

    [Tooltip("Speed of the dizzy animation. The Aseprite file uses 100 ms per frame = 10.")]
    public float dizzyFramesPerSecond = 10f;

    [Tooltip("Seconds the dizzy animation plays. 1.6 = all 16 frames once.")]
    public float dizzyDuration = 1.6f;

    [Header("Waiting for the ground")]
    [Tooltip("The cockroach must be on the ground AND nearly still for this many seconds before the dizzy animation starts.")]
    public float settleStillTime = 0.3f;

    [Tooltip("Slower than this (world units per second) counts as 'not moving'.")]
    public float stillSpeed = 0.3f;

    [Tooltip("Safety: if it has not settled after this many seconds (for example it keeps sliding), carry on anyway.")]
    public float maxSettleWait = 6f;

    [Tooltip("Which layers count as ground. Leave empty to use the layer named 'Ground'.")]
    public LayerMask groundLayers;

    [Header("Camera")]
    [Tooltip("Pan and zoom the camera onto the loser when it dies.")]
    public bool focusCamera = true;

    [Tooltip("Zoom while looking at the loser (Orthographic Size). Smaller = closer.")]
    public float cameraZoom = 30f;

    [Tooltip("How smooth the camera pan and zoom are. Bigger = slower and softer.")]
    public float cameraSmoothTime = 0.7f;

    [Tooltip("Minimum seconds on the loser before the dizzy stage, so the camera has time to arrive.")]
    public float cameraArriveTime = 1f;

    private const float ZoomBackTime = 0.8f;      // seconds spent zooming back out when the match goes on
    private const float CountdownMaxWait = 10f;   // safety limit for waiting on the health number

    private Health health;
    private CockroachMovement movement;
    private CockroachAim aim;
    private CockroachShooting shooting;
    private CockroachAnimator animator;
    private HealthBar healthBar;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;

    private float defaultGravityScale;
    private LayerMask groundMask;

    // Only one death sequence runs at a time (if two cockroaches die at once, the second waits).
    private static CockroachDeath currentlyPlaying;

    private bool showingDizzy;
    private float dizzyTime;

    /// <summary>True from the moment this cockroach dies until its death sequence is completely finished.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Set by things that kill a cockroach somewhere it can't stand (like the acid):
    /// the sequence then skips waiting for the ground and the dizzy animation.
    /// </summary>
    public bool SkipDizzyStage { get; set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        movement = GetComponent<CockroachMovement>();
        aim = GetComponent<CockroachAim>();
        shooting = GetComponent<CockroachShooting>();
        animator = GetComponent<CockroachAnimator>();
        healthBar = GetComponent<HealthBar>();
        body = GetComponent<Rigidbody2D>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Remembered now, because CockroachMovement can switch gravity off while standing on a slope.
        if (body != null)
            defaultGravityScale = body.gravityScale;

        groundMask = groundLayers.value != 0 ? groundLayers : (LayerMask)LayerMask.GetMask("Ground");

        SortDizzyFramesByNumber();
    }

    /// <summary>
    /// Dragging sprites into a list can put them in any order, so sort them by the number
    /// at the end of their name (sheet_0, sheet_1 ... sheet_15) when every name has one.
    /// </summary>
    private void SortDizzyFramesByNumber()
    {
        if (dizzyFrames == null || dizzyFrames.Length < 2)
            return;

        foreach (Sprite frame in dizzyFrames)
        {
            if (frame == null || !TryGetFrameNumber(frame, out _))
                return; // an empty slot or a name without a number: keep the order as given
        }

        System.Array.Sort(dizzyFrames, (a, b) =>
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
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;

        // If something switches us off in the middle of the sequence, don't leave everyone waiting.
        if (currentlyPlaying == this)
            currentlyPlaying = null;

        IsPlaying = false;
        showingDizzy = false;
    }

    private void HandleDeath()
    {
        if (IsPlaying) return;
        IsPlaying = true;

        // Stop reading input. Each of these is optional on purpose, in case
        // a future object using Health does not have all of them.
        if (movement != null) movement.enabled = false;
        if (aim != null) aim.enabled = false;
        if (shooting != null) shooting.enabled = false;

        // Standing on a slope switches gravity off in CockroachMovement. That script is now off
        // and can't switch it back on, so do it here, or a body whose ground was blasted away would float.
        if (body != null)
            body.gravityScale = defaultGravityScale;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // One at a time: if another cockroach is mid-sequence, wait for it.
        while (currentlyPlaying != null && currentlyPlaying != this)
            yield return null;
        currentlyPlaying = this;

        // Let the rest of this frame's code run first (TurnManager may be placing the camera
        // on the explosion right now), so our camera move is the last word.
        yield return null;

        // 1. Camera glides and zooms in on the loser.
        CameraController cam = focusCamera ? FindFirstObjectByType<CameraController>() : null;
        float originalZoom = 0f;
        float cameraStart = Time.time;

        if (cam != null)
        {
            originalZoom = cam.TargetZoom;
            cam.BeginIntro(cameraSmoothTime);
            cam.SetZoom(cameraZoom);
            cam.SetTarget(transform);
        }

        // 2. Let the health number finish counting down to 0 (Enter skips it, as usual).
        float waited = 0f;
        while (healthBar != null && healthBar.IsAnimating && waited < CountdownMaxWait)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Make sure the camera really is on the loser, and has had time to get there.
        if (cam != null)
        {
            cam.SetTarget(transform);

            float remaining = cameraArriveTime - (Time.time - cameraStart);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }

        // 3 + 4. On the ground and still, then dizzy. Skipped if it died somewhere it can't stand.
        if (!SkipDizzyStage)
        {
            yield return WaitUntilSettled();
            yield return PlayDizzy();
        }

        // 5. The normal death animation (the explosion).
        if (animator != null)
            animator.PlayDeath();

        yield return new WaitForSeconds(deathAnimationDuration);

        // 6. Tidy up. All of this must happen BEFORE SetActive(false), because switching
        //    this object off also stops this coroutine.
        if (cam != null)
        {
            if (MatchIsOver())
            {
                // The game-over sequence is about to take the camera to the winner. Leave it.
            }
            else
            {
                cam.SetZoom(originalZoom);
                yield return new WaitForSeconds(ZoomBackTime);
                cam.EndIntro();
            }
        }

        currentlyPlaying = null;
        IsPlaying = false;
        gameObject.SetActive(false);
    }

    /// <summary>Waits until the cockroach is touching the ground and has stopped moving.</summary>
    private IEnumerator WaitUntilSettled()
    {
        float waited = 0f;
        float stillFor = 0f;

        while (waited < maxSettleWait)
        {
            bool onGround = body == null || body.IsTouchingLayers(groundMask);
            bool still = body == null || body.linearVelocity.sqrMagnitude < stillSpeed * stillSpeed;

            if (onGround && still)
            {
                stillFor += Time.deltaTime;
                if (stillFor >= settleStillTime)
                    yield break;
            }
            else
            {
                stillFor = 0f;
            }

            waited += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PlayDizzy()
    {
        if (dizzyFrames == null || dizzyFrames.Length == 0 || spriteRenderer == null)
            yield break;

        showingDizzy = true;
        dizzyTime = 0f;

        while (dizzyTime < dizzyDuration)
        {
            yield return null;
            dizzyTime += Time.deltaTime;
        }

        showingDizzy = false;
    }

    // LateUpdate runs after the Animator has chosen its sprite for this frame,
    // so the dizzy sprite we set here is the one that is drawn.
    private void LateUpdate()
    {
        if (!showingDizzy || spriteRenderer == null || dizzyFrames == null || dizzyFrames.Length == 0)
            return;

        int frame = (int)(dizzyTime * dizzyFramesPerSecond) % dizzyFrames.Length;
        spriteRenderer.sprite = dizzyFrames[frame];
    }

    /// <summary>True when this death leaves one cockroach or none standing.</summary>
    private bool MatchIsOver()
    {
        CockroachMovement[] everyone = FindObjectsByType<CockroachMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int total = 0;
        int alive = 0;

        foreach (CockroachMovement other in everyone)
        {
            total++;

            Health otherHealth = other.GetComponent<Health>();
            if (other.gameObject.activeInHierarchy && (otherHealth == null || !otherHealth.IsDead))
                alive++;
        }

        return total > 1 && alive <= 1;
    }
}
