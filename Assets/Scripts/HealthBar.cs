using UnityEngine;

/// <summary>
/// Health bar: only appears once the cockroach has taken damage, and goes away
/// again if it is at full health (or dead). Drains from green to red.
/// Add this component to each cockroach next to Health.
/// </summary>
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(Health))]
public class HealthBar : OverheadBar
{
    [Header("Health Bar")]
    [Tooltip("0 = the bar stays visible for as long as the cockroach is damaged. Above 0 = it hides this many seconds after the last hit.")]
    public float hideAfterSeconds = 0f;

    [Tooltip("Seconds the bar takes to fade in and out.")]
    public float fadeTime = 0.3f;

    [Header("Fill Colors")]
    public Color fullHealthColor = new Color(0.2f, 0.85f, 0.3f);
    public Color lowHealthColor = new Color(0.9f, 0.15f, 0.15f);

    private Health health;
    private int lastHealth;
    private float lastHitTime = -999f;

    private void Reset()
    {
        // A little thinner than the jump bar when first added.
        barHeight = 0.5f;
    }

    protected override int Slot => OverheadStack.SlotHealth;

    protected override float FadeTime => fadeTime;

    protected override void OnBarAwake()
    {
        health = GetComponent<Health>();
        lastHealth = health.CurrentHealth;
    }

    protected override bool ShouldShow()
    {
        // Remember when the last hit landed.
        if (health.CurrentHealth < lastHealth)
            lastHitTime = Time.time;
        lastHealth = health.CurrentHealth;

        if (health.IsDead)
            return false;

        bool damaged = health.CurrentHealth < health.maxHealth;
        if (!damaged)
            return false;

        return hideAfterSeconds <= 0f || Time.time - lastHitTime < hideAfterSeconds;
    }

    protected override float Value01() =>
        health.maxHealth > 0 ? (float)health.CurrentHealth / health.maxHealth : 0f;

    protected override Color FillColor(float value01) => Color.Lerp(lowHealthColor, fullHealthColor, value01);
}
