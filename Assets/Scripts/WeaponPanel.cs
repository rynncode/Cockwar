using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Step 13: weapon panel UI.
/// Builds a "Weapons" button in the top-right corner and a dropdown list of the
/// active player's weapons. Everything is created in code, so the only setup is
/// adding this script to an empty GameObject in the scene.
/// The button is locked when it is not a player's turn, while a shot is in
/// flight, and while a shot is being charged.
/// </summary>
public class WeaponPanel : MonoBehaviour
{
    [Tooltip("Optional. If empty, the first Canvas in the scene is used (one is created if none exists).")]
    public Canvas canvas;

    [Tooltip("Optional. Found automatically if empty.")]
    public TurnManager turnManager;

    [Header("Layout")]
    public Vector2 buttonSize = new Vector2(260f, 56f);
    public Vector2 margin = new Vector2(20f, 20f);
    public float fontSize = 26f;

    [Header("Colors")]
    public Color buttonColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    public Color lockedColor = new Color(0.15f, 0.15f, 0.15f, 0.45f);
    public Color currentColor = new Color(0.25f, 0.55f, 0.25f, 0.95f);
    public Color emptyColor = new Color(0.45f, 0.2f, 0.2f, 0.9f);

    private Button mainButton;
    private Image mainImage;
    private TextMeshProUGUI mainLabel;
    private RectTransform listRoot;

    private CockroachShooting shootingShown;
    private readonly List<GameObject> entries = new List<GameObject>();

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
        EnsureCanvas();
        BuildMainButton();
        BuildList();
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        es.transform.SetParent(null);
    }

    private void EnsureCanvas()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) return;

        GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }

    // --- Building the UI ---

    private Button MakeButton(string name, Transform parent, out Image image, out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        image = go.GetComponent<Image>();
        image.color = buttonColor;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        label = textGo.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(10f, 0f);
        lr.offsetMax = new Vector2(-10f, 0f);

        return go.GetComponent<Button>();
    }

    private void BuildMainButton()
    {
        mainButton = MakeButton("WeaponsButton", canvas.transform, out mainImage, out mainLabel);
        RectTransform rt = (RectTransform)mainButton.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-margin.x, -margin.y);
        rt.sizeDelta = buttonSize;
        mainButton.onClick.AddListener(ToggleList);
    }

    private void BuildList()
    {
        GameObject go = new GameObject("WeaponList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(canvas.transform, false);
        listRoot = (RectTransform)go.transform;
        listRoot.anchorMin = listRoot.anchorMax = listRoot.pivot = new Vector2(1f, 1f);
        listRoot.anchoredPosition = new Vector2(-margin.x, -margin.y - buttonSize.y - 6f);

        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter f = go.GetComponent<ContentSizeFitter>();
        f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        listRoot.sizeDelta = new Vector2(buttonSize.x, 0f);
        go.SetActive(false);
    }

    private void RebuildEntries(CockroachShooting shooting)
    {
        foreach (GameObject e in entries) Destroy(e);
        entries.Clear();
        if (shooting == null) return;

        for (int i = 0; i < shooting.weapons.Count; i++)
        {
            WeaponData w = shooting.weapons[i];
            if (w == null) continue;

            Image img;
            TextMeshProUGUI label;
            Button b = MakeButton("Weapon_" + i, listRoot, out img, out label);

            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = buttonSize.y;

            if (w.icon != null)
            {
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(b.transform, false);
                Image icon = iconGo.GetComponent<Image>();
                icon.sprite = w.icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0f, 0.5f);
                ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(8f, 0f);
                ir.sizeDelta = new Vector2(buttonSize.y - 12f, buttonSize.y - 12f);
            }

            int index = i;
            CockroachShooting captured = shooting;
            b.onClick.AddListener(() =>
            {
                captured.SelectWeapon(index);
                listRoot.gameObject.SetActive(false);
            });

            entries.Add(b.gameObject);
        }
        RefreshEntries(shooting);
    }

    private static string AmmoText(int ammo) => ammo < 0 ? "" : "  x" + ammo;

    private void RefreshEntries(CockroachShooting shooting)
    {
        int slot = 0;
        for (int i = 0; i < shooting.weapons.Count; i++)
        {
            WeaponData w = shooting.weapons[i];
            if (w == null) continue;

            GameObject e = entries[slot++];
            int ammo = shooting.GetAmmo(i);
            e.GetComponentInChildren<TextMeshProUGUI>().text = w.displayName + AmmoText(ammo);

            Image img = e.GetComponent<Image>();
            if (ammo == 0) img.color = emptyColor;
            else img.color = (i == shooting.CurrentWeaponIndex) ? currentColor : buttonColor;
        }
    }

    private void ToggleList()
    {
        listRoot.gameObject.SetActive(!listRoot.gameObject.activeSelf);
    }

    // --- Per-frame state ---

    private void Update()
    {
        CockroachMovement player = turnManager.CurrentPlayer;
        CockroachShooting shooting = player != null ? player.GetComponent<CockroachShooting>() : null;

        bool hasWeapons = shooting != null && shooting.weapons != null && shooting.weapons.Count > 0;
        bool canUse = hasWeapons
                      && player.isMyTurn
                      && !turnManager.IsShotInFlight
                      && !shooting.IsCharging;

        mainButton.interactable = canUse;
        mainImage.color = canUse ? buttonColor : lockedColor;

        if (!canUse && listRoot.gameObject.activeSelf) listRoot.gameObject.SetActive(false);

        if (!hasWeapons)
        {
            mainLabel.text = "Weapons";
            return;
        }

        // Rebuild the list if the active player changed (their list may differ).
        if (shooting != shootingShown)
        {
            shootingShown = shooting;
            RebuildEntries(shooting);
        }
        else
        {
            RefreshEntries(shooting);
        }

        WeaponData cur = shooting.CurrentWeapon;
        mainLabel.text = (cur != null ? cur.displayName + AmmoText(shooting.GetAmmo(shooting.CurrentWeaponIndex)) : "Weapons") + "  (change)";
    }
}
