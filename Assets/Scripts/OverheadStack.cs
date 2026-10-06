using UnityEngine;

/// <summary>
/// Keeps everything that floats above a cockroach's head (health bar, stamina bar,
/// jump power bar, P1/P2 label) stacked neatly instead of overlapping.
/// Each item reports whether it is visible and how tall it is; the stack works out
/// where each one should sit: health nearest the head, then stamina, then jump power,
/// then the label on top. Hidden items take no space.
/// This component adds itself automatically, you never need to add it by hand.
/// </summary>
public class OverheadStack : MonoBehaviour
{
    public const int SlotHealth = 0;
    public const int SlotStamina = 1;
    public const int SlotJump = 2;
    public const int SlotLabel = 3;
    private const int SlotCount = 4;

    [Tooltip("Gap between the top of the cockroach's head and the first item.")]
    public float gapAboveHead = 1f;

    [Tooltip("Gap between stacked items.")]
    public float spacing = 0.3f;

    private readonly bool[] visible = new bool[SlotCount];
    private readonly float[] heights = new float[SlotCount];

    private Collider2D bodyCollider;

    /// <summary>Gets the stack on this object, adding one if there isn't one yet.</summary>
    public static OverheadStack For(GameObject target)
    {
        OverheadStack stack = target.GetComponent<OverheadStack>();
        if (stack == null)
            stack = target.AddComponent<OverheadStack>();

        return stack;
    }

    private void Awake()
    {
        bodyCollider = GetComponent<Collider2D>();
    }

    /// <summary>World X the items are centred on (the middle of the head).</summary>
    public float CenterX => bodyCollider != null ? bodyCollider.bounds.center.x : transform.position.x;

    /// <summary>World Y of the top of the cockroach's collider.</summary>
    public float TopY => bodyCollider != null ? bodyCollider.bounds.max.y : transform.position.y;

    /// <summary>World Y just above the topmost visible item (or above the head if nothing is showing).</summary>
    public float TopOfStackY => GetBottomY(SlotCount);

    /// <summary>An item tells the stack if it is showing and how tall it is (world units).</summary>
    public void Report(int slot, bool isVisible, float height)
    {
        visible[slot] = isVisible;
        heights[slot] = height;
    }

    /// <summary>
    /// World Y where the BOTTOM edge of the given slot's item should sit:
    /// above the head, plus the height of every visible item in the slots below it.
    /// </summary>
    public float GetBottomY(int slot)
    {
        float y = TopY + gapAboveHead;

        for (int i = 0; i < slot; i++)
        {
            if (visible[i])
                y += heights[i] + spacing;
        }

        return y;
    }
}
