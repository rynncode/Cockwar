using System;
using UnityEngine;

/// <summary>
/// Step 6: Damage.
/// Put this on anything that can take damage — currently just the cockroach.
/// This script only tracks the number and clamps it at 0; it does not destroy
/// or disable anything itself. CockroachDeath (step 8) listens to OnDeath instead.
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("Starting and maximum health.")]
    public int maxHealth = 100;

    private int currentHealth;

    /// <summary>Current health, read-only from outside this script.</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>True once health has reached 0.</summary>
    public bool IsDead => currentHealth <= 0;

    /// <summary>Fires every time damage is actually taken, carrying the amount. CockroachSfx listens to this.</summary>
    public event Action<int> OnDamaged;

    /// <summary>Fires exactly once, the moment health reaches 0.</summary>
    public event Action OnDeath;

    private int baseMaxHealth;

    /// <summary>Max health at the start of the match, before any shield raised it.</summary>
    public int BaseMaxHealth => baseMaxHealth;

    /// <summary>How far shields have raised max health above the starting max.</summary>
    public int ShieldBonus => maxHealth - baseMaxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        baseMaxHealth = maxHealth;
    }

    /// <summary>
    /// Restores health, never above the current max. Returns how much was actually healed
    /// (0 if already full). Does nothing to a dead cockroach.
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || IsDead) return 0;

        int healed = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healed;
        return healed;
    }

    /// <summary>
    /// Shield: raises max health AND current health by the amount, but the total raise over
    /// the starting max never goes past maxBonus. Returns how much was actually added.
    /// </summary>
    public int AddShield(int amount, int maxBonus)
    {
        if (amount <= 0 || IsDead) return 0;

        int added = Mathf.Clamp(amount, 0, Mathf.Max(0, baseMaxHealth + maxBonus - maxHealth));
        maxHealth += added;
        currentHealth += added;
        return added;
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

        OnDamaged?.Invoke(amount);

        if (IsDead)
        {
            Debug.Log(gameObject.name + " has reached 0 health.");
            OnDeath?.Invoke();
        }
    }
}
