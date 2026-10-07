using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Worms-style supply crates.
///
/// Match start: a random number of crates (CrateSettings min..max) is placed on the ground,
/// spread across the map: the map is split into one strip per crate and each crate is placed in
/// its own strip, on flat top-surface ground (open sky above, never in a cave, never in the
/// acid), away from the players and from each other. If the map has no room, fewer are placed.
///
/// Between turns: TurnManager calls TryStartTurnEndDrop once per finished turn. With dropChance
/// (50% by default) a plane flies over, drops a crate on a parachute and leaves; the next turn
/// waits until the crate has landed. The drop spot is picked from many random candidates, scored
/// so it prefers spots away from other crates, away from recent drops, and away from the player
/// who moves next (who would otherwise reach it first), with some randomness on top.
///
/// Crates are neutral: whichever cockroach touches one first gets its weapon (see SupplyCrate).
/// Every setting is in the CrateSettings asset (Assets/Resources/CrateSettings).
/// TurnManager adds this component by itself; add it to the scene yourself to pick other settings.
/// </summary>
public class CrateDropManager : MonoBehaviour
{
    [Tooltip("Leave empty to use Assets/Resources/CrateSettings.")]
    public CrateSettings settings;

    private TurnManager turnManager;
    private TerrainGenerator terrain;
    private CameraController cameraController;

    private readonly List<SupplyCrate> crates = new List<SupplyCrate>();
    private readonly List<float> recentDropX = new List<float>();
    private CratePing ping;
    private bool dropRunning;
    private float acidY;

    /// <summary>Crates currently on the map.</summary>
    public IReadOnlyList<SupplyCrate> Crates => crates;

    /// <summary>True while a plane delivery is in progress.</summary>
    public bool IsDropRunning => dropRunning;

    private void Awake()
    {
        if (settings == null) settings = CrateSettings.Load();
        turnManager = GetComponent<TurnManager>();
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        if (turnManager != null) turnManager.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        if (turnManager != null) turnManager.OnTurnStarted -= HandleTurnStarted;
    }

    // Start, not Awake: the terrain builds itself and places the players in its own Awake.
    private IEnumerator Start()
    {
        // Wait for the CHOOSE MATCH screen: 2 VS 2 adds cockroaches and moves everyone, so crates
        // (and testing weapons) must wait until the line-up is final.
        while (!MatchSetup.IsReady) yield return null;

        terrain = TerrainGenerator.Instance != null ? TerrainGenerator.Instance : FindFirstObjectByType<TerrainGenerator>();
        cameraController = turnManager != null && turnManager.cameraController != null
            ? turnManager.cameraController
            : FindFirstObjectByType<CameraController>();

        // Testing option: hand out the listed weapons before anything else, so it works even with crates off.
        if (turnManager != null)
        {
            foreach (CockroachMovement player in turnManager.players)
            {
                CockroachShooting shooting = player != null ? player.GetComponent<CockroachShooting>() : null;
                if (shooting == null) continue;
                foreach (WeaponData weapon in settings.showEmptyAtStart) shooting.GiveWeapon(weapon, 0);
                foreach (WeaponData weapon in settings.giveEveryPlayerAtStart) shooting.GiveWeapon(weapon, 1);
            }
        }

        if (terrain == null)
        {
            Debug.LogWarning("CrateDropManager: no TerrainGenerator in the scene, so supply crates are switched off.");
            enabled = false;
            yield break;
        }

        if (!settings.HasAnyContent())
        {
            Debug.LogWarning("CrateDropManager: every crate kind is switched off (or empty) in CrateSettings, so supply crates are switched off.");
            enabled = false;
            yield break;
        }

        // Same surface line the acid uses; a map with no acid just uses the map's bottom.
        Rect map = terrain.WorldBounds;
        AcidHazard acid = FindFirstObjectByType<AcidHazard>();
        acidY = acid != null ? map.yMin + acid.surfaceHeightAboveBottom : map.yMin - 10f;

        ping = gameObject.AddComponent<CratePing>();
        ping.Init(settings);

        SpawnStartingCrates();

        // Without an intro the first turn can start before these crates exist; ping them now.
        if (turnManager != null && turnManager.CurrentPlayer != null) ping.Begin(crates);
    }

    // ------------------------------------------------------------------
    // Starting crates
    // ------------------------------------------------------------------

    private void SpawnStartingCrates()
    {
        int wanted = Random.Range(settings.minStartingCrates, settings.maxStartingCrates + 1);
        if (wanted <= 0) return;

        Rect map = terrain.WorldBounds;
        float left = map.xMin + settings.edgeMargin;
        float width = map.width - 2f * settings.edgeMargin;
        if (width <= 0f) return;

        // One strip per crate, in a shuffled order, so crates spread over the whole map.
        float stripWidth = width / wanted;
        List<int> strips = new List<int>();
        for (int i = 0; i < wanted; i++) strips.Add(i);
        Shuffle(strips);

        int placed = 0;
        foreach (int strip in strips)
        {
            float stripLeft = left + strip * stripWidth;

            // Try inside the crate's own strip first, then anywhere on the map.
            if (!TryFindSpot(stripLeft, stripWidth, 30, out Vector2 spot) &&
                !TryFindSpot(left, width, 60, out spot))
                continue;

            Vector2 position = spot + Vector2.up * (settings.crateSize * 0.5f + 0.5f);
            crates.Add(SupplyCrate.Create(this, settings, position, false, acidY));
            placed++;
        }

        if (placed < wanted)
            Debug.Log("CrateDropManager: only room for " + placed + " of " + wanted + " starting crates on this map.");
    }

    /// <summary>Random flat spot in [xMin, xMin + width] that keeps its distance from players and crates.</summary>
    private bool TryFindSpot(float xMin, float width, int attempts, out Vector2 spot)
    {
        for (int i = 0; i < attempts; i++)
        {
            float x = xMin + Random.value * width;
            if (IsValidSpot(x, out spot)) return true;
        }

        spot = Vector2.zero;
        return false;
    }

    /// <summary>
    /// A spot is valid if it is flat top-surface ground (open sky above, inside the map, above
    /// the acid) and is far enough from every living player and every crate.
    /// </summary>
    private bool IsValidSpot(float x, out Vector2 spot)
    {
        if (!terrain.TryFindSurface(x, settings.flatHalfWidth, settings.maxSlope, out spot)) return false;
        if (spot.y < acidY + settings.minHeightAboveAcid) return false;

        foreach (CockroachMovement player in LivingPlayers())
        {
            if (Vector2.Distance(spot, player.transform.position) < settings.minimumPlayerDistance) return false;
        }

        foreach (SupplyCrate crate in crates)
        {
            if (crate == null) continue;
            if (Mathf.Abs(crate.transform.position.x - spot.x) < settings.minimumCrateDistance &&
                Vector2.Distance(crate.transform.position, spot) < settings.minimumCrateDistance) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Plane drops between turns
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by TurnManager once per finished turn. Rolls the drop chance; on a hit, starts the
    /// plane and returns true, then calls onFinished once the crate has landed (or the drop
    /// gave up). Returns false (and never calls onFinished) when there is no drop this time.
    /// </summary>
    public bool TryStartTurnEndDrop(System.Action onFinished)
    {
        if (!enabled || dropRunning || terrain == null) return false;

        CleanUpList();
        if (settings.maxCratesOnMap > 0 && crates.Count >= settings.maxCratesOnMap) return false;
        if (Random.value >= settings.dropChance) return false;

        if (!TryPickDropSpot(PeekNextPlayer(), out Vector2 spot))
        {
            Debug.Log("CrateDropManager: no free spot for a crate drop this turn.");
            return false;
        }

        StartCoroutine(RunDrop(spot, onFinished));
        return true;
    }

    private IEnumerator RunDrop(Vector2 spot, System.Action onFinished)
    {
        dropRunning = true;
        float timeLeft = Mathf.Max(2f, settings.maxDropSeconds);

        RememberDrop(spot.x);

        if (settings.delayBeforeAircraft > 0f) yield return new WaitForSeconds(settings.delayBeforeAircraft);

        // Comes in from a random side, so there is no pattern to learn.
        float side = Random.value < 0.5f ? -1f : 1f;

        float approach = Mathf.Max(20f, settings.aircraftApproachDistance);
        float height = Mathf.Max(terrain.HighestGroundY(), spot.y) + settings.aircraftHeightAboveGround;

        SupplyCrate crate = null;
        CrateAircraft plane = CrateAircraft.Launch(settings, spot.x + side * approach, spot.x, spot.x - side * approach, height,
            dropPoint =>
            {
                crate = SupplyCrate.Create(this, settings, dropPoint, true, acidY);
                crates.Add(crate);
                if (settings.dropSound != null) Sfx.Play(settings.dropSound, settings.crateVolume);
                if (settings.cameraFollowsDrop && cameraController != null) cameraController.SetTarget(crate.transform);
            });

        if (settings.cameraFollowsDrop && cameraController != null) cameraController.SetTarget(plane.transform);

        // Wait for the drop, then for the landing. The time limit means a stuck crate can never
        // hold up the match; a crate that is collected or sinks on the way counts as done.
        while (timeLeft > 0f && plane != null && !plane.HasDropped)
        {
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        // The next turn starts a moment after the release; the crate finishes falling on its own.
        if (settings.delayAfterDrop > 0f) yield return new WaitForSeconds(settings.delayAfterDrop);

        dropRunning = false;
        onFinished?.Invoke();
    }

    /// <summary>
    /// Scores many random valid spots and keeps the best. Higher score = further from other
    /// crates and recent drops, further from the player who moves next, plus random noise so
    /// the result never becomes predictable.
    /// </summary>
    private bool TryPickDropSpot(CockroachMovement nextPlayer, out Vector2 best)
    {
        Rect map = terrain.WorldBounds;
        float left = map.xMin + settings.edgeMargin;
        float width = map.width - 2f * settings.edgeMargin;

        best = Vector2.zero;
        float bestScore = float.MinValue;
        bool found = false;
        const float Cap = 150f; // beyond this, more distance stops mattering

        for (int i = 0; i < 40; i++)
        {
            float x = left + Random.value * width;
            if (!IsValidSpot(x, out Vector2 spot)) continue;

            float spread = Cap;
            foreach (SupplyCrate crate in crates)
                if (crate != null) spread = Mathf.Min(spread, Vector2.Distance(crate.transform.position, spot));
            foreach (float recentX in recentDropX)
                spread = Mathf.Min(spread, Mathf.Abs(recentX - spot.x));

            float awayFromNext = nextPlayer != null
                ? Mathf.Min(Cap, Vector2.Distance(nextPlayer.transform.position, spot))
                : Cap;

            float score = spread + settings.turnOrderFairness * awayFromNext + Random.value * settings.dropRandomness;
            if (score > bestScore)
            {
                bestScore = score;
                best = spot;
                found = true;
            }
        }

        return found;
    }

    private void RememberDrop(float x)
    {
        recentDropX.Add(x);
        while (recentDropX.Count > settings.rememberRecentDrops) recentDropX.RemoveAt(0);
    }

    /// <summary>The living player after the current one in turn order (the one who will move next).</summary>
    private CockroachMovement PeekNextPlayer()
    {
        if (turnManager == null || turnManager.players.Count == 0) return null;

        int start = Mathf.Max(0, turnManager.players.IndexOf(turnManager.CurrentPlayer));
        for (int i = 1; i <= turnManager.players.Count; i++)
        {
            CockroachMovement candidate = turnManager.players[(start + i) % turnManager.players.Count];
            if (IsAlive(candidate)) return candidate;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Crate bookkeeping
    // ------------------------------------------------------------------

    /// <summary>Called by a crate when it is opened, sinks or expires.</summary>
    public void Forget(SupplyCrate crate) => crates.Remove(crate);

    private void HandleTurnStarted(CockroachMovement player)
    {
        CleanUpList();

        // Expiry first, so a crate that has just run out is not pinged.
        if (settings.crateLifetimeTurns > 0)
        {
            foreach (SupplyCrate crate in crates.ToArray())
            {
                crate.TurnsAlive++;
                if (crate.TurnsAlive >= settings.crateLifetimeTurns) crate.Expire();
            }
        }

        if (ping != null) ping.Begin(crates);
    }

    private void CleanUpList() => crates.RemoveAll(crate => crate == null);

    private IEnumerable<CockroachMovement> LivingPlayers()
    {
        if (turnManager == null) yield break;
        foreach (CockroachMovement player in turnManager.players)
            if (IsAlive(player)) yield return player;
    }

    private static bool IsAlive(CockroachMovement player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
