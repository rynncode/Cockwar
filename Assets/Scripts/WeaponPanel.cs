using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Step 13: weapon panel UI, laid out like the Worms W.M.D weapon panel.
///
/// Current weapon card (top-right): the selected weapon's icon, name and ammo. It pops when the
/// weapon changes. Click it to open the panel.
///
/// The panel slides in from the right edge:
///  - one row per weapon category (Launchers, Grenades ...), with its number key on the left,
///    like the F-key rows in Worms;
///  - an icon tile per weapon with its ammo in the corner (∞ = unlimited). Empty weapons are
///    greyed out, weapons still locked by their Round Delay show the rounds left, and the
///    selected weapon has a frame in the player's color;
///  - a footer describing the weapon under the mouse (or the selected one): name, ammo,
///    damage / blast / fuse read from its prefabs, and its description.
///
/// Controls: right-click (when not charging) or the Toggle Key opens and closes the panel.
/// Click a tile to pick it; clicking anywhere else closes the panel without firing.
/// Number keys 1-9 pick a row even with the panel closed; pressing the same number again
/// moves to the next weapon in that row.
///
/// The panel is locked when it is not a player's turn, while a shot is in flight, and while
/// a shot is being charged. Everything is created in code, so the only setup is adding this
/// script to an empty GameObject in the scene.
/// </summary>
public class WeaponPanel : MonoBehaviour
{
    [Tooltip("Optional. Found automatically if empty.")]
    public TurnManager turnManager;

    [Header("Controls")]
    [Tooltip("Press this key to open or close the weapon panel. Set to None to turn the hotkey off. Already used elsewhere: A, D, Space, Tab (next weapon, skip intro), Enter, F1.")]
    public KeyCode toggleKey = KeyCode.Q;

    [Tooltip("Right-click opens and closes the panel, like Worms. (Right-click while charging still cancels the shot instead.)")]
    public bool rightClickOpens = true;

    [Tooltip("Number keys 1-9 pick a row of the panel; pressing the same number again moves to the next weapon in that row. Works with the panel closed too.")]
    public bool numberKeysSelectRow = true;

    [Header("Look")]
    [Tooltip("Size of a weapon tile, in UI units at 1920x1080.")]
    public float tileSize = 76f;

    [Tooltip("Gap between tiles and between rows.")]
    public float tileGap = 8f;

    [Tooltip("Text size of the weapon name. Other text is scaled from it.")]
    public float fontSize = 26f;

    [Tooltip("Draw order of the weapon canvas. Higher = on top of other canvases.")]
    public int canvasSortingOrder = 60;

    // Layout, in UI units at 1920x1080.
    private const float ScreenMargin = 20f;
    private const float Pad = 16f;
    private const float HeaderHeight = 40f;
    private const float LabelColumn = 150f;
    private const float FooterHeight = 128f;
    private const float MinPanelWidth = 480f;
    private const float CardWidth = 320f;
    private const float CardHeight = 84f;
    private const float PixelPanelSize = 4f;   // one pixel of the panel outlines
    private const float BadgePixelSize = 3f;   // one pixel of the ammo numbers

    private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.93f);
    private static readonly Color TileColor = new Color(0.13f, 0.13f, 0.16f, 0.95f);
    private static readonly Color TileHoverColor = new Color(0.25f, 0.25f, 0.3f, 0.98f);
    private static readonly Color KeyCapColor = new Color(0.2f, 0.2f, 0.24f, 1f);
    private static readonly Color DimTextColor = new Color(0.62f, 0.64f, 0.7f, 1f);
    private static readonly Color DisabledIconColor = new Color(0.35f, 0.35f, 0.38f, 0.55f);
    private static readonly Color EmptyColor = new Color(1f, 0.32f, 0.25f, 1f);
    private static readonly Color LockedColor = new Color(1f, 0.78f, 0.25f, 1f);

    private class Tile
    {
        public int weaponIndex;
        public WeaponData weapon;
        public RectTransform rect;
        public Vector2 basePosition;
        public Image frame;
        public Image background;
        public Image icon;
        public Image badge;
        public GameObject lockOverlay;
        public Image lockNumber;
        public float hover;
        public float appear;      // < 0 = waiting its turn to pop in, 1 = done
        public float shake;
    }

    private class Row
    {
        public WeaponCategory category;
        public Image keyCap;
        public readonly List<Tile> tiles = new List<Tile>();
    }

    // UI pieces.
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private GameObject blocker;

    private RectTransform card;
    private CanvasGroup cardGroup;
    private Image cardIcon;
    private TextMeshProUGUI cardName;
    private Image cardAmmo;
    private TextMeshProUGUI cardHint;

    private RectTransform panel;
    private CanvasGroup panelGroup;
    private RectTransform rowsRoot;
    private RectTransform divider;
    private RectTransform footer;
    private TextMeshProUGUI footerName;
    private TextMeshProUGUI footerStatus;
    private TextMeshProUGUI footerStats;
    private TextMeshProUGUI footerDescription;

    // State.
    private readonly List<Row> rows = new List<Row>();
    private readonly Dictionary<WeaponData, string> statsCache = new Dictionary<WeaponData, string>();
    private CockroachShooting shootingShown;
    private Color playerColor = Color.white;
    private Tile hovered;
    private bool isOpen;
    private float panelWidth;
    private float panelX;
    private float panelXVelocity;
    private float cardPop;
    private int lastWeaponIndex = -1;
    private int lastAmmo = int.MinValue;
    private int lastCardRounds = -1;
    private bool wasCharging;
    private WeaponData footerShown;
    private int footerAmmo;
    private int footerRounds;

    private float ClosedX => panelWidth + 40f;

    private static Vector2 CardPosition => new Vector2(-ScreenMargin - CardWidth * 0.5f, -ScreenMargin - CardHeight * 0.5f);

    private void Start()
    {
        if (turnManager == null) turnManager = FindAnyObjectByType<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError("WeaponPanel: no TurnManager found in the scene.");
            enabled = false;
            return;
        }

        EnsureEventSystem();
        BuildCanvas();
        BuildCard();
        BuildPanel();
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        es.transform.SetParent(null);
    }

    // =====================================================================
    // Building
    // =====================================================================

    private void BuildCanvas()
    {
        canvasObject = new GameObject("WeaponPanel_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = (RectTransform)canvasObject.transform;

        // Invisible full-screen catcher behind the panel: a click outside the panel closes it,
        // and because it is UI, that click never starts charging a shot.
        Image blockerImage = NewImage("ClickOutside", canvasRect, null, Color.clear);
        blockerImage.raycastTarget = true;
        Stretch(blockerImage.rectTransform);
        blocker = blockerImage.gameObject;
        AddTrigger(blocker, EventTriggerType.PointerClick, data =>
        {
            if (((PointerEventData)data).button == PointerEventData.InputButton.Left) Close();
        });
        blocker.SetActive(false);
    }

    private void BuildCard()
    {
        card = NewRect("CurrentWeapon", canvasRect);
        card.anchorMin = card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(0.5f, 0.5f); // pops from its middle
        card.anchoredPosition = CardPosition;
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();

        Image background = NewPixelPanel("Background", card, PanelColor);
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        AddTrigger(card.gameObject, EventTriggerType.PointerClick, data =>
        {
            if (((PointerEventData)data).button == PointerEventData.InputButton.Left && CanUse()) Toggle();
        });

        float slot = CardHeight - 16f;
        Image iconSlot = NewPixelPanel("IconSlot", card, TileColor);
        RectTransform slotRect = iconSlot.rectTransform;
        slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0f, 0.5f);
        slotRect.anchoredPosition = new Vector2(8f, 0f);
        slotRect.sizeDelta = new Vector2(slot, slot);

        cardIcon = NewImage("Icon", slotRect, null, Color.white);
        cardIcon.preserveAspect = true;
        Stretch(cardIcon.rectTransform, 8f);

        float textX = 8f + slot + 12f;

        cardName = NewText("Name", card, fontSize * 0.9f, TextAlignmentOptions.TopLeft, Color.white);
        cardName.fontStyle = FontStyles.Bold;
        RectTransform nameRect = cardName.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(textX, -46f);
        nameRect.offsetMax = new Vector2(-10f, -10f);

        cardAmmo = NewImage("Ammo", card, null, Color.white);
        RectTransform ammoRect = cardAmmo.rectTransform;
        ammoRect.anchorMin = ammoRect.anchorMax = ammoRect.pivot = new Vector2(0f, 0f);
        ammoRect.anchoredPosition = new Vector2(textX, 12f);

        cardHint = NewText("Hint", card, fontSize * 0.55f, TextAlignmentOptions.BottomRight, DimTextColor);
        RectTransform hintRect = cardHint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.offsetMin = new Vector2(textX, 8f);
        hintRect.offsetMax = new Vector2(-12f, 34f);

        string hint = toggleKey != KeyCode.None ? toggleKey.ToString().ToUpperInvariant() : "";
        if (rightClickOpens) hint += (hint.Length > 0 ? " / " : "") + "RIGHT-CLICK";
        cardHint.text = hint;
    }

    private void BuildPanel()
    {
        panel = NewRect("WeaponPanel", canvasRect);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;

        Image background = NewPixelPanel("Background", panel, PanelColor);
        Stretch(background.rectTransform);
        background.raycastTarget = true; // clicks on the panel's empty space don't close it or fire

        TextMeshProUGUI title = NewText("Title", panel, fontSize * 0.85f, TextAlignmentOptions.Left, Color.white);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6f;
        title.text = "WEAPONS";
        PlaceTopLeft(title.rectTransform, Pad, Pad, 220f, HeaderHeight - 8f);

        TextMeshProUGUI keys = NewText("Keys", panel, fontSize * 0.55f, TextAlignmentOptions.Right, DimTextColor);
        keys.text = (numberKeysSelectRow ? "1-9  ROW     " : "") + (rightClickOpens ? "RIGHT-CLICK  CLOSE" : "");
        RectTransform keysRect = keys.rectTransform;
        keysRect.anchorMin = new Vector2(0f, 1f);
        keysRect.anchorMax = new Vector2(1f, 1f);
        keysRect.pivot = new Vector2(1f, 1f);
        keysRect.offsetMin = new Vector2(240f, -(Pad + HeaderHeight - 8f));
        keysRect.offsetMax = new Vector2(-Pad, -Pad);

        rowsRoot = NewRect("Rows", panel);
        rowsRoot.anchorMin = rowsRoot.anchorMax = rowsRoot.pivot = new Vector2(0f, 1f);
        rowsRoot.anchoredPosition = new Vector2(Pad, -(Pad + HeaderHeight));

        Image dividerImage = NewImage("Divider", panel, null, new Color(1f, 1f, 1f, 0.1f));
        divider = dividerImage.rectTransform;
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(0.5f, 1f);

        footer = NewRect("Footer", panel);
        footer.anchorMin = new Vector2(0f, 1f);
        footer.anchorMax = new Vector2(1f, 1f);
        footer.pivot = new Vector2(0.5f, 1f);

        footerName = NewText("Name", footer, fontSize, TextAlignmentOptions.Left, Color.white);
        footerName.fontStyle = FontStyles.Bold;
        PlaceFooterLine(footerName.rectTransform, 0f, 34f);

        footerStatus = NewText("Status", footer, fontSize * 0.7f, TextAlignmentOptions.Right, Color.white);
        footerStatus.richText = true;
        PlaceFooterLine(footerStatus.rectTransform, 0f, 34f);

        footerStats = NewText("Stats", footer, fontSize * 0.62f, TextAlignmentOptions.Left, Color.white);
        footerStats.richText = true;
        PlaceFooterLine(footerStats.rectTransform, 36f, 26f);

        footerDescription = NewText("Description", footer, fontSize * 0.62f, TextAlignmentOptions.TopLeft, DimTextColor);
        footerDescription.textWrappingMode = TextWrappingModes.Normal;
        PlaceFooterLine(footerDescription.rectTransform, 66f, FooterHeight - 66f);

        LayoutPanel(0, 1);
        panelX = ClosedX;
        ApplyPanelPosition();
    }

    /// <summary>Sizes the panel for this many rows and tiles in the longest row.</summary>
    private void LayoutPanel(int rowCount, int maxTiles)
    {
        rowCount = Mathf.Max(1, rowCount);
        maxTiles = Mathf.Max(1, maxTiles);

        float rowsHeight = rowCount * (tileSize + tileGap) - tileGap;
        panelWidth = Mathf.Max(MinPanelWidth, Pad * 2f + LabelColumn + maxTiles * (tileSize + tileGap) - tileGap);

        float y = Pad + HeaderHeight + rowsHeight + 14f;
        divider.offsetMin = new Vector2(Pad, -(y + 2f));
        divider.offsetMax = new Vector2(-Pad, -y);

        y += 12f;
        footer.offsetMin = new Vector2(Pad, -(y + FooterHeight));
        footer.offsetMax = new Vector2(-Pad, -y);

        panel.sizeDelta = new Vector2(panelWidth, y + FooterHeight + Pad);
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, -(ScreenMargin + CardHeight + 10f));
    }

    /// <summary>Builds one row per category with a tile per weapon, for this player's weapon list.</summary>
    private void RebuildTiles(CockroachShooting shooting)
    {
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);
        rows.Clear();
        hovered = null;

        if (shooting == null) return;

        // Group by category, in category order, keeping the list order inside a row.
        foreach (WeaponCategory category in System.Enum.GetValues(typeof(WeaponCategory)))
        {
            Row row = null;
            for (int i = 0; i < shooting.weapons.Count; i++)
            {
                WeaponData weapon = shooting.weapons[i];
                if (weapon == null || weapon.category != category) continue;

                if (row == null)
                {
                    row = new Row { category = category };
                    rows.Add(row);
                }

                row.tiles.Add(new Tile { weaponIndex = i, weapon = weapon });
            }
        }

        int maxTiles = 1;
        foreach (Row row in rows) maxTiles = Mathf.Max(maxTiles, row.tiles.Count);
        LayoutPanel(rows.Count, maxTiles);

        for (int r = 0; r < rows.Count; r++)
            BuildRow(rows[r], r);
    }

    private void BuildRow(Row row, int rowIndex)
    {
        float top = rowIndex * (tileSize + tileGap);

        // Number key, like the F-key column in Worms.
        float capSize = 34f;
        row.keyCap = NewPixelPanel("Key" + (rowIndex + 1), rowsRoot, KeyCapColor);
        PlaceTopLeft(row.keyCap.rectTransform, 0f, top + (tileSize - capSize) * 0.5f, capSize, capSize);

        if (numberKeysSelectRow && rowIndex < 9)
        {
            Image digit = NewImage("Digit", row.keyCap.rectTransform, PixelSprites.Text((rowIndex + 1).ToString()), Color.white);
            digit.rectTransform.sizeDelta = digit.sprite.rect.size * PixelPanelSize;
        }

        TextMeshProUGUI label = NewText("Category", rowsRoot, fontSize * 0.58f, TextAlignmentOptions.Left, DimTextColor);
        label.characterSpacing = 3f;
        label.text = row.category.ToString().ToUpperInvariant();
        PlaceTopLeft(label.rectTransform, capSize + 10f, top, LabelColumn - capSize - 14f, tileSize);

        for (int c = 0; c < row.tiles.Count; c++)
        {
            Tile tile = row.tiles[c];
            BuildTile(tile, new Vector2(LabelColumn + c * (tileSize + tileGap) + tileSize * 0.5f, -(top + tileSize * 0.5f)));
            tile.appear = 1f;
        }
    }

    private void BuildTile(Tile tile, Vector2 center)
    {
        tile.rect = NewRect("Tile_" + tile.weapon.displayName, rowsRoot);
        tile.rect.anchorMin = tile.rect.anchorMax = new Vector2(0f, 1f);
        tile.rect.sizeDelta = new Vector2(tileSize, tileSize);
        tile.basePosition = center;
        tile.rect.anchoredPosition = center;

        // Frame: shows in the player's color around the selected weapon.
        tile.frame = NewPixelPanel("Frame", tile.rect, Color.clear);
        Stretch(tile.frame.rectTransform, -5f);

        tile.background = NewPixelPanel("Background", tile.rect, TileColor);
        Stretch(tile.background.rectTransform);
        tile.background.raycastTarget = true;

        tile.icon = NewImage("Icon", tile.rect, tile.weapon.icon, Color.white);
        tile.icon.preserveAspect = true;
        Stretch(tile.icon.rectTransform, 10f);
        if (tile.weapon.icon == null)
        {
            // No art: show the first letters of the name in the pixel font instead.
            string initials = tile.weapon.displayName.Length > 2 ? tile.weapon.displayName.Substring(0, 2) : tile.weapon.displayName;
            tile.icon.sprite = PixelSprites.Text(initials);
        }

        tile.badge = NewImage("Ammo", tile.rect, null, Color.white);
        RectTransform badgeRect = tile.badge.rectTransform;
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(-5f, 5f);

        // Locked by its round delay: darkened, with the rounds left in the middle.
        Image lockShade = NewPixelPanel("Locked", tile.rect, new Color(0f, 0f, 0f, 0.6f));
        Stretch(lockShade.rectTransform);
        tile.lockOverlay = lockShade.gameObject;
        tile.lockNumber = NewImage("RoundsLeft", lockShade.rectTransform, null, LockedColor);

        AddTrigger(tile.rect.gameObject, EventTriggerType.PointerEnter, _ => hovered = tile);
        AddTrigger(tile.rect.gameObject, EventTriggerType.PointerExit, _ => { if (hovered == tile) hovered = null; });
        AddTrigger(tile.rect.gameObject, EventTriggerType.PointerClick, data =>
        {
            if (((PointerEventData)data).button == PointerEventData.InputButton.Left) Pick(tile.weaponIndex, tile);
        });
    }

    // =====================================================================
    // Opening, closing, picking
    // =====================================================================

    private bool CanUse()
    {
        CockroachMovement player = turnManager.CurrentPlayer;
        return shootingShown != null
               && player != null
               && player.isMyTurn
               && shootingShown.weapons.Count > 0
               && !turnManager.IsShotInFlight
               && !shootingShown.IsCharging;
    }

    private void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;
        blocker.SetActive(true);

        // Tiles pop in one after another, row by row.
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].tiles.Count; c++)
                rows[r].tiles[c].appear = -(r * 2 + c) * 0.035f;
        }
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        hovered = null;
        blocker.SetActive(false);
    }

    /// <summary>Selects the weapon if it is usable; otherwise shakes its tile. Closes the panel on success.</summary>
    private void Pick(int weaponIndex, Tile tile)
    {
        if (!CanUse()) return;

        if (!shootingShown.IsWeaponReady(weaponIndex))
        {
            if (tile != null) tile.shake = 1f;
            return;
        }

        if (shootingShown.SelectWeapon(weaponIndex)) Close();
    }

    /// <summary>Number key: the next usable weapon in that row after the selected one (or the row's first).</summary>
    private void SelectRow(int rowIndex)
    {
        if (rowIndex >= rows.Count) return;

        List<Tile> tiles = rows[rowIndex].tiles;
        int start = tiles.FindIndex(t => t.weaponIndex == shootingShown.CurrentWeaponIndex);

        for (int step = 1; step <= tiles.Count; step++)
        {
            int i = ((start < 0 ? -1 : start) + step) % tiles.Count;
            if (shootingShown.IsWeaponReady(tiles[i].weaponIndex))
            {
                if (tiles[i].weaponIndex != shootingShown.CurrentWeaponIndex)
                    shootingShown.SelectWeapon(tiles[i].weaponIndex);
                return;
            }
        }

        // Nothing usable in this row.
        foreach (Tile tile in tiles) tile.shake = 1f;
        cardPop = -1f;
    }

    // =====================================================================
    // Per frame
    // =====================================================================

    private void Update()
    {
        float dt = Time.deltaTime;

        CockroachMovement player = turnManager.CurrentPlayer;
        CockroachShooting shooting = player != null ? player.GetComponent<CockroachShooting>() : null;
        bool hasWeapons = shooting != null && shooting.weapons != null && shooting.weapons.Count > 0;

        // New player (or no player): their weapon list may differ, so rebuild.
        if (shooting != shootingShown)
        {
            Close();
            shootingShown = shooting;
            RebuildTiles(hasWeapons ? shooting : null);
            lastWeaponIndex = -1;
            lastAmmo = int.MinValue;
            lastCardRounds = -1;
            footerShown = null;

            PlayerLabel label = player != null ? player.GetComponent<PlayerLabel>() : null;
            playerColor = PlayerLabel.GetPlayerColor(label != null ? label.playerNumber : turnManager.players.IndexOf(player) + 1);
        }

        bool canUse = hasWeapons && CanUse();
        if (!canUse) Close();

        if (canUse)
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) Toggle();

            // Right-click also cancels a charge; never open the panel on that same click.
            if (rightClickOpens && Input.GetMouseButtonDown(1) && !wasCharging) Toggle();

            if (numberKeysSelectRow)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                        SelectRow(i);
                }
            }
        }

        wasCharging = shooting != null && shooting.IsCharging;

        UpdateCard(hasWeapons, canUse, dt);
        UpdatePanel(dt);
    }

    private void UpdateCard(bool hasWeapons, bool canUse, float dt)
    {
        float targetAlpha = !hasWeapons || turnManager.CurrentPlayer == null ? 0f : canUse ? 1f : 0.55f;
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, targetAlpha, dt * 5f);
        cardGroup.blocksRaycasts = canUse;

        if (!hasWeapons) return;

        int index = shootingShown.CurrentWeaponIndex;
        WeaponData weapon = shootingShown.CurrentWeapon;
        int ammo = shootingShown.GetAmmo(index);
        int rounds = shootingShown.RoundsUntilAvailable(index);

        if (index != lastWeaponIndex || ammo != lastAmmo || rounds != lastCardRounds)
        {
            // A different weapon (not the first draw for this player) gets a pop.
            if (lastWeaponIndex >= 0 && index != lastWeaponIndex) cardPop = 1f;

            lastWeaponIndex = index;
            lastAmmo = ammo;
            lastCardRounds = rounds;

            cardName.text = weapon != null ? weapon.displayName : "";
            cardIcon.sprite = weapon != null ? weapon.icon : null;
            cardIcon.enabled = cardIcon.sprite != null;
            SetPixelText(cardAmmo, rounds > 0 ? "LOCKED" : AmmoText(ammo), 4f);
        }

        cardAmmo.color = rounds > 0 ? LockedColor : ammo == 0 ? EmptyColor : Color.white;

        // Pop bigger on a new weapon; a negative pop is a "no" shake (nothing usable in that row).
        float scale = 1f;
        float shakeX = 0f;
        if (cardPop > 0f)
        {
            cardPop = Mathf.Max(0f, cardPop - dt * 4f);
            scale = 1f + 0.1f * cardPop * cardPop;
        }
        else if (cardPop < 0f)
        {
            cardPop = Mathf.Min(0f, cardPop + dt * 3f);
            shakeX = Mathf.Sin(Time.time * 70f) * 8f * -cardPop;
        }

        card.localScale = new Vector3(scale, scale, 1f);
        card.anchoredPosition = CardPosition + new Vector2(shakeX, 0f);
    }

    private void UpdatePanel(float dt)
    {
        panelX = Mathf.SmoothDamp(panelX, isOpen ? -ScreenMargin : ClosedX, ref panelXVelocity, 0.11f);
        ApplyPanelPosition();

        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, isOpen ? 1f : 0f, dt * 8f);
        panelGroup.blocksRaycasts = isOpen;
        panelGroup.interactable = isOpen;

        if (shootingShown == null || panelGroup.alpha <= 0f) return;

        foreach (Row row in rows)
        {
            bool rowHasSelected = false;
            foreach (Tile tile in row.tiles)
            {
                UpdateTile(tile, dt);
                if (tile.weaponIndex == shootingShown.CurrentWeaponIndex) rowHasSelected = true;
            }

            row.keyCap.color = rowHasSelected ? Color.Lerp(KeyCapColor, playerColor, 0.75f) : KeyCapColor;
        }

        Tile shownTile = hovered;
        WeaponData shown = shownTile != null ? shownTile.weapon : shootingShown.CurrentWeapon;
        int shownIndex = shownTile != null ? shownTile.weaponIndex : shootingShown.CurrentWeaponIndex;
        UpdateFooter(shown, shownIndex);
    }

    private void UpdateTile(Tile tile, float dt)
    {
        int ammo = shootingShown.GetAmmo(tile.weaponIndex);
        int rounds = shootingShown.RoundsUntilAvailable(tile.weaponIndex);
        bool ready = ammo != 0 && rounds == 0;
        bool selected = tile.weaponIndex == shootingShown.CurrentWeaponIndex;
        bool isHovered = hovered == tile && isOpen;

        tile.hover = Mathf.MoveTowards(tile.hover, isHovered ? 1f : 0f, dt * 10f);
        tile.appear = Mathf.Min(1f, tile.appear + dt / 0.22f);
        tile.shake = Mathf.Max(0f, tile.shake - dt * 3.5f);

        float appear = EaseOutBack(Mathf.Clamp01(tile.appear));
        float scale = appear * (1f + 0.08f * tile.hover * (ready ? 1f : 0.4f));
        tile.rect.localScale = new Vector3(scale, scale, 1f);
        tile.rect.anchoredPosition = tile.basePosition + new Vector2(Mathf.Sin(Time.time * 70f) * 6f * tile.shake, 0f);

        tile.background.color = Color.Lerp(TileColor, TileHoverColor, tile.hover);
        tile.icon.color = ready ? Color.white : DisabledIconColor;

        Color frameColor = selected ? playerColor : new Color(1f, 1f, 1f, 0.35f * tile.hover);
        if (tile.shake > 0f) frameColor = Color.Lerp(frameColor, EmptyColor, tile.shake);
        tile.frame.color = frameColor;

        tile.lockOverlay.SetActive(rounds > 0);
        tile.badge.enabled = rounds == 0;

        if (rounds > 0)
            SetPixelText(tile.lockNumber, rounds.ToString(), 6f);
        else
        {
            SetPixelText(tile.badge, AmmoText(ammo), BadgePixelSize);
            tile.badge.color = ammo == 0 ? EmptyColor : Color.white;
        }
    }

    private void UpdateFooter(WeaponData weapon, int index)
    {
        int ammo = shootingShown.GetAmmo(index);
        int rounds = shootingShown.RoundsUntilAvailable(index);
        if (weapon == footerShown && ammo == footerAmmo && rounds == footerRounds) return;

        footerShown = weapon;
        footerAmmo = ammo;
        footerRounds = rounds;

        if (weapon == null)
        {
            footerName.text = footerStatus.text = footerStats.text = footerDescription.text = "";
            return;
        }

        footerName.text = weapon.displayName;

        if (rounds > 0)
            footerStatus.text = Colored(LockedColor, "Unlocks in " + rounds + (rounds == 1 ? " round" : " rounds"));
        else if (ammo == 0)
            footerStatus.text = Colored(EmptyColor, "Out of ammo");
        else
            footerStatus.text = ammo < 0 ? "Unlimited" : ammo + " left";

        footerStats.text = GetStats(weapon);
        footerDescription.text = weapon.description;
    }

    /// <summary>Damage, blast size, impact or fuse and shot count, read from the weapon's prefabs (cached).</summary>
    private string GetStats(WeaponData weapon)
    {
        if (statsCache.TryGetValue(weapon, out string cached)) return cached;

        List<string> parts = new List<string>();
        Projectile projectile = weapon.projectilePrefab != null ? weapon.projectilePrefab.GetComponent<Projectile>() : null;
        Explosion explosion = projectile != null && projectile.explosionPrefab != null
            ? projectile.explosionPrefab.GetComponent<Explosion>()
            : null;

        int shots = Mathf.Max(1, weapon.multiShotCount);

        if (explosion != null)
        {
            parts.Add(Stat("DAMAGE", explosion.maxDamage + (shots > 1 ? " x" + shots : "")));
            string size = explosion.blastRadius < 12f ? "Small" : explosion.blastRadius < 20f ? "Medium" : "Large";
            parts.Add(Stat("BLAST", size));
        }
        else if (shots > 1)
        {
            parts.Add(Stat("SHOTS", shots.ToString()));
        }

        if (projectile != null)
        {
            if (projectile.explodeOnImpact)
                parts.Add(Stat("TRIGGER", "Impact"));
            else
                parts.Add(Stat("FUSE", projectile.fuseSeconds.ToString("0.#") + "s" + (projectile.fuseStartsOnFirstHit ? " after landing" : "")));
        }

        if (!weapon.endsTurnOnFire) parts.Add(Colored(LockedColor, "Doesn't end turn"));

        string stats = string.Join("     ", parts);
        statsCache[weapon] = stats;
        return stats;
    }

    private static string Stat(string label, string value) => Colored(DimTextColor, label) + "  " + value;

    private static string Colored(Color color, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

    private static string AmmoText(int ammo) => ammo < 0 ? "∞" : ammo.ToString();

    private void ApplyPanelPosition()
    {
        panel.anchoredPosition = new Vector2(panelX, panel.anchoredPosition.y);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = x - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    // =====================================================================
    // Small UI helpers
    // =====================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image NewPixelPanel(string name, Transform parent, Color color)
    {
        Image image = NewImage(name, parent, PixelSprites.Panel(), color);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / PixelPanelSize;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
    {
        TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    /// <summary>Shows pixel-font text on an Image at its natural size (one font pixel = pixelSize UI units).</summary>
    private static void SetPixelText(Image image, string text, float pixelSize)
    {
        Sprite sprite = PixelSprites.Text(text);
        if (image.sprite == sprite) return;

        image.sprite = sprite;
        image.rectTransform.sizeDelta = sprite.rect.size * pixelSize;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void PlaceFooterLine(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -(top + height));
        rect.offsetMax = new Vector2(0f, -top);
    }

    private static void AddTrigger(GameObject target, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
