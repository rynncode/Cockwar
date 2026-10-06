using UnityEngine;

/// <summary>
/// Base for anything a special attack leaves running in the world: a falling meteor, the UFO,
/// the airstrike plane, the demon fire, the nuke countdown.
///
/// While any AttackRunner exists, TurnManager keeps the turn open (it waits after the shot instead
/// of passing to the next player) and nobody can start another special attack. A runner just has to
/// Destroy itself when it is done. Runners pause with the game because they use Time.deltaTime.
/// </summary>
public abstract class AttackRunner : MonoBehaviour
{
    private static int activeCount;

    /// <summary>True while at least one special attack effect is still playing.</summary>
    public static bool AnyActive => activeCount > 0;

    protected virtual void OnEnable() => activeCount++;

    protected virtual void OnDisable() => activeCount = Mathf.Max(0, activeCount - 1);

    // A scene reload with a runner mid-flight must not leave the count stuck above zero.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => activeCount = 0;
}
