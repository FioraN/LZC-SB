using System.Collections.Generic;
using TMPro;
using PrototypeFPC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Perk selection panel controller.
/// UI only: candidate refresh and availability checks come from PerkSelectionRefresher.
/// </summary>
public sealed class PerkSelectionUI : MonoBehaviour
{
    [Header("Core References")]
    public PerkManager perkManager;
    public PlayerExperience playerExperience;
    public Dependencies fpcDependencies;
    public PerkSelectionRefresher selectionRefresher;

    [Header("Card Setup")]
    [Tooltip("Perk card prefab with a PerkCardUI component. Leave empty to build a default card in code.")]
    public PerkCardUI cardPrefab;

    [Tooltip("Background sprite for code-built cards. Leave empty to use a solid color.")]
    public Sprite cardBackgroundSprite;

    [Header("Card Layout")]
    [Tooltip("Center card stays on screen center. Adjust center offset / upper / lower distance here.")]
    public Vector2 centerCardOffset = Vector2.zero;
    public float upperCardDistance = 220f;
    public float lowerCardDistance = 220f;

    private bool _isOpen;
    private GameObject _pendingPrefab;

    private Canvas _canvas;
    private RectTransform _cardListRoot;
    private RectTransform _gunSelectRoot;
    private Text _gunSelectTitle;
    private Button _gunAButton;
    private Button _gunBButton;
    private Button _refreshButton;

    private readonly List<PerkCardUI> _spawnedCards = new();

    private void Awake()
    {
        if (playerExperience == null)
            playerExperience = FindFirstObjectByType<PlayerExperience>();

        BuildPanel();
        SetOpen(false);
    }

    public void Open()
    {
        if (_isOpen) return;
        SetOpen(true);
    }

    public void Close()
    {
        if (!_isOpen) return;
        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        _isOpen = open;
        _canvas.gameObject.SetActive(open);

        PerkSceneCanvasUI.IsFireBlocked = open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (fpcDependencies != null)
            fpcDependencies.isInspecting = open;

        if (open)
            ShowCardList(forceRefresh: false);
    }

    private void ShowCardList()
    {
        ShowCardList(forceRefresh: false);
    }

    private void ShowCardList(bool forceRefresh)
    {
        _pendingPrefab = null;
        _cardListRoot.gameObject.SetActive(true);
        _gunSelectRoot.gameObject.SetActive(false);
        SpawnCandidateCards(forceRefresh);
    }

    private void ShowGunSelect(GameObject perkPrefab)
    {
        _pendingPrefab = perkPrefab;
        _cardListRoot.gameObject.SetActive(false);
        _gunSelectRoot.gameObject.SetActive(true);

        if (_gunSelectTitle != null)
        {
            var meta = perkPrefab != null ? perkPrefab.GetComponent<PerkMeta>() : null;
            string nameText = meta != null ? meta.EffectiveDisplayName : (perkPrefab != null ? perkPrefab.name : "");
            _gunSelectTitle.text = $"Equip \"{nameText}\" To:";
        }

        RefreshGunSelectButtons();
    }

    private void OnGunSelected(int gunIndex)
    {
        if (_pendingPrefab == null || perkManager == null)
        {
            ShowCardList(forceRefresh: false);
            return;
        }

        if (!HasAvailableUpgradePoint())
        {
            Debug.LogWarning("[PerkSelectionUI] Not enough upgrade points to select a perk.");
            RefreshGunSelectButtons();
            return;
        }

        perkManager.RefreshAll(force: true);

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.root == null)
        {
            Debug.LogError($"[PerkSelectionUI] GunRefs.root is null (gunIndex={gunIndex}). Check PerkManager setup.");
            ShowCardList(forceRefresh: false);
            return;
        }

        var inst = perkManager.InstantiatePerkToGun(_pendingPrefab, gunIndex, gunRefs.root.transform);
        if (inst == null)
        {
            Debug.LogWarning($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} failed (prerequisite/conflict/already owned).");
        }
        else
        {
            if (!playerExperience.TrySpendUpgradePoint())
            {
                Debug.LogError("[PerkSelectionUI] Perk granted but failed to spend an upgrade point.");
                return;
            }

            Debug.Log($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} success.");
            ShowCardList(forceRefresh: true);
            return;
        }

        RefreshGunSelectButtons();
    }

    private void RefreshGunSelectButtons()
    {
        bool hasPoint = HasAvailableUpgradePoint();
        bool canEquipGunA = hasPoint && _pendingPrefab != null && perkManager != null && perkManager.CanEquipPerkToGun(_pendingPrefab, 0);
        bool canEquipGunB = hasPoint && _pendingPrefab != null && perkManager != null && perkManager.CanEquipPerkToGun(_pendingPrefab, 1);

        SetButtonSelectableVisual(_gunAButton, canEquipGunA);
        SetButtonSelectableVisual(_gunBButton, canEquipGunB);
    }

    private bool HasAvailableUpgradePoint()
    {
        return playerExperience != null && playerExperience.AvailableUpgradePoints > 0;
    }

    private void SpawnCandidateCards(bool forceRefresh)
    {
        ClearCards();

        IReadOnlyList<GameObject> candidates = selectionRefresher != null
            ? selectionRefresher.RefreshCandidates(forceRefresh)
            : System.Array.Empty<GameObject>();

        Vector2 nativeSize = Vector2.zero;
        if (cardPrefab != null)
        {
            var prefabRT = cardPrefab.GetComponent<RectTransform>();
            if (prefabRT != null && prefabRT.sizeDelta.x > 0f && prefabRT.sizeDelta.y > 0f)
                nativeSize = prefabRT.sizeDelta;
        }

        foreach (var perkPrefab in candidates)
        {
            if (perkPrefab == null) continue;

            var capturedPrefab = perkPrefab;
            PerkCardUI card;

            if (cardPrefab != null)
            {
                card = Instantiate(cardPrefab, _cardListRoot);
                if (nativeSize != Vector2.zero)
                {
                    var le = card.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = nativeSize.x;
                    le.preferredHeight = nativeSize.y;
                }
            }
            else
            {
                card = BuildDefaultCard(_cardListRoot, cardBackgroundSprite);
            }

            EnsureCardBackgroundVisible(card);
            card.Populate(capturedPrefab);

            bool selectable = selectionRefresher == null || selectionRefresher.IsPerkSelectableForAnyGun(capturedPrefab);
            card.SetSelectableVisual(selectable);

            if (selectable && card.selectButton != null)
                card.selectButton.onClick.AddListener(() => ShowGunSelect(capturedPrefab));

            _spawnedCards.Add(card);
        }

        PositionSpawnedCards();
    }

    private void PositionSpawnedCards()
    {
        int count = _spawnedCards.Count;
        if (count == 0) return;

        int centerIndex = count / 2;

        for (int i = 0; i < count; i++)
        {
            var card = _spawnedCards[i];
            if (card == null) continue;

            var rt = card.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            float y = centerCardOffset.y;
            if (i < centerIndex)
                y += upperCardDistance * (centerIndex - i);
            else if (i > centerIndex)
                y -= lowerCardDistance * (i - centerIndex);

            rt.anchoredPosition = new Vector2(centerCardOffset.x, y);
        }
    }

    private static void EnsureCardBackgroundVisible(PerkCardUI card)
    {
        if (card == null) return;

        var images = card.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image != null)
                image.enabled = true;
        }
    }

    private static void SetButtonSelectableVisual(Button button, bool selectable)
    {
        if (button == null) return;

        button.interactable = selectable;

        var image = button.GetComponent<Image>();
        if (image == null) return;

        Color baseColor = button.colors.normalColor;
        image.color = selectable ? baseColor : ToGray(baseColor);
    }

    private static Color ToGray(Color color)
    {
        float gray = color.grayscale;
        return new Color(gray, gray, gray, color.a * 0.75f);
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        _spawnedCards.Clear();
    }

    private static PerkCardUI BuildDefaultCard(Transform parent, Sprite bgSprite)
    {
        var root = new GameObject("PerkCard");
        root.transform.SetParent(parent, false);
        root.AddComponent<LayoutElement>().preferredHeight = 180f;

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.35f, 0.40f, 0.48f, 1f);
        bg.sprite = bgSprite;
        if (bgSprite != null) bg.type = Image.Type.Sliced;

        var btn = root.AddComponent<Button>();
        var bc = btn.colors;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        bc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        bc.selectedColor = Color.white;
        btn.colors = bc;

        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.42f, 0.72f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(10f, 2f);
        nameRT.offsetMax = new Vector2(-10f, -2f);

        var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.fontSize = 20;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = Color.white;
        nameTxt.alignment = TextAlignmentOptions.MidlineRight;

        var descGO = new GameObject("DescText");
        descGO.transform.SetParent(root.transform, false);
        var descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.44f);
        descRT.offsetMin = new Vector2(12f, 6f);
        descRT.offsetMax = new Vector2(-8f, -6f);

        var descTxt = descGO.AddComponent<TextMeshProUGUI>();
        descTxt.fontSize = 13;
        descTxt.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        descTxt.alignment = TextAlignmentOptions.TopLeft;

        var card = root.AddComponent<PerkCardUI>();
        card.perkNameTMP = nameTxt;
        card.descriptionTMP = descTxt;
        card.selectButton = btn;

        return card;
    }

    private void BuildPanel()
    {
        var cgo = new GameObject("PerkSelectionCanvas");
        DontDestroyOnLoad(cgo);

        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();

        var dim = NewRT("Dimmer", cgo.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);

        var panel = NewRT("MainPanel", cgo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(1000f, 740f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 0.97f);

        var header = NewRT("Header", panel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 60f);
        header.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 1f);

        var titleRT = NewRT("Title", header);
        Stretch(titleRT);
        titleRT.offsetMin = new Vector2(24f, 0f);
        titleRT.offsetMax = new Vector2(-70f, 0f);
        var titleTxt = titleRT.gameObject.AddComponent<Text>();
        titleTxt.text = "Choose A Perk";
        titleTxt.font = GetFont();
        titleTxt.fontSize = 22;
        titleTxt.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        titleTxt.alignment = TextAnchor.MiddleLeft;

        var xRT = NewRT("CloseBtn", header);
        xRT.anchorMin = new Vector2(1f, 0f);
        xRT.anchorMax = new Vector2(1f, 1f);
        xRT.pivot = new Vector2(1f, 0.5f);
        xRT.anchoredPosition = Vector2.zero;
        xRT.sizeDelta = new Vector2(60f, 0f);
        xRT.gameObject.AddComponent<Image>().color = new Color(0.50f, 0.08f, 0.08f, 1f);
        var xBtn = xRT.gameObject.AddComponent<Button>();
        var xc = xBtn.colors;
        xc.highlightedColor = new Color(0.78f, 0.12f, 0.12f, 1f);
        xc.pressedColor = new Color(0.30f, 0.04f, 0.04f, 1f);
        xBtn.colors = xc;
        xBtn.onClick.AddListener(Close);
        AddCentredText(xRT, "X", 20, Color.white);

        var refreshRT = NewRT("RefreshBtn", header);
        refreshRT.anchorMin = new Vector2(1f, 0f);
        refreshRT.anchorMax = new Vector2(1f, 1f);
        refreshRT.pivot = new Vector2(1f, 0.5f);
        refreshRT.anchoredPosition = new Vector2(-66f, 0f);
        refreshRT.sizeDelta = new Vector2(110f, 0f);
        refreshRT.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.26f, 0.52f, 1f);
        _refreshButton = refreshRT.gameObject.AddComponent<Button>();
        var rc = _refreshButton.colors;
        rc.highlightedColor = new Color(0.24f, 0.36f, 0.66f, 1f);
        rc.pressedColor = new Color(0.10f, 0.17f, 0.32f, 1f);
        _refreshButton.colors = rc;
        _refreshButton.onClick.AddListener(() => ShowCardList(forceRefresh: true));
        AddCentredText(refreshRT, "Refresh", 16, Color.white);

        var body = NewRT("Body", panel);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(0f, 0f);
        body.offsetMax = new Vector2(0f, -60f);

        _cardListRoot = NewRT("CardListRoot", cgo.transform);
        Stretch(_cardListRoot);

        _gunSelectRoot = NewRT("GunSelectPanel", body);
        Stretch(_gunSelectRoot);
        BuildGunSelectPanel(_gunSelectRoot);
    }

    private void BuildGunSelectPanel(RectTransform parent)
    {
        var col = NewRT("Column", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
        col.anchoredPosition = Vector2.zero;
        col.sizeDelta = new Vector2(520f, 0f);

        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        col.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleRT = NewRT("EquipTitle", col);
        titleRT.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        _gunSelectTitle = titleRT.gameObject.AddComponent<Text>();
        _gunSelectTitle.font = GetFont();
        _gunSelectTitle.fontSize = 20;
        _gunSelectTitle.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        _gunSelectTitle.alignment = TextAnchor.MiddleCenter;
        _gunSelectTitle.text = "Equip To:";

        _gunAButton = SpawnButton(col, "Gun  A", new Color(0.08f, 0.36f, 0.12f, 1f), 82f, 24);
        _gunAButton.onClick.AddListener(() => OnGunSelected(0));

        _gunBButton = SpawnButton(col, "Gun  B", new Color(0.08f, 0.12f, 0.38f, 1f), 82f, 24);
        _gunBButton.onClick.AddListener(() => OnGunSelected(1));

        var btnBack = SpawnButton(col, "返回", new Color(0.22f, 0.08f, 0.08f, 1f), 54f, 16);
        btnBack.onClick.AddListener(() => ShowCardList(forceRefresh: false));
    }

    private static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Button SpawnButton(RectTransform parent, string label, Color bg, float height, int fontSize)
    {
        var rt = NewRT(label, parent);
        rt.gameObject.AddComponent<Image>().color = bg;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        var btn = rt.gameObject.AddComponent<Button>();
        var c = btn.colors;
        c.normalColor = bg;
        c.highlightedColor = Color.Lerp(bg, Color.white, 0.22f);
        c.pressedColor = Color.Lerp(bg, Color.black, 0.28f);
        c.selectedColor = bg;
        btn.colors = c;

        var lblRT = NewRT("Label", rt);
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        var txt = lblRT.gameObject.AddComponent<Text>();
        txt.text = label;
        txt.font = GetFont();
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private static void AddCentredText(RectTransform parent, string text, int size, Color color)
    {
        var rt = NewRT("Label", parent);
        Stretch(rt);
        var txt = rt.gameObject.AddComponent<Text>();
        txt.text = text;
        txt.font = GetFont();
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private static Font _font;

    private static Font GetFont()
    {
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

}
