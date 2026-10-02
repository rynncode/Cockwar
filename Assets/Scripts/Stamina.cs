using System;
using UnityEngine;

/// <summary>
/// Step 9: Turn system (stamina).
/// Optional: only cockroaches that have this component are limited by stamina.
/// Drains while walking during your own turn. TurnManager refills it at the
/// start of each turn and listens for OnStaminaDepleted to end the turn early.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
public class Stamina : MonoBehaviour
{
    [Tooltip("Maximum stamina. Refilled to this amount at the start of each turn.")]
    public float maxStamina = 10f;

    [Tooltip("Stamina drained per second while walking.")]
    public float drainPerSecond = 2f;

    private CockroachMovement movement;
    private float currentStamina;
    private float nextStaminaLogTime;

    // So OnStaminaDepleted fires exactly once per turn, not every frame at 0.
    private bool hasFiredDepletedEvent;

    /// <summary>Current stamina, read-only from outside this script.</summary>
    public float CurrentStamina => currentStamina;

    /// <summary>0 to 1. For a stamina bar in step 15.</summary>
    public float Stamina01 => maxStamina > 0f ? currentStamina / maxStamina : 0f;

    /// <summary>Raised once, the moment stamina reaches 0. TurnManager listens to this.</summary>
    public event Action OnStaminaDepleted;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        currentStamina = maxStamina;
    }

    private void Update()
    {
        if (!movement.isMyTurn) return;
        if (currentStamina <= 0f) return;

        if (movement.WalkAmount > 0f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - drainPerSecond * Time.deltaTime);

            if (Time.time >= nextStaminaLogTime)
            {
                Debug.Log(gameObject.name + " stamina: " + currentStamina);
                nextStaminaLogTime = Time.time + 1f;
            }

            if (currentStamina <= 0f && !hasFiredDepletedEvent)
            {
                hasFiredDepletedEvent = true;
                OnStaminaDepleted?.Invoke();
            }
        }
    }

    /// <summary>
    /// Refills stamina to max. Called by TurnManager at the start of this player's turn.
    /// </summary>
    public void ResetStamina()
    {
        currentStamina = maxStamina;
        hasFiredDepletedEvent = false;
    }
}
