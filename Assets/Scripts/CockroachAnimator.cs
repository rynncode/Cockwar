using UnityEngine;

/// <summary>
/// Step 2: Player animations.
/// This script does not animate anything itself. It reads the movement state
/// each frame and reports it to the Animator, which decides which clip to play.
///
/// Required Animator parameters (spelling and capitalisation must match):
///   WalkAmount  - Float
///   IsGrounded  - Bool
///   Die         - Trigger
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CockroachMovement))]
public class CockroachAnimator : MonoBehaviour
{
    private Animator animator;
    private CockroachMovement movement;

    // True once the cockroach has died, so we stop feeding it movement states.
    private bool isDead;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<CockroachMovement>();
    }

    private void Update()
    {
        if (isDead) return;

        animator.SetFloat("WalkAmount", movement.WalkAmount);
        animator.SetBool("IsGrounded", movement.IsGrounded);
    }

    /// <summary>
    /// Plays the death animation once. Nothing calls this yet.
    /// The death system (step 8) will call it later.
    /// </summary>
    public void PlayDeath()
    {
        if (isDead) return;
        isDead = true;
        animator.SetTrigger("Die");
    }
}
