using UnityEngine;

/// <summary>
/// All of one cockroach's sound effects: footsteps, jump, landing, hurt, death,
/// and the shooting sounds (charging, firing, cancelling, switching weapon).
/// Add it to the cockroach next to CockroachMovement and drag clips into the slots.
/// Every slot is optional — an empty slot just stays silent.
/// Arrays pick a random clip each time, so several footstep clips sound less repetitive.
/// It only listens to the other scripts (events and read-only state), so removing it
/// silences the cockroach without changing how it plays.
/// </summary>
[RequireComponent(typeof(CockroachMovement))]
public class CockroachSfx : MonoBehaviour
{
    [Header("Walking")]
    public AudioClip[] footstepSounds;
    [Range(0f, 1f)] public float footstepVolume = 0.4f;

    [Tooltip("Seconds between footsteps while walking.")]
    public float footstepInterval = 0.18f;

    [Header("Jumping")]
    public AudioClip[] jumpSounds;
    [Range(0f, 1f)] public float jumpVolume = 0.7f;

    public AudioClip[] landSounds;
    [Range(0f, 1f)] public float landVolume = 0.6f;

    [Tooltip("Must be in the air at least this long for a landing sound, so tiny bumps on uneven ground stay quiet.")]
    public float minAirTimeForLand = 0.15f;

    [Header("Damage")]
    public AudioClip[] hurtSounds;
    [Range(0f, 1f)] public float hurtVolume = 0.8f;

    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float deathVolume = 1f;

    [Header("Shooting")]
    [Tooltip("Played when firing a weapon that has no Fire Sound of its own (and in single-weapon mode).")]
    public AudioClip defaultFireSound;
    [Range(0f, 1f)] public float fireVolume = 0.8f;

    [Tooltip("Optional loop played while holding the mouse to charge a shot. Its pitch rises with the charge.")]
    public AudioClip chargeLoop;
    [Range(0f, 1f)] public float chargeVolume = 0.4f;

    [Tooltip("Pitch of the charge loop at empty and at full charge.")]
    public float chargePitchMin = 0.8f;
    public float chargePitchMax = 1.6f;

    public AudioClip chargeCancelSound;
    public AudioClip weaponSwitchSound;
    [Range(0f, 1f)] public float uiVolume = 0.6f;

    private CockroachMovement movement;
    private CockroachShooting shooting;
    private Health health;
    private Rigidbody2D body;
    private AudioSource chargeSource;

    private float footstepTimer;
    private bool wasGrounded = true;
    private float airTime;
    private int lastWeaponIndex;
    private int lastFireFrame = -1;

    private void Awake()
    {
        movement = GetComponent<CockroachMovement>();
        shooting = GetComponent<CockroachShooting>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();

        if (chargeLoop != null)
        {
            chargeSource = gameObject.AddComponent<AudioSource>();
            chargeSource.clip = chargeLoop;
            chargeSource.loop = true;
            chargeSource.playOnAwake = false;
            chargeSource.spatialBlend = 0f;
            chargeSource.volume = chargeVolume;
        }
    }

    private void Start()
    {
        // Sfx.Output is set by AudioManager.Awake, which has run by now.
        if (chargeSource != null) chargeSource.outputAudioMixerGroup = Sfx.Output;
        if (shooting != null) lastWeaponIndex = shooting.CurrentWeaponIndex;
    }

    private void OnEnable()
    {
        movement.OnJumped += HandleJumped;

        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDeath += HandleDeath;
        }

        if (shooting != null)
        {
            shooting.OnWeaponFired += HandleWeaponFired;
            shooting.OnChargeCancelled += HandleChargeCancelled;
            shooting.OnWeaponChanged += HandleWeaponChanged;
        }
    }

    private void OnDisable()
    {
        movement.OnJumped -= HandleJumped;

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDeath -= HandleDeath;
        }

        if (shooting != null)
        {
            shooting.OnWeaponFired -= HandleWeaponFired;
            shooting.OnChargeCancelled -= HandleChargeCancelled;
            shooting.OnWeaponChanged -= HandleWeaponChanged;
        }

        if (chargeSource != null) chargeSource.Stop();
    }

    private void Update()
    {
        UpdateFootsteps();
        UpdateLanding();
        UpdateChargeLoop();
    }

    private void UpdateFootsteps()
    {
        bool walking = movement.isMyTurn
                       && movement.IsGrounded
                       && !movement.IsKnockedBack
                       && movement.WalkAmount > 0f
                       && body != null && Mathf.Abs(body.linearVelocity.x) > 0.5f; // pressing into a wall / out of stamina = silent

        if (!walking)
        {
            footstepTimer = 0f; // next step plays the instant walking starts
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            Sfx.PlayRandom(footstepSounds, footstepVolume, 0.12f);
            footstepTimer = footstepInterval;
        }
    }

    private void UpdateLanding()
    {
        bool grounded = movement.IsGrounded;

        if (!grounded)
        {
            airTime += Time.deltaTime;
        }
        else if (!wasGrounded && airTime >= minAirTimeForLand)
        {
            Sfx.PlayRandom(landSounds, landVolume, 0.1f);
        }

        if (grounded) airTime = 0f;
        wasGrounded = grounded;
    }

    private void UpdateChargeLoop()
    {
        if (chargeSource == null || shooting == null) return;

        if (shooting.IsCharging)
        {
            if (!chargeSource.isPlaying) chargeSource.Play();
            chargeSource.volume = chargeVolume * GameSettings.SfxVolume;
            chargeSource.pitch = Mathf.Lerp(chargePitchMin, chargePitchMax, shooting.ChargeRatio01);
        }
        else if (chargeSource.isPlaying)
        {
            chargeSource.Stop();
        }
    }

    private void HandleJumped() => Sfx.PlayRandom(jumpSounds, jumpVolume, 0.08f);

    private void HandleDamaged(int amount)
    {
        // A fatal hit plays the death sound instead (OnDeath fires right after this).
        if (health.IsDead) return;
        Sfx.PlayRandom(hurtSounds, hurtVolume, 0.1f);
    }

    private void HandleDeath() => Sfx.PlayRandom(deathSounds, deathVolume);

    private void HandleWeaponFired(WeaponData weapon)
    {
        lastFireFrame = Time.frameCount;
        AudioClip clip = weapon != null && weapon.fireSound != null ? weapon.fireSound : defaultFireSound;
        Sfx.Play(clip, fireVolume, 0.05f);
    }

    private void HandleChargeCancelled() => Sfx.Play(chargeCancelSound, uiVolume);

    private void HandleWeaponChanged()
    {
        // OnWeaponChanged also fires when ammo is used; only a real switch makes a sound.
        if (shooting.CurrentWeaponIndex == lastWeaponIndex) return;
        lastWeaponIndex = shooting.CurrentWeaponIndex;

        // Running out of ammo auto-switches in the same frame as the shot; the fire sound covers that.
        if (Time.frameCount == lastFireFrame) return;
        if (movement.isMyTurn) Sfx.Play(weaponSwitchSound, uiVolume);
    }
}
