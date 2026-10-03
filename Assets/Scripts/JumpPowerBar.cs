using UnityEngine;

/// <summary>
/// Jump power bar: a small fill bar above the cockroach's head that only
/// appears while the jump key is held, and fills up as the jump charges.
/// Sits in the stack above the health and stamina bars so they never overlap.
/// Add this component to each cockroach next to CockroachMovement.
/// </summary>
[DefaultExecutionOrder(12)]
[RequireComponent(typeof(CockroachMovement))]
public class JumpPowerBar : OverheadBar
{
    [Header("Fill Colors")]
    [Tooltip("Fill color at the smallest jump.")]
    public Color lowPowerColor = Color.green;

    [Tooltip("Fill color at the biggest jump.")]
    public Color highPowerColor = Color.red;

    private CockroachMovement movement;

    protected override int Slot => OverheadStack.SlotJump;

    protected override void OnBarAwake()
    {
        movement = GetComponent<CockroachMovement>();
    }

    // Only while the jump key is held (instant on / off, so it feels tied to the key).
    protected override bool ShouldShow() => movement.IsChargingJump;

    protected override float Value01() => movement.JumpCharge01;

    protected override Color FillColor(float value01) => Color.Lerp(lowPowerColor, highPowerColor, value01);
}
