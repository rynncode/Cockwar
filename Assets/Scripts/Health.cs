using UnityEngine;

/// <summary>
/// Step 6: Damage.
/// Put this on anything that can take damage — currently just the cockroach.
/// Death (step 8) will react to health reaching 0. This script only tracks
/// the number and clamps it at 0; it does not destroy or disable anything itself.
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("Starting and maximum health.")]
    public int maxHealth = 100;

    private int currentHealth;

    /// <summary>Current health, read-only from outside this script.</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>True once health has reached 0. Death (step 8) will check this.</summary>
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Reduces health by the given amount, clamped so it never goes below 0.
    /// Negative or zero amounts are ignored.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (IsDead) return; // Already at 0, nothing left to reduce.

        currentHealth = Mathf.Max(0, currentHealth - amount);

        Debug.Log(gameObject.name + " took " + amount + " damage. Health now: " + currentHealth + "/" + maxHealth);

        if (IsDead)
        {
            // Step 8 will hook actual death behaviour here (animation, removing from turn order, etc).
            Debug.Log(gameObject.name + " has reached 0 health.");
        }
    }
}
