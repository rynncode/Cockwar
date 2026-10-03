using UnityEngine;

/// <summary>
/// Stamina bar: appears while the cockroach is using stamina (walking or jumping),
/// stays for a moment after the last use, then fades out so it is not a distraction.
/// Add this component to each cockroach next to Stamina.
/// </summary>
[DefaultExecutionOrder(11)]
[RequireComponent(typeof(Stamina))]
public class StaminaBar : OverheadBar
{
    [Header("Stamina Bar")]
    [Tooltip("Seconds the bar stays visible after stamina was last used.")]
    public float visibleSeconds = 1.5f;

    [Tooltip("Seconds the bar takes to fade in and out.")]
    public float fadeTime = 0.3f;

    [Header("Fill Colors")]
    public Color fullStaminaColor = new Color(1f, 0.85f, 0.1f);
    public Color lowStaminaColor = new Color(1f, 0.4f, 0.1f);

    private Stamina stamina;
    private float lastStamina;
    private float lastUseTime = -999f;

    private void Reset()
    {
        // A little thinner than the jump bar when first added.
        barHeight = 0.5f;
    }

    protected override int Slot => OverheadStack.SlotStamina;

    protected override float FadeTime => fadeTime;

    protected override void OnBarAwake()
    {
        stamina = GetComponent<Stamina>();
        lastStamina = stamina.CurrentStamina;
    }

    protected override bool ShouldShow()
    {
        // Stamina going DOWN means movement. (Going up is the refill at turn start: no bar for that.)
        if (stamina.CurrentStamina < lastStamina - 0.0001f)
            lastUseTime = Time.time;
        lastStamina = stamina.CurrentStamina;

        return Time.time - lastUseTime < visibleSeconds;
    }

    protected override float Value01() => stamina.Stamina01;

    protected override Color FillColor(float value01) => Color.Lerp(lowStaminaColor, fullStaminaColor, value01);
}
