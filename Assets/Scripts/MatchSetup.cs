using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>The two match types you can pick when a match starts.</summary>
public enum MatchMode
{
    /// <summary>Two teams of one cockroach each.</summary>
    OneVsOne,

    /// <summary>Two teams of two cockroaches each.</summary>
    TwoVsTwo
}

/// <summary>
/// One team: its name, colour, members and whose turn it is next inside the team.
/// Built by MatchSetup; TurnManager uses it for turn order and the win check, TeamHud to draw it.
/// </summary>
public class MatchTeam
{
    public string name;
    public Color color;
    public Sprite icon;
    public readonly List<CockroachMovement> members = new List<CockroachMovement>();

    /// <summary>Sum of every member's max health when the match started (the full health bar).</summary>
    public int startingHealth;

    // The member who plays this team's next turn (rotates through the living members).
    private int nextMember;

    /// <summary>Total health of the team: every living member's health added up.</summary>
    public int TotalHealth
    {
        get
        {
            int total = 0;
            foreach (CockroachMovement member in members)
            {
                if (!IsAlive(member)) continue;
                Health health = member.GetComponent<Health>();
                total += health != null ? health.CurrentHealth : 0;
            }
            return total;
        }
    }

    /// <summary>Total max health right now (shields raise it above the starting health).</summary>
    public int MaxHealth
    {
        get
        {
            int total = 0;
            foreach (CockroachMovement member in members)
            {
                Health health = member != null ? member.GetComponent<Health>() : null;
                total += health != null ? health.maxHealth : 0;
            }
            return Mathf.Max(total, startingHealth, 1);
        }
    }

    public bool HasLivingMember
    {
        get
        {
            foreach (CockroachMovement member in members)
                if (IsAlive(member)) return true;
            return false;
        }
    }

    public bool Contains(CockroachMovement player) => members.Contains(player);

    /// <summary>
    /// The member who takes this team's next turn: the next living one in the rotation, so every
    /// cockroach on the team gets a go in order (dead ones are skipped). Null if all are dead.
    /// </summary>
    public CockroachMovement TakeNextLivingMember()
    {
        for (int i = 0; i < members.Count; i++)
        {
            int index = (nextMember + i) % members.Count;
            if (!IsAlive(members[index])) continue;

            nextMember = index + 1;
            return members[index];
        }
        return null;
    }

    public static bool IsAlive(CockroachMovement player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        Health health = player.GetComponent<Health>();
        return health == null || !health.IsDead;
    }
}

/// <summary>
/// Match setup, at the start of every match:
///   1. A CHOOSE MATCH screen asks for 1 VS 1 or 2 VS 2 (keys 1 / 2 work too). Nothing moves until
///      a mode is picked. Restarting a match keeps the mode and skips the question.
///   2. The teams are built, Worms-style: two teams, each in its own colour (P1's red team and P2's
///      blue team). For 2 VS 2 the two cockroaches in the scene are copied to make P3 (red) and
///      P4 (blue), and everyone is spread over the map again.
///   3. TurnManager then plays it with the Worms rules: the teams take turns, the cockroaches inside
///      a team take turns in rotation, friendly fire is on, and the last team standing wins.
///
/// TurnManager starts this by itself; nothing to set up. Team names are on the TurnManager.
/// </summary>
public class MatchSetup : MonoBehaviour
{
    /// <summary>The mode picked for this match.</summary>
    public static MatchMode Mode { get; private set; } = MatchMode.OneVsOne;

    /// <summary>False until a mode has been picked and the teams are built. The match waits for it.</summary>
    public static bool IsReady { get; private set; } = true;

    /// <summary>True once the players are split into teams (team colours, team turn order).</summary>
    public static bool UsesTeams { get; private set; }

    private static bool hasChosen;
    private static bool keepModeOnNextLoad;

    private TurnManager turnManager;
    private GameObject canvasObject;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color PanelColor = new Color(0.36f, 0.66f, 0.74f, 0.98f);
    private static readonly Color CardColor = new Color(0.18f, 0.40f, 0.47f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Mode = MatchMode.OneVsOne;
        IsReady = true;
        UsesTeams = false;
        hasChosen = false;
        keepModeOnNextLoad = false;
    }

    /// <summary>Restarting the match: use the same mode again without asking. (Called by the pause menu.)</summary>
    public static void KeepModeForRestart() => keepModeOnNextLoad = hasChosen;

    /// <summary>Back to the Main Menu: the next match asks again.</summary>
    public static void ForgetMode() => keepModeOnNextLoad = false;

    /// <summary>Called by TurnManager.Awake. Asks for the mode (or reuses it on a restart) and builds the teams.</summary>
    public static void Begin(TurnManager turnManager)
    {
        IsReady = false;
        UsesTeams = false;

        if (keepModeOnNextLoad)
        {
            keepModeOnNextLoad = false;
            Apply(turnManager, Mode);
            return;
        }

        MatchSetup screen = new GameObject("Match Setup").AddComponent<MatchSetup>();
        screen.turnManager = turnManager;
    }

    // =====================================================================
    // Building the match
    // =====================================================================

    private static void Apply(TurnManager turnManager, MatchMode mode)
    {
        Mode = mode;
        hasChosen = true;

        List<CockroachMovement> roster = new List<CockroachMovement>();
        foreach (CockroachMovement player in turnManager.players)
            if (player != null) roster.Add(player);

        if (roster.Count >= 2)
        {
            CockroachMovement red = roster[0];
            CockroachMovement blue = roster[1];

            // Any extra cockroaches placed in the scene sit this match out.
            for (int i = 2; i < roster.Count; i++) roster[i].gameObject.SetActive(false);

            // Turn order list: red, blue, red, blue... (TurnManager alternates the teams itself).
            List<CockroachMovement> players = new List<CockroachMovement> { red, blue };
            MatchTeam redTeam = NewTeam(turnManager.team1Name, 1, red);
            MatchTeam blueTeam = NewTeam(turnManager.team2Name, 2, blue);

            if (mode == MatchMode.TwoVsTwo)
            {
                CockroachMovement red2 = CloneCockroach(red, 3);
                CockroachMovement blue2 = CloneCockroach(blue, 4);
                players.Add(red2);
                players.Add(blue2);
                redTeam.members.Add(red2);
                blueTeam.members.Add(blue2);
            }

            foreach (MatchTeam team in new[] { redTeam, blueTeam })
            {
                team.startingHealth = 0;
                foreach (CockroachMovement member in team.members)
                {
                    Health health = member.GetComponent<Health>();
                    team.startingHealth += health != null ? health.maxHealth : 0;
                }
            }

            UsesTeams = true;
            turnManager.players = players;
            turnManager.SetTeams(new List<MatchTeam> { redTeam, blueTeam });

            // New cockroaches need somewhere to stand: spread everyone over the map again.
            if (mode == MatchMode.TwoVsTwo && TerrainGenerator.Instance != null)
                TerrainGenerator.Instance.PlacePlayers();
        }

        IsReady = true;
        Debug.Log("MatchSetup: " + (mode == MatchMode.TwoVsTwo ? "2 VS 2" : "1 VS 1") + " with " + turnManager.players.Count + " cockroaches.");
    }

    private static MatchTeam NewTeam(string name, int colourNumber, CockroachMovement firstMember)
    {
        MatchTeam team = new MatchTeam
        {
            name = string.IsNullOrEmpty(name) ? "TEAM " + colourNumber : name.ToUpperInvariant(),
            color = PlayerLabel.GetPlayerColor(colourNumber),
        };
        team.members.Add(firstMember);

        // The team's portrait: the cockroach's own sprite.
        SpriteRenderer body = firstMember.GetComponentInChildren<SpriteRenderer>();
        team.icon = body != null ? body.sprite : null;
        return team;
    }

    /// <summary>
    /// Makes a teammate by copying a cockroach that is already in the scene. The copy is made under an
    /// inactive parent so its scripts start up (Awake) only after it has its new name and number, and
    /// it gets its own crosshair (the original's crosshair is a separate object it would otherwise share).
    /// </summary>
    private static CockroachMovement CloneCockroach(CockroachMovement original, int playerNumber)
    {
        GameObject holder = new GameObject("Clone Holder");
        holder.SetActive(false);

        GameObject copy = Instantiate(original.gameObject, original.transform.position, original.transform.rotation, holder.transform);
        copy.name = "Cockroach (P" + playerNumber + ")";

        PlayerLabel label = copy.GetComponent<PlayerLabel>();
        if (label != null) label.playerNumber = playerNumber;

        CockroachAim aim = copy.GetComponent<CockroachAim>();
        if (aim != null && aim.crosshair != null)
        {
            GameObject crosshair = Instantiate(aim.crosshair.gameObject);
            crosshair.name = "Crosshair (P" + playerNumber + ")";
            aim.crosshair = crosshair.transform;
        }

        CockroachMovement movement = copy.GetComponent<CockroachMovement>();
        movement.isMyTurn = false;

        // Leaving the inactive parent starts it up.
        copy.transform.SetParent(null, true);
        Destroy(holder);
        return movement;
    }

    // =====================================================================
    // The CHOOSE MATCH screen
    // =====================================================================

    private void Start()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        BuildScreen();
    }

    private void Update()
    {
        if (GameMenu.IsPaused) return;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Pick(MatchMode.OneVsOne);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Pick(MatchMode.TwoVsTwo);
    }

    private void Pick(MatchMode mode)
    {
        if (IsReady) return;
        Apply(turnManager, mode);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void BuildScreen()
    {
        canvasObject = new GameObject("MatchSetup_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;   // over the HUD, under the pause menu (100)

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = (RectTransform)canvasObject.transform;

        Image dim = NewImage("Dim", root, null, DimColor);
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        Image panel = NewPanel("Panel", root, PanelColor, new Vector2(1040f, 560f));

        PixelText("Title", panel.rectTransform, "CHOOSE MATCH", 9f, Color.white, new Vector2(0f, 200f));

        BuildCard(panel.rectTransform, new Vector2(-250f, -10f), "1 VS 1", "ONE COCKROACH EACH", "PRESS 1", MatchMode.OneVsOne);
        BuildCard(panel.rectTransform, new Vector2(250f, -10f), "2 VS 2", "TWO PER TEAM", "PRESS 2", MatchMode.TwoVsTwo);

        PixelText("Rules", panel.rectTransform, "TEAMS TAKE TURNS - LAST TEAM STANDING WINS", 3.5f,
                  new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, -225f));
    }

    private void BuildCard(RectTransform parent, Vector2 position, string title, string subtitle, string hint, MatchMode mode)
    {
        Image card = NewPanel(title, parent, CardColor, new Vector2(420f, 300f));
        card.raycastTarget = true;
        card.rectTransform.anchoredPosition = position;

        // Team colour stripes: one red and one blue cockroach marker per side.
        int perTeam = mode == MatchMode.TwoVsTwo ? 2 : 1;
        for (int side = 0; side < 2; side++)
        {
            for (int i = 0; i < perTeam; i++)
            {
                Image chip = NewPanel("Chip", card.rectTransform, PlayerLabel.GetPlayerColor(side + 1), new Vector2(46f, 46f));
                float x = (side == 0 ? -1f : 1f) * (70f + i * 56f);
                chip.rectTransform.anchoredPosition = new Vector2(x, 80f);
            }
        }
        PixelText("Vs", card.rectTransform, "VS", 4f, Color.white, new Vector2(0f, 80f));

        PixelText("Title", card.rectTransform, title, 8f, Color.white, new Vector2(0f, -5f));
        PixelText("Subtitle", card.rectTransform, subtitle, 3.5f, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, -75f));
        PixelText("Hint", card.rectTransform, hint, 3f, new Color(1f, 1f, 1f, 0.55f), new Vector2(0f, -115f));

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.3f, 1.25f, 1f);   // brightens the card under the mouse
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };   // Space / Enter must not press it
        button.onClick.AddListener(() => Pick(mode));
    }

    private static Image NewPanel(string name, Transform parent, Color color, Vector2 size)
    {
        Image image = NewImage(name, parent, PixelSprites.Panel(), color);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / 5f;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    private static void PixelText(string name, Transform parent, string text, float pixelSize, Color color, Vector2 position)
    {
        Sprite sprite = PixelSprites.Text(text);
        Image image = NewImage(name, parent, sprite, color);
        image.rectTransform.sizeDelta = sprite.rect.size * pixelSize;
        image.rectTransform.anchoredPosition = position;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
