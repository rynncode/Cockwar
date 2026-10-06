using UnityEngine;

/// <summary>How the player triggers a special weapon.</summary>
public enum SpecialActivation
{
    /// <summary>Hold and release the left mouse button, like every normal weapon (power charge + aim).</summary>
    ChargeShot,

    /// <summary>Left-click a spot on the battlefield. A target marker follows the mouse until then.</summary>
    ClickTarget,

    /// <summary>Press the right mouse button. No aiming.</summary>
    RightClick
}

/// <summary>Everything a special attack needs to know about the shot that started it.</summary>
public struct AttackContext
{
    public CockroachShooting shooter;   // who fired (can be hit by their own attack)
    public WeaponData weapon;
    public Vector2 origin;              // aim origin, in front of the cockroach
    public Vector2 direction;           // aim direction (normalised)
    public float power;                 // launch speed from the charge (ChargeShot only)
    public Vector2 target;              // clicked spot (ClickTarget), or the shooter's position
}

/// <summary>
/// Base for weapons that do more than fire one projectile (Reneitor, Satelaser, Airstrike, Demon Fire, Doom).
///
/// How it plugs in: a WeaponData asset has a Special Attack slot. When that is filled,
/// CockroachShooting calls Begin instead of spawning the Projectile Prefab, then uses ammo and raises
/// OnFired exactly like a normal shot, so the TurnManager ends the turn the usual way. Anything the
/// attack spawns that takes time (a meteor, a UFO, a countdown...) is an AttackRunner, and the
/// TurnManager keeps the turn open until every AttackRunner has finished.
///
/// Each weapon is a ScriptableObject asset (Assets/Weapons/Special/Attacks) holding its tuning
/// values, so balancing never needs a code change. Its own scripts live in Assets/Scripts/Weapons.
/// To add a new special weapon: make a class that extends this one, give it [CreateAssetMenu],
/// make an asset of it, and drag that asset into a WeaponData's Special Attack slot.
/// </summary>
public abstract class SpecialAttack : ScriptableObject
{
    [Header("Art (optional; empty = built-in pixel art)")]
    [Tooltip("Shown in the weapon panel when the WeaponData has no Icon of its own.")]
    public Sprite icon;

    [Tooltip("Drawn in the cockroach's hand when the WeaponData has no Held Sprite of its own. The art should point right.")]
    public Sprite heldSprite;

    private Sprite builtIcon;
    private Sprite builtHeld;

    /// <summary>How this weapon is triggered. Fixed per weapon type.</summary>
    public abstract SpecialActivation Activation { get; }

    /// <summary>
    /// Starts the attack. Return the Projectile the camera should follow and the turn should wait on
    /// to land, or null if there is none (the turn then waits on the AttackRunners instead).
    /// </summary>
    public abstract Projectile Begin(AttackContext context);

    /// <summary>Marker that follows the mouse for ClickTarget weapons. Null = none.</summary>
    public virtual Sprite TargetMarker => null;

    /// <summary>
    /// ClickTarget weapons: can this spot be chosen right now? An invalid spot shows the marker in red
    /// and clicking it does nothing (the Teleporter refuses spots inside the ground).
    /// </summary>
    public virtual bool IsValidTarget(Vector2 target, CockroachShooting shooter) => true;

    /// <summary>True for a weapon that is aimed even though it is not charged (the Blowtorch), so the crosshair stays.</summary>
    public virtual bool ShowsCrosshair => false;

    /// <summary>World width of the target marker.</summary>
    public virtual float TargetMarkerSize => 10f;

    /// <summary>Pixel-art icon for the weapon panel, used when no icon sprite is assigned.</summary>
    protected abstract Sprite BuildIcon();

    /// <summary>Pixel-art hand-held sprite, used when no held sprite is assigned.</summary>
    protected abstract Sprite BuildHeldSprite();

    public Sprite Icon
    {
        get
        {
            if (icon != null) return icon;
            if (builtIcon == null) builtIcon = BuildIcon();
            return builtIcon;
        }
    }

    public Sprite HeldSprite
    {
        get
        {
            if (heldSprite != null) return heldSprite;
            if (builtHeld == null) builtHeld = BuildHeldSprite();
            return builtHeld;
        }
    }

    // --- Small helpers for the other scripts, so they don't each repeat the null checks ---

    public static SpecialAttack Of(WeaponData weapon) => weapon != null && weapon.specialAttack != null ? weapon.specialAttack : null;

    /// <summary>True for weapons that are not aimed along a line (ClickTarget and RightClick).</summary>
    public static bool HidesCrosshair(WeaponData weapon)
    {
        SpecialAttack special = Of(weapon);
        return special != null && special.Activation != SpecialActivation.ChargeShot && !special.ShowsCrosshair;
    }

    /// <summary>True if the right mouse button triggers this weapon (so it must not open the weapon panel).</summary>
    public static bool UsesRightClick(WeaponData weapon)
    {
        SpecialAttack special = Of(weapon);
        return special != null && special.Activation == SpecialActivation.RightClick;
    }
}
