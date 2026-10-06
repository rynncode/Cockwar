using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 9: Turn system.
/// Decides whose turn it is. Only the active cockroach has isMyTurn = true,
/// so only that one can walk, jump, aim and shoot (CockroachMovement and
/// CockroachShooting already check isMyTurn).
///
/// A turn ends when any of the following happens:
///  - the player manually ends it (the End Turn key from the settings menu; a real button
///    arrives in step 15), or
///  - the active player's Stamina (optional component) runs out, or
///  - the active player fires a weapon that has endsTurnOnFire = true, THEN
///    the fired projectile actually lands (Projectile.OnLanded), THEN a
///    short endTurnDelay settle period passes so the explosion and knockback
///    can finish — this no longer guesses a fixed wait time, so a shot that
///    flies a long way no longer passes the turn before it lands, or
///  - the turn time limit runs out, or
///  - the active player dies.
///
/// While a shot is in flight, the camera follows the projectile. Press
/// toggleCameraViewKey to switch back and forth between it and the active
/// player while waiting for it to land.
///
/// Health countdown: when a hit lands, the victim's HealthBar plays a short
/// "-10" label and counts the number down one by one. The turn is held until
/// that has finished, and skipHealthCountKey (the main Enter key) skips it.
///
/// Deaths: a dead cockroach plays a short death sequence (see CockroachDeath). The turn
/// does not move on until that sequence has finished, and when only one player is left
/// the GameOverSequence takes over (camera to the winner, "WINNER", then the panel).
///
/// Dead players are skipped. When only one player is left alive, turns stop
/// and a message is logged. Step 14 (Game rules) will replace that message
/// with a real win screen.
///
/// Step 10: also tells the CameraController who to follow at the start of
/// each turn, instead of a pass-the-device screen.
///
/// Game intro: before the first turn, the camera zooms in and pans to each
/// player in turn order (showing their P1 / P2 label), then zooms back out and
/// the first player's turn begins.
/// </summary>
public class TurnManager : MonoBehaviour
{
    [Header("Players")]
    [Tooltip("Every cockroach in the match, in turn order. Drag each Cockroach from the Hierarchy into this list.")]
    public List<CockroachMovement> players = new List<CockroachMovement>();

    [Header("Turn Timing")]
    [Tooltip("Seconds a player has to fire before the turn ends by itself. Set to 0 for no time limit.")]
    public float turnTimeLimit = 30f;

    [Tooltip("Seconds to wait after a shot before passing to the next player. Gives the explosion and any knockback time to finish. Increase it if players are still flying when the next turn starts.")]
    public float endTurnDelay = 4f;

    [Tooltip("Seconds to wait after a special weapon's effect (meteor, laser, airstrike, demon fire, nuke) has finished before passing to the next player.")]
    public float settleAfterSpecialAttack = 1.5f;

    // Manual End Turn: the End Turn key is set in the settings menu (GameSettings).

    [Header("Game Over")]
    [Tooltip("Plays the winner camera, the WINNER text and the Retry / Home panel when the match ends. Leave empty to find it in the scene automatically.")]
    public GameOverSequence gameOverSequence;

    [Header("Health Countdown")]
    [Tooltip("Press this key to skip the health counting down after a hit and show the final number straight away. KeyCode.Return is the main Enter key; the numpad Enter is a different key (KeypadEnter), so it does nothing here.")]
    public KeyCode skipHealthCountKey = KeyCode.Return;

    [Header("Projectile Camera")]
    [Tooltip("While a shot is in flight, press this key to toggle the camera between the projectile and the active player.")]
    public KeyCode toggleCameraViewKey = KeyCode.Space;

    [Header("Step 10: Camera")]
    [Tooltip("Drag the Main Camera here (the one with CameraController on it). When set, the camera pans to whoever's turn it is.")]
    public CameraController cameraController;

    [Header("Opponent Indicator")]
    [Tooltip("Points at the opponent when they're off-screen. 1v1 only for now — leave empty if you haven't added one yet.")]
    public OffscreenIndicator opponentIndicator;

    [Header("Game Intro")]
    [Tooltip("When on, the camera visits each player before the first turn. Needs the Camera Controller above.")]
    public bool playIntro = true;

    [Tooltip("Seconds to wait before the intro starts (lets a scene fade-in finish first).")]
    public float introStartDelay = 0.5f;

    [Tooltip("Seconds the camera spends on each player (travel time plus time to look at them).")]
    public float introFocusTime = 2.2f;

    [Tooltip("Zoom during the intro (Orthographic Size). Smaller = closer. Kept inside the camera's min / max zoom.")]
    public float introZoom = 30f;

    [Tooltip("How smooth the intro pan and zoom are. Bigger = slower, softer movement.")]
    public float introSmoothTime = 0.8f;

    [Tooltip("Press this key to skip the intro and start playing straight away.")]
    public KeyCode skipIntroKey = KeyCode.Tab;

    [Header("HUD")]
    [Tooltip("Adds the turn timer (top middle of the screen) when the match starts, unless the scene already has a TurnTimerHud.")]
    public bool autoAddTurnTimer = true;

    [Tooltip("Adds the P1 / P2 bubbles over the players' heads when the match starts, unless the scene already has a PlayerBubbles.")]
    public bool autoAddPlayerBubbles = true;

    [Header("Supply Crates")]
    [Tooltip("Adds the supply crate system (starting crates, plane drops between turns) when the match starts, unless the scene already has a CrateDropManager. Its settings live in Assets/Resources/CrateSettings.")]
    public bool autoAddCrateDrops = true;

    [Tooltip("Runs the between-turns crate drop. Leave empty to find it in the scene automatically.")]
    public CrateDropManager crateDrops;

    // Seconds into each player's focus before their label appears (camera has mostly arrived).
    private const float IntroLabelDelay = 0.5f;

    // Seconds spent zooming back out before the first turn starts.
    private const float IntroZoomOutTime = 1.2f;

    private bool introSkipped;

    // Index into the players list of whoever is playing now. -1 = nobody yet.
    private int currentIndex = -1;

    // True once the active player has fired this turn.
    private bool shotFired;

    // True from the moment of firing until Projectile.OnLanded confirms impact.
    // Replaces an old fixed-delay guess that could end the turn before a
    // long-distance shot had actually landed.
    private bool waitingForImpact;

    // The projectile currently in flight, if any. Used for the camera toggle.
    private Projectile activeProjectile;

    // True while the camera is on the projectile instead of the active player.
    private bool followingProjectile;

    private float turnTimeLeft;
    private float endDelayLeft;

    // 1 during everyone's first turn, 2 once play comes back round to the first player, and so on.
    private int roundNumber;
    private bool gameOver;

    // A turn ended while a death sequence was still playing; moving on waits until it is done.
    private bool advancePending;

    // A supply plane is dropping a crate between two turns; the next turn waits for it.
    private bool betweenTurns;

    // The crate roll for the turn that just ended has been made, so it never happens twice.
    private bool crateRollDone;

    // --- Read-only state for other scripts (turn UI in step 15) ---

    /// <summary>True while a turn-ending shot is in flight. The weapon panel uses this to lock itself.</summary>
    public bool IsShotInFlight => waitingForImpact;

    /// <summary>The cockroach whose turn it is, or null if there is none.</summary>
    public CockroachMovement CurrentPlayer =>
        (currentIndex >= 0 && currentIndex < players.Count) ? players[currentIndex] : null;

    /// <summary>Seconds left in the current turn (only counts down before a shot).</summary>
    public float TurnTimeLeft => turnTimeLeft;

    /// <summary>The current round: 1 while everyone has their first turn, then 2, and so on.</summary>
    public int RoundNumber => roundNumber;

    /// <summary>True while the turn clock is ticking: a turn is on and the player has not fired yet.</summary>
    public bool IsTurnClockRunning =>
        !gameOver && !advancePending && !betweenTurns && currentIndex >= 0 && !shotFired && turnTimeLimit > 0f;

    /// <summary>True while a supply plane is dropping a crate between two turns.</summary>
    public bool IsBetweenTurns => betweenTurns;

    /// <summary>Fires when a player's turn begins, with that player.</summary>
    public event System.Action<CockroachMovement> OnTurnStarted;

    private void Awake()
    {
        // The HUD pieces build themselves; add them to this object (or anywhere) yourself to tune them.
        if (autoAddTurnTimer && FindFirstObjectByType<TurnTimerHud>() == null)
            gameObject.AddComponent<TurnTimerHud>();

        if (autoAddPlayerBubbles && FindFirstObjectByType<PlayerBubbles>() == null)
            gameObject.AddComponent<PlayerBubbles>();

        if (crateDrops == null) crateDrops = FindFirstObjectByType<CrateDropManager>();
        if (crateDrops == null && autoAddCrateDrops) crateDrops = gameObject.AddComponent<CrateDropManager>();
    }

    private void Start()
    {
        if (players.Count == 0)
        {
            Debug.LogWarning("TurnManager: the Players list is empty. Drag your cockroaches into it.");
            return;
        }

        foreach (CockroachMovement player in players)
        {
            if (player == null) continue;

            // Everyone starts with no control; the first StartTurn gives it to one player.
            player.isMyTurn = false;

            CockroachShooting shooting = player.GetComponent<CockroachShooting>();
            if (shooting != null)
            {
                shooting.OnFired += HandleShotFired;
                shooting.OnProjectileLaunched += HandleProjectileLaunched;
            }

            // Stamina is optional — a cockroach without it just has unlimited movement.
            Stamina stamina = player.GetComponent<Stamina>();
            if (stamina != null)
            {
                stamina.OnStaminaDepleted += HandleStaminaDepleted;
            }
        }

        if (playIntro && cameraController != null)
        {
            StartCoroutine(PlayIntro());
        }
        else
        {
            AdvanceToNextPlayer();
        }
    }

    /// <summary>
    /// Camera visits every living player in turn order, then the first turn starts.
    /// Nobody can act during this: isMyTurn is false for everyone, and the turn
    /// logic in Update does nothing until a turn has started.
    /// </summary>
    private IEnumerator PlayIntro()
    {
        float originalZoom = cameraController.TargetZoom;
        introSkipped = false;

        cameraController.BeginIntro(introSmoothTime);

        yield return StartCoroutine(WaitUnlessSkipped(introStartDelay));

        if (!introSkipped)
        {
            cameraController.SetZoom(introZoom);

            foreach (CockroachMovement player in players)
            {
                if (introSkipped) break;
                if (!IsAlive(player)) continue;

                cameraController.SetTarget(player.transform);

                // Wait for the camera to get most of the way there, then show the label.
                yield return StartCoroutine(WaitUnlessSkipped(IntroLabelDelay));
                if (introSkipped) break;

                PlayerLabel label = player.GetComponent<PlayerLabel>();
                if (label != null) label.ShowLabel();

                yield return StartCoroutine(WaitUnlessSkipped(Mathf.Max(0f, introFocusTime - IntroLabelDelay)));
            }
        }

        // Zoom back out to where the player had it. Skipping goes straight on.
        cameraController.SetZoom(originalZoom);

        if (!introSkipped)
        {
            yield return StartCoroutine(WaitUnlessSkipped(IntroZoomOutTime));
        }

        cameraController.EndIntro();

        // Player 1 (the first living player in the list) starts.
        AdvanceToNextPlayer();
    }

    /// <summary>Waits the given seconds, but stops early if the skip key is pressed.</summary>
    private IEnumerator WaitUnlessSkipped(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !introSkipped)
        {
            if (Input.GetKeyDown(skipIntroKey)) introSkipped = true;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe so we never get calls from objects that outlive this manager.
        foreach (CockroachMovement player in players)
        {
            if (player == null) continue;

            CockroachShooting shooting = player.GetComponent<CockroachShooting>();
            if (shooting != null)
            {
                shooting.OnFired -= HandleShotFired;
                shooting.OnProjectileLaunched -= HandleProjectileLaunched;
            }

            Stamina stamina = player.GetComponent<Stamina>();
            if (stamina != null)
            {
                stamina.OnStaminaDepleted -= HandleStaminaDepleted;
            }
        }

        // In case a turn ends mid-flight (e.g. this object is destroyed while a
        // shot is in the air), make sure we don't leave a dangling subscription
        // on a projectile that may outlive us briefly.
        if (activeProjectile != null)
        {
            activeProjectile.OnLanded -= HandleProjectileLanded;
        }
    }

    private void Update()
    {
        if (gameOver || currentIndex < 0) return;
        if (GameMenu.IsPaused) return; // keys pressed in the menu must not end the turn
        if (betweenTurns) return;      // a crate drop is playing; HandleCrateDropFinished moves on

        // The turn ended while someone's death sequence was playing: wait for it, then carry on.
        if (advancePending)
        {
            if (!IsAnyDeathSequencePlaying())
            {
                advancePending = false;
                AdvanceToNextPlayer();
            }

            return;
        }

        // Enter while a health bar is counting down: skip the countdown and do nothing else.
        // Returning here also means this same key press can never end the turn (Enter is
        // also the manual end-turn key). The next press, with nothing counting, ends it as usual.
        if (Input.GetKeyDown(skipHealthCountKey) && SkipHealthCountdowns())
        {
            return;
        }

        CockroachMovement current = players[currentIndex];

        if (!shotFired)
        {
            // Died before firing: nothing left to wait for.
            if (!IsAlive(current))
            {
                EndTurn();
                return;
            }

            // Everyone else is gone (for example they fell in the acid) and their death
            // has finished: the match is over, no need to wait for this turn to run out.
            if (players.Count > 1 && CountAlivePlayers() <= 1 && !IsAnyDeathSequencePlaying())
            {
                EndTurn();
                return;
            }

            if (Input.GetKeyDown(GameSettings.Key(GameAction.EndTurn)))
            {
                EndTurn();
                return;
            }

            if (turnTimeLimit > 0f)
            {
                turnTimeLeft -= Time.deltaTime;
                if (turnTimeLeft <= 0f)
                {
                    EndTurn();
                }
            }

            return;
        }

        if (waitingForImpact)
        {
            // Still in the air. HandleProjectileLanded (via Projectile.OnLanded)
            // is what moves things forward from here — not a timer — so a long
            // shot is never cut off before it actually lands.
            if (activeProjectile != null && Input.GetKeyDown(toggleCameraViewKey))
            {
                followingProjectile = !followingProjectile;

                if (cameraController != null)
                {
                    Transform viewTarget = followingProjectile ? activeProjectile.transform : current.transform;
                    cameraController.SetTarget(viewTarget);
                }
            }

            return;
        }

        // A special attack is still playing (meteor falling, UFO firing, nuke counting down...):
        // hold the turn, and give it a short settle time once it has finished.
        if (AttackRunner.AnyActive)
        {
            endDelayLeft = Mathf.Max(endDelayLeft, settleAfterSpecialAttack);
            return;
        }

        // Landed: wait for the explosion and knockback to settle.
        endDelayLeft -= Time.deltaTime;
        if (endDelayLeft <= 0f)
        {
            // Hold the turn while a health number is still counting down, so the
            // next player doesn't start (or the game doesn't end) before it is seen.
            if (IsAnyHealthCountingDown())
            {
                return;
            }

            EndTurn();
        }
    }

    /// <summary>True while any player's health bar is still showing a hit (pause, label or countdown).</summary>
    private bool IsAnyHealthCountingDown()
    {
        foreach (CockroachMovement player in players)
        {
            if (player == null) continue;

            HealthBar healthBar = player.GetComponent<HealthBar>();
            if (healthBar != null && healthBar.IsAnimating)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Skips every health countdown that is playing. Returns true if there was
    /// anything to skip, so the caller knows the key press has been used up.
    /// </summary>
    private bool SkipHealthCountdowns()
    {
        bool skippedAny = false;

        foreach (CockroachMovement player in players)
        {
            if (player == null) continue;

            HealthBar healthBar = player.GetComponent<HealthBar>();
            if (healthBar != null && healthBar.IsAnimating)
            {
                healthBar.SkipAnimation();
                skippedAny = true;
            }
        }

        return skippedAny;
    }

    /// <summary>
    /// Called by CockroachShooting.OnFired. Only the active player can fire
    /// (everyone else has isMyTurn = false), so this always means "the current player fired".
    /// </summary>
    private void HandleShotFired()
    {
        if (gameOver || shotFired || currentIndex < 0) return;

        CockroachMovement current = players[currentIndex];
        CockroachShooting shooting = current.GetComponent<CockroachShooting>();

        // Some weapons (step 13) will not end the turn when fired — a utility
        // item, say. If this one doesn't, leave the turn running as normal.
        if (shooting != null && !shooting.EndsTurnOnFire) return;

        shotFired = true;

        // A normal shot reports its projectile just before this, so we wait for it to land.
        // Special attacks without a projectile (Satelaser, Airstrike, Doom...) go straight to the
        // settle phase, which waits for their effects (AttackRunner) to finish.
        waitingForImpact = activeProjectile != null;
        if (!waitingForImpact) endDelayLeft = endTurnDelay;

        // Lock the shooter out right away: one shot per turn, no walking while waiting.
        current.isMyTurn = false;
    }

    /// <summary>
    /// Called by CockroachShooting.OnProjectileLaunched. Starts following the
    /// projectile with the camera and listens for it to land.
    /// </summary>
    private void HandleProjectileLaunched(Projectile projectile)
    {
        activeProjectile = projectile;
        followingProjectile = true;
        projectile.OnLanded += HandleProjectileLanded;

        if (cameraController != null)
        {
            cameraController.SetTarget(projectile.transform);
        }
    }

    /// <summary>
    /// Called by Projectile.OnLanded once the shot actually lands (hit
    /// something, or its own max lifetime ran out). This is what ends the
    /// "waiting for impact" phase and starts the normal settle delay — not a
    /// fixed timer started the moment the shot was fired.
    ///
    /// The camera deliberately stays on the impact point rather than
    /// snapping straight back to the shooter: it holds there for the rest of
    /// the settle delay, then StartTurn naturally pans to whoever plays next
    /// once the turn actually passes.
    /// </summary>
    private void HandleProjectileLanded(Vector2 landingPosition)
    {
        if (activeProjectile != null)
        {
            activeProjectile.OnLanded -= HandleProjectileLanded;
        }

        activeProjectile = null;
        followingProjectile = false;
        waitingForImpact = false;
        endDelayLeft = endTurnDelay;

        if (cameraController != null)
        {
            cameraController.SetTargetPosition(landingPosition);
        }
    }

    /// <summary>
    /// Called by Stamina.OnStaminaDepleted. Only the active player's stamina
    /// drains (Stamina checks isMyTurn itself), so this always means the
    /// current player ran out.
    /// </summary>
    private void HandleStaminaDepleted()
    {
        if (gameOver || shotFired || currentIndex < 0) return;

        Debug.Log("TurnManager: stamina depleted, ending turn.");
        EndTurn();
    }

    private void EndTurn()
    {
        if (currentIndex >= 0 && players[currentIndex] != null)
        {
            players[currentIndex].isMyTurn = false;
        }

        AdvanceToNextPlayer();
    }

    /// <summary>
    /// Finds the next living player after the current one (wrapping around the list)
    /// and starts their turn.
    /// </summary>
    private void AdvanceToNextPlayer()
    {
        // Someone is still dying (dizzy, then the explosion): wait until that is over, so the
        // next turn, or the game-over screen, does not start on top of it. Update retries.
        if (IsAnyDeathSequencePlaying())
        {
            advancePending = true;
            return;
        }

        // With 2+ players in the match, one survivor means the match is over.
        // (With a single cockroach in the scene we keep going so you can test alone.)
        if (players.Count > 1 && CountAlivePlayers() <= 1)
        {
            EndGame();
            return;
        }

        // Supply crates: once per finished turn (never before the first turn), roll for a plane
        // drop. If one happens, the next turn waits until the crate has landed.
        if (!crateRollDone && currentIndex >= 0 && crateDrops != null && crateDrops.isActiveAndEnabled)
        {
            crateRollDone = true;

            if (crateDrops.TryStartTurnEndDrop(HandleCrateDropFinished))
            {
                betweenTurns = true;
                return;
            }
        }

        for (int i = 1; i <= players.Count; i++)
        {
            int candidate = (currentIndex + i) % players.Count;

            if (IsAlive(players[candidate]))
            {
                StartTurn(candidate);
                return;
            }
        }

        // Nobody is alive at all.
        EndGame();
    }

    private void StartTurn(int index)
    {
        // Wrapping back to (or past) the start of the list begins a new round.
        if (currentIndex < 0 || index <= currentIndex)
        {
            roundNumber++;

            // Weapons with a round delay unlock from the round number.
            foreach (CockroachMovement player in players)
            {
                CockroachShooting playerShooting = player != null ? player.GetComponent<CockroachShooting>() : null;
                if (playerShooting != null) playerShooting.Round = roundNumber;
            }
        }

        currentIndex = index;
        shotFired = false;
        crateRollDone = false;
        turnTimeLeft = turnTimeLimit;

        // Defensive cleanup: a fresh turn should never start still watching an
        // old projectile. In the normal flow HandleProjectileLanded already
        // cleared these, but this guards against any stuck edge case.
        if (activeProjectile != null)
        {
            activeProjectile.OnLanded -= HandleProjectileLanded;
            activeProjectile = null;
        }
        waitingForImpact = false;
        followingProjectile = false;

        players[index].isMyTurn = true;

        Stamina stamina = players[index].GetComponent<Stamina>();
        if (stamina != null)
        {
            stamina.ResetStamina();
        }

        if (cameraController != null)
        {
            cameraController.SetTarget(players[index].transform);
        }

        // 1v1 only for now: the "opponent" is just the other player in the list.
        if (opponentIndicator != null && players.Count == 2)
        {
            CockroachMovement opponent = players[(index + 1) % players.Count];
            opponentIndicator.SetTarget(opponent.transform, players[index].transform);
        }
        Debug.Log("TurnManager: it is now " + players[index].name + "'s turn.");

        OnTurnStarted?.Invoke(players[index]);
    }

    /// <summary>
    /// Called by CrateDropManager when the between-turns crate has landed (or the drop gave up).
    /// Runs the normal advance again, so a death or game over during the drop is still handled;
    /// crateRollDone stops it from rolling a second crate for the same turn.
    /// </summary>
    private void HandleCrateDropFinished()
    {
        if (!betweenTurns) return;
        betweenTurns = false;

        if (!gameOver) AdvanceToNextPlayer();
    }

    private void EndGame()
    {
        gameOver = true;
        currentIndex = -1;

        CockroachMovement winner = null;

        foreach (CockroachMovement player in players)
        {
            if (IsAlive(player))
            {
                winner = player;
                break;
            }
        }

        if (winner != null)
        {
            Debug.Log("TurnManager: game over. Winner: " + winner.name);
        }
        else
        {
            Debug.Log("TurnManager: game over. Nobody survived.");
        }

        // Camera to the winner, "WINNER" text, then the Retry / Home panel.
        // (winner is null for a draw.)
        if (gameOverSequence == null)
        {
            gameOverSequence = FindFirstObjectByType<GameOverSequence>();
        }

        if (gameOverSequence != null)
        {
            gameOverSequence.Begin(winner);
        }
    }

    /// <summary>True while any player is still in its death sequence (dizzy, then the explosion).</summary>
    private bool IsAnyDeathSequencePlaying()
    {
        foreach (CockroachMovement player in players)
        {
            if (player == null) continue;

            CockroachDeath death = player.GetComponent<CockroachDeath>();
            if (death != null && death.IsPlaying)
            {
                return true;
            }
        }

        return false;
    }

    private int CountAlivePlayers()
    {
        int count = 0;
        foreach (CockroachMovement player in players)
        {
            if (IsAlive(player)) count++;
        }
        return count;
    }

    /// <summary>
    /// A player counts as alive if it still exists, is active in the scene,
    /// and its Health says it is not dead. Checking Health (not just "active")
    /// matters because a dead cockroach stays active for a moment while its
    /// death animation plays.
    /// </summary>
    private bool IsAlive(CockroachMovement player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;

        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }
}