using UnityEngine;

/// <summary>
/// Step 8: Death.
/// Listens for Health.OnDeath and reacts: stops the player's scripts from
/// reading input (movement, aiming, shooting), plays the death animation,
/// and disables the whole GameObject once the animation has had time to play.
/// The Rigidbody2D is left alone, so gravity still pulls the body down and it
/// settles on the ground naturally, without needing separate ragdoll code.
/// </summary>
[RequireComponent(typeof(Health))]
public class CockroachDeath : MonoBehaviour
{
    [Tooltip("How long to wait after death before disabling this GameObject. Match this to your death animation clip's length.")]
    public float deathAnimationDuration = 1f;

    private Health health;
    private CockroachMovement movement;
    private CockroachAim aim;
    private CockroachShooting shooting;
    private CockroachAnimator animator;

    private void Awake()
    {
        health = GetComponent<Health>();
        movement = GetComponent<CockroachMovement>();
        aim = GetComponent<CockroachAim>();
        shooting = GetComponent<CockroachShooting>();
        animator = GetComponent<CockroachAnimator>();
    }

    private void OnEnable()
    {
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        // Stop reading input. Each of these is optional on purpose, in case
        // a future object using Health does not have all of them.
        if (movement != null) movement.enabled = false;
        if (aim != null) aim.enabled = false;
        if (shooting != null) shooting.enabled = false;

        if (animator != null)
        {
            animator.PlayDeath();
        }

        Invoke(nameof(FinishDeath), deathAnimationDuration);
    }

    private void FinishDeath()
    {
        gameObject.SetActive(false);
    }
}
