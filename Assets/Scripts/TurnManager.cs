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
///  - the player manually ends it (manualEndTurnKey for now; a real button
///    arrives in step 15), or
///  - the active player's Stamina (optional component) runs out, or
///  - the active player fires a weapon that has endsTurnOnFire = true
///    (after a short delay so the explosion and knockback can settle), or
///  - the turn time limit runs out, or
///  - the active player dies.
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

    [Header("Manual End Turn")]
    [Tooltip("Press this key to end the current turn immediately. Stands in for a real End Turn button until step 15 (UI) adds one.")]
    public KeyCode manualEndTurnKey = KeyCode.Return;

    [Header("Step 10: Camera")]
    [Tooltip("Drag the Main Camera here (the one with CameraController on it). When set, the camera pans to whoever's turn it is.")]
    public CameraController cameraController;

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

    // Seconds into each player's focus before their label appears (camera has mostly arrived).
    private const float IntroLabelDelay = 0.5f;

    // Seconds spent zooming back out before the first turn starts.
    private const float IntroZoomOutTime = 1.2f;

    private bool introSkipped;

    // Index into the players list of whoever is playing now. -1 = nobody yet.
    private int currentIndex = -1;

    // True once the active player has fired this turn.
    private bool shotFired;

    private float turnTimeLeft;
    private float endDelayLeft;
    private bool gameOver;

    // --- Read-only state for other scripts (turn UI in step 15) ---

    /// <summary>The cockroach whose turn it is, or null if there is none.</summary>
    public CockroachMovement CurrentPlayer =>
        (currentIndex >= 0 && currentIndex < players.Count) ? players[currentIndex] : null;

    /// <summary>Seconds left in the current turn (only counts down before a shot).</summary>
    public float TurnTimeLeft => turnTimeLeft;

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
            }

            Stamina stamina = player.GetComponent<Stamina>();
            if (stamina != null)
            {
                stamina.OnStaminaDepleted -= HandleStaminaDepleted;
            }
        }
    }

    private void Update()
    {
        if (gameOver || currentIndex < 0) return;

        CockroachMovement current = players[currentIndex];

        if (!shotFired)
        {
            // Died before firing: nothing left to wait for.
            if (!IsAlive(current))
            {
                EndTurn();
                return;
            }

            if (Input.GetKeyDown(manualEndTurnKey))
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
        }
        else
        {
            // Shot already fired: wait for the explosion and knockback to settle.
            endDelayLeft -= Time.deltaTime;
            if (endDelayLeft <= 0f)
            {
                EndTurn();
            }
        }
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
        if (shooting != null && !shooting.endsTurnOnFire) return;

        shotFired = true;
        endDelayLeft = endTurnDelay;

        // Lock the shooter out right away: one shot per turn, no walking while waiting.
        current.isMyTurn = false;
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
        // With 2+ players in the match, one survivor means the match is over.
        // (With a single cockroach in the scene we keep going so you can test alone.)
        if (players.Count > 1 && CountAlivePlayers() <= 1)
        {
            EndGame();
            return;
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
        currentIndex = index;
        shotFired = false;
        turnTimeLeft = turnTimeLimit;

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

        Debug.Log("TurnManager: it is now " + players[index].name + "'s turn.");
    }

    private void EndGame()
    {
        gameOver = true;
        currentIndex = -1;

        foreach (CockroachMovement player in players)
        {
            if (IsAlive(player))
            {
                Debug.Log("TurnManager: game over. Winner: " + player.name);
                return;
            }
        }

        Debug.Log("TurnManager: game over. Nobody survived.");
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