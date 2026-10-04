using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Health bar: only appears once the cockroach has taken damage. It shows the health
/// as a number on the bar, and when a hit lands it plays a short sequence:
///   1. a brief pause (hitPauseTime), which only starts once any knockback animation
///      (tumble, landing, dizzy stars) has finished and the cockroach is back to idle,
///   2. a red "-10" label floats up above the head,
///   3. the number counts down one by one (100, 99, 98 ... 90) with the bar shrinking with it.
/// TurnManager holds the turn while this is playing, and the skip key (Enter) jumps
/// straight to the final number by calling SkipAnimation.
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

    [Header("Health Number")]
    [Tooltip("Show the health as a number on the bar.")]
    public bool showNumber = true;

    [Tooltip("World size of ONE pixel of the number's font. A digit is 5 pixels tall (7 with its outline). The bar is made at least tall enough to hold it.")]
    public float numberPixelSize = 0.2f;

    [Header("Damage Countdown")]
    [Tooltip("Seconds the bar waits after a hit before the number starts counting down.")]
    public float hitPauseTime = 0.4f;

    [Tooltip("How many numbers the count goes down per second. 30 means 10 damage takes about a third of a second.")]
    public float countPerSecond = 30f;

    [Tooltip("A big hit counts faster so the whole countdown never takes longer than this many seconds.")]
    public float maxCountSeconds = 1.5f;

    [Header("Damage Label (-10)")]
    [Tooltip("World size of ONE pixel of the floating damage label's font.")]
    public float popupPixelSize = 0.3f;

    [Tooltip("How far (world units) the label floats upward.")]
    public float popupRise = 4f;

    [Tooltip("Seconds the label is on screen before it has faded away.")]
    public float popupSeconds = 1.2f;

    public Color popupColor = new Color(1f, 0.25f, 0.2f);

    [Header("Fill Colors")]
    public Color fullHealthColor = new Color(0.2f, 0.85f, 0.3f);
    public Color lowHealthColor = new Color(0.9f, 0.15f, 0.15f);

    private enum Phase { Idle, Pause, Count }

    private class Popup
    {
        public PixelNumber number;
        public float age;
    }

    private Health health;
    private KnockbackAnimation knockbackAnimation;

    private Phase phase = Phase.Idle;
    private int knownHealth;       // the real health we saw last frame, to spot hits
    private int displayedHealth;   // what the bar and number show (lags behind during a countdown)
    private int pendingDamage;     // damage taken but not yet shown as a label
    private float pauseTimer;
    private float tickTimer;
    private float tickInterval;
    private float lastHitTime = -999f;

    private PixelNumber healthNumber;
    private int shownNumber = -1;
    private readonly List<Popup> popups = new List<Popup>();

    /// <summary>
    /// True while a hit is still being shown: the pause, the label, or the counting down.
    /// Based on the real health too, so it is already true in the very frame damage lands.
    /// </summary>
    public bool IsAnimating =>
        health != null && (phase != Phase.Idle || displayedHealth > health.CurrentHealth);

    private void Reset()
    {
        // Bigger than the other bars so the number fits on it.
        barWidth = 7f;
        barHeight = 1.2f;
    }

    protected override int Slot => OverheadStack.SlotHealth;

    protected override float FadeTime => fadeTime;

    protected override void OnBeforeBuild()
    {
        health = GetComponent<Health>();

        // Make sure the bar is big enough to hold a 3-digit number.
        if (showNumber)
        {
            float minHeight = PixelNumber.HeightInPixels * numberPixelSize - borderThickness * 2f;
            float minWidth = PixelNumber.WidthInPixels(3) * numberPixelSize + borderThickness * 2f;

            if (barHeight < minHeight) barHeight = minHeight;
            if (barWidth < minWidth) barWidth = minWidth;
        }
    }

    protected override void OnBarAwake()
    {
        knownHealth = health.CurrentHealth;
        displayedHealth = health.CurrentHealth;

        // Optional: if the cockroach has a knockback animation, the countdown waits for it.
        knockbackAnimation = GetComponent<KnockbackAnimation>();

        // The number sits in the middle of the bar, above the fill.
        healthNumber = new PixelNumber(BarRoot, "HealthNumber", BarSortingLayerId, BarBaseOrder + 2, numberPixelSize);
        healthNumber.SetColor(Color.white);
    }

    // ---------------- Hit tracking and the countdown ----------------

    private void Update()
    {
        int real = health.CurrentHealth;

        if (real < knownHealth)
        {
            OnHit(knownHealth - real);
        }
        else if (real > knownHealth)
        {
            // Healed or reset: just show it, no countdown.
            displayedHealth = real;
            pendingDamage = 0;
            phase = Phase.Idle;
        }

        knownHealth = real;

        if (phase == Phase.Pause)
        {
            if (knockbackAnimation != null && knockbackAnimation.IsPlaying)
            {
                // Still tumbling or dizzy: keep the pause full. It only starts counting
                // down once the cockroach is back to idle.
                pauseTimer = hitPauseTime;
            }
            else
            {
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                    BeginCount();
            }
        }
        else if (phase == Phase.Count)
        {
            tickTimer += Time.deltaTime;

            // One number at a time, so the count never skips a value.
            while (tickTimer >= tickInterval && displayedHealth > real)
            {
                displayedHealth--;
                tickTimer -= tickInterval;
            }

            if (displayedHealth <= real)
            {
                displayedHealth = real;
                phase = Phase.Idle;
            }
        }
    }

    private void OnHit(int damage)
    {
        lastHitTime = Time.time;

        if (phase == Phase.Idle)
        {
            // First hit: a brief stop, then the label and the countdown.
            phase = Phase.Pause;
            pauseTimer = hitPauseTime;
            pendingDamage = damage;
        }
        else if (phase == Phase.Pause)
        {
            // Another hit during the pause: it all counts as one batch.
            pendingDamage += damage;
        }
        else
        {
            // Another hit while counting: show its label now and count faster to catch up.
            SpawnPopup(damage);
            SetTickInterval();
        }
    }

    private void BeginCount()
    {
        phase = Phase.Count;
        tickTimer = 0f;

        SpawnPopup(pendingDamage);
        pendingDamage = 0;

        SetTickInterval();
    }

    // The count goes at countPerSecond, but speeds up for big hits so it never drags on.
    private void SetTickInterval()
    {
        float numbersToCount = Mathf.Max(1, displayedHealth - health.CurrentHealth);
        float perSecond = Mathf.Max(countPerSecond, numbersToCount / Mathf.Max(0.1f, maxCountSeconds));
        tickInterval = 1f / Mathf.Max(1f, perSecond);
    }

    /// <summary>
    /// Jumps straight to the real health: no more pause, no more counting.
    /// Any label that has not appeared yet appears right away so the damage is still shown.
    /// Called by TurnManager when the skip key is pressed.
    /// </summary>
    public void SkipAnimation()
    {
        if (health == null)
            return;

        if (pendingDamage > 0)
        {
            SpawnPopup(pendingDamage);
            pendingDamage = 0;
        }

        displayedHealth = health.CurrentHealth;
        phase = Phase.Idle;

        // Skipping also ends the knockback animation straight away.
        if (knockbackAnimation != null)
            knockbackAnimation.Skip();
    }

    // ---------------- Bar visuals ----------------

    protected override bool ShouldShow()
    {
        bool animating = IsAnimating;

        // A dead cockroach keeps its bar only until the number has counted down to 0.
        if (health.IsDead)
            return animating;

        if (animating)
            return true;

        bool damaged = health.CurrentHealth < health.maxHealth;
        if (!damaged)
            return false;

        return hideAfterSeconds <= 0f || Time.time - lastHitTime < hideAfterSeconds;
    }

    protected override float Value01() =>
        health.maxHealth > 0 ? (float)displayedHealth / health.maxHealth : 0f;

    protected override Color FillColor(float value01) => Color.Lerp(lowHealthColor, fullHealthColor, value01);

    protected override void AfterBarUpdate(bool visible, float alpha)
    {
        // The number on the bar.
        healthNumber.SetActive(showNumber);

        if (showNumber && visible)
        {
            // Only rebuild the text when the number actually changed.
            if (displayedHealth != shownNumber)
            {
                shownNumber = displayedHealth;
                healthNumber.SetText(Mathf.Max(0, displayedHealth).ToString());
            }

            healthNumber.SetAlpha(alpha);
        }

        UpdatePopups();
    }

    // ---------------- Floating "-10" labels ----------------

    private void SpawnPopup(int damage)
    {
        if (damage <= 0)
            return;

        PixelNumber number = new PixelNumber(null, "DamageLabel_" + gameObject.name,
            BarSortingLayerId, BarBaseOrder + 5, popupPixelSize);
        number.SetColor(popupColor);
        number.SetText("-" + damage);

        popups.Add(new Popup { number = number, age = 0f });
    }

    // Moves each label up and fades it out. Labels follow the cockroach, so a hit
    // that sends it flying doesn't leave the label behind.
    private void UpdatePopups()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup popup = popups[i];
            popup.age += Time.deltaTime;

            float t = popup.age / Mathf.Max(0.1f, popupSeconds);

            if (t >= 1f)
            {
                popup.number.Destroy();
                popups.RemoveAt(i);
                continue;
            }

            // Start above whatever is stacked over the head, then float up.
            float halfLabelHeight = PixelNumber.HeightInPixels * 0.5f * popupPixelSize;
            float y = Stack.GetBottomY(OverheadStack.SlotLabel) + halfLabelHeight + popupRise * t;
            popup.number.Transform.position = new Vector3(Stack.CenterX, y, 0f);

            // Fully visible for the first 60%, then fades out.
            popup.number.SetAlpha(t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
        }
    }

    protected override void OnBarDestroyed()
    {
        foreach (Popup popup in popups)
            popup.number.Destroy();

        popups.Clear();

        if (healthNumber != null)
            healthNumber.Destroy();
    }
}
