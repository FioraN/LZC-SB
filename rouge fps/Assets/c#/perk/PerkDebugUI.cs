using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrototypeFPC;

/// <summary>
/// Runtime perk debug panel.
///
/// Flow:
///   1. Press I (toggleKey) → panel appears, cursor unlocks, camera-look pauses.
///   2. Scroll wheel over the perk list to browse; click a perk button.
///   3. Perk list hides → Gun A / Gun B buttons appear with the selected perk's name.
///   4. Click Gun A or Gun B → perk is instantiated on that gun; panel returns to perk list.
///   5. Press I again (or click the dimmer / X) → panel hides, cursor locks, camera-look resumes.
///
/// Setup:
///   - Attach to any GameObject in the scene.
///   - Assign perkManager and fpcDependencies in the Inspector.
///   - Populate perkPrefabs with every perk prefab you want to test.
/// </summary>
public sealed class PerkDebugUI : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("Refs")]
    public PerkManager  perkManager;

    [Tooltip("Assign the FPC Dependencies component — pauses mouse-look while panel is open.")]
    public Dependencies fpcDependencies;

    [Tooltip("Every prefab listed here gets its own button in the scrollable perk list.")]
    public List<GameObject> perkPrefabs = new List<GameObject>();

    [Header("Toggle")]
    [Tooltip("Key that shows / hides the debug panel (default: I).")]
    public KeyCode toggleKey = KeyCode.I;

    // ─── Runtime state ────────────────────────────────────────────────────

    private bool          _isOpen;
    private GameObject    _pendingPrefab;
    private Canvas        _canvas;
    private RectTransform _perkListRoot;
    private RectTransform _gunSelectRoot;
    private Text          _gunSelectTitle;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildUI();
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetOpen(!_isOpen);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Open / Close
    // ─────────────────────────────────────────────────────────────────────

    private void SetOpen(bool open)
    {
        _isOpen = open;
        _canvas.gameObject.SetActive(open);

        // Pause / resume FPC camera-look
        if (fpcDependencies != null)
            fpcDependencies.isInspecting = open;
        else if (open)
            Debug.LogWarning("[PerkDebugUI] fpcDependencies not assigned — camera will keep rotating while panel is open.");

        // Cursor
        Cursor.lockState = open ? CursorLockMode.None  : CursorLockMode.Locked;
        Cursor.visible   = open;

        if (open) ShowPerkList();
    }

    // ─────────────────────────────────────────────────────────────────────
    // State transitions
    // ─────────────────────────────────────────────────────────────────────

    private void ShowPerkList()
    {
        _pendingPrefab = null;
        _perkListRoot.gameObject.SetActive(true);
        _gunSelectRoot.gameObject.SetActive(false);
    }

    private void ShowGunSelect(GameObject prefab)
    {
        _pendingPrefab = prefab;
        _perkListRoot.gameObject.SetActive(false);
        _gunSelectRoot.gameObject.SetActive(true);
        if (_gunSelectTitle != null)
            _gunSelectTitle.text = $"Equip  \"{prefab.name}\"  to:";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Callbacks
    // ─────────────────────────────────────────────────────────────────────

    private void OnGunSelected(int gunIndex)
    {
        if (_pendingPrefab == null || perkManager == null)
        {
            ShowPerkList();
            return;
        }

        perkManager.RefreshAll(force: true);

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.root == null)
        {
            Debug.LogError($"[PerkDebugUI] GunRefs.root is null for gun {gunIndex}. Check PerkManager assignments.");
            ShowPerkList();
            return;
        }

        var inst = perkManager.InstantiatePerkToGun(_pendingPrefab, gunIndex, gunRefs.root.transform);
        if (inst == null)
            Debug.LogError($"[PerkDebugUI] InstantiatePerkToGun failed — '{_pendingPrefab.name}' → Gun{(gunIndex == 0 ? 'A' : 'B')}.");
        else
            Debug.Log($"[PerkDebugUI] '{_pendingPrefab.name}' → Gun{(gunIndex == 0 ? 'A' : 'B')}.");

        ShowPerkList();
    }

    // ─────────────────────────────────────────────────────────────────────
    // UI construction
    // ─────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas (screen-space overlay, survives scene loads) ───────────
        var cgo = new GameObject("PerkDebugCanvas");
        DontDestroyOnLoad(cgo);

        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 99;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();

        // ── Dimmer — click outside panel to close ─────────────────────────
        var dim = NewRT("Dimmer", cgo.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => SetOpen(false));

        // ── Main panel — centred 960 × 720 ───────────────────────────────
        var panel = NewRT("MainPanel", cgo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta        = new Vector2(960f, 720f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.97f);

        // ── Header bar — 64 px ────────────────────────────────────────────
        var header = NewRT("Header", panel);
        header.anchorMin        = new Vector2(0f, 1f);
        header.anchorMax        = new Vector2(1f, 1f);
        header.pivot            = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta        = new Vector2(0f, 64f);
        header.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);

        // Header title
        var titleRT = NewRT("Title", header);
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(24f, 0f); titleRT.offsetMax = new Vector2(-70f, 0f);
        var titleTxt = titleRT.gameObject.AddComponent<Text>();
        titleTxt.text      = "PERK  DEBUG  PANEL";
        titleTxt.font      = GetFont();
        titleTxt.fontSize  = 22;
        titleTxt.color     = new Color(0.82f, 0.82f, 0.82f, 1f);
        titleTxt.alignment = TextAnchor.MiddleLeft;

        // [X] close button
        var xRT = NewRT("CloseBtn", header);
        xRT.anchorMin = new Vector2(1f, 0f); xRT.anchorMax = new Vector2(1f, 1f);
        xRT.pivot     = new Vector2(1f, 0.5f);
        xRT.anchoredPosition = Vector2.zero;
        xRT.sizeDelta        = new Vector2(64f, 0f);
        xRT.gameObject.AddComponent<Image>().color = new Color(0.50f, 0.08f, 0.08f, 1f);
        var xBtn = xRT.gameObject.AddComponent<Button>();
        var xColors = xBtn.colors;
        xColors.highlightedColor = new Color(0.78f, 0.12f, 0.12f, 1f);
        xColors.pressedColor     = new Color(0.32f, 0.04f, 0.04f, 1f);
        xBtn.colors = xColors;
        xBtn.onClick.AddListener(() => SetOpen(false));
        AddCentredText(xRT, "✕", 22, Color.white);

        // ── Body — fills below header ──────────────────────────────────────
        var body = NewRT("Body", panel);
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(0f,  0f);
        body.offsetMax = new Vector2(0f, -64f);

        // ── Perk list panel (scroll view) ──────────────────────────────────
        _perkListRoot = NewRT("PerkListPanel", body);
        Stretch(_perkListRoot);
        BuildScrollView(_perkListRoot);

        // ── Gun select panel ───────────────────────────────────────────────
        _gunSelectRoot = NewRT("GunSelectPanel", body);
        Stretch(_gunSelectRoot);
        BuildGunSelectPanel(_gunSelectRoot);
    }

    // ── Scroll view ───────────────────────────────────────────────────────

    private void BuildScrollView(RectTransform parent)
    {
        // Inset padding inside the body
        var area = NewRT("ScrollArea", parent);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(40f, 20f);
        area.offsetMax = new Vector2(-40f, -20f);

        var sr = area.gameObject.AddComponent<ScrollRect>();
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 40f;
        sr.movementType      = ScrollRect.MovementType.Clamped;
        sr.inertia           = true;
        sr.decelerationRate  = 0.135f;

        // Viewport (clip mask)
        var viewport = NewRT("Viewport", area);
        Stretch(viewport);
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f); // invisible but required by Mask
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = viewport;

        // Content (grows with buttons)
        var content = NewRT("Content", viewport);
        content.anchorMin        = new Vector2(0f, 1f);
        content.anchorMax        = new Vector2(1f, 1f);
        content.pivot            = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta        = Vector2.zero;
        sr.content = content;

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 10f;
        vlg.padding               = new RectOffset(0, 0, 4, 4);
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        // Perk buttons (one per prefab)
        foreach (var prefab in perkPrefabs)
        {
            if (prefab == null) continue;
            var cap = prefab;
            var btn = SpawnButton(content, prefab.name,
                                  new Color(0.15f, 0.17f, 0.20f, 1f),
                                  height: 68f, fontSize: 17,
                                  anchor: TextAnchor.MiddleLeft, leftPad: 28f);
            btn.onClick.AddListener(() => ShowGunSelect(cap));
        }
    }

    // ── Gun select panel ──────────────────────────────────────────────────

    private void BuildGunSelectPanel(RectTransform parent)
    {
        // Centred column
        var col = NewRT("Column", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
        col.anchoredPosition = Vector2.zero;
        col.sizeDelta        = new Vector2(560f, 0f);

        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 20f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        col.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        // "Equip X to:" label
        var titleRT = NewRT("EquipTitle", col);
        titleRT.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        _gunSelectTitle = titleRT.gameObject.AddComponent<Text>();
        _gunSelectTitle.font      = GetFont();
        _gunSelectTitle.fontSize  = 19;
        _gunSelectTitle.color     = new Color(0.82f, 0.82f, 0.82f, 1f);
        _gunSelectTitle.alignment = TextAnchor.MiddleCenter;
        _gunSelectTitle.text      = "Equip to:";

        var btnA = SpawnButton(col, "Gun  A",
                               new Color(0.08f, 0.36f, 0.12f, 1f),
                               height: 82f, fontSize: 24,
                               anchor: TextAnchor.MiddleCenter, leftPad: 0f);
        btnA.onClick.AddListener(() => OnGunSelected(0));

        var btnB = SpawnButton(col, "Gun  B",
                               new Color(0.08f, 0.12f, 0.38f, 1f),
                               height: 82f, fontSize: 24,
                               anchor: TextAnchor.MiddleCenter, leftPad: 0f);
        btnB.onClick.AddListener(() => OnGunSelected(1));

        var btnBack = SpawnButton(col, "← Back",
                                  new Color(0.22f, 0.08f, 0.08f, 1f),
                                  height: 56f, fontSize: 16,
                                  anchor: TextAnchor.MiddleCenter, leftPad: 0f);
        btnBack.onClick.AddListener(ShowPerkList);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

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

    /// <summary>Creates a button managed by a parent VerticalLayoutGroup.</summary>
    private static Button SpawnButton(RectTransform parent, string label, Color bg,
                                      float height, int fontSize,
                                      TextAnchor anchor, float leftPad)
    {
        var rt = NewRT(label, parent);
        rt.gameObject.AddComponent<Image>().color = bg;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        var btn = rt.gameObject.AddComponent<Button>();
        var c   = btn.colors;
        c.normalColor      = bg;
        c.highlightedColor = Color.Lerp(bg, Color.white, 0.22f);
        c.pressedColor     = Color.Lerp(bg, Color.black, 0.28f);
        c.selectedColor    = bg;
        btn.colors = c;

        var lblRT = NewRT("Label", rt);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(leftPad, 3f);
        lblRT.offsetMax = new Vector2(-8f, -3f);

        var txt = lblRT.gameObject.AddComponent<Text>();
        txt.text      = label;
        txt.font      = GetFont();
        txt.fontSize  = fontSize;
        txt.color     = Color.white;
        txt.alignment = anchor;

        return btn;
    }

    private static void AddCentredText(RectTransform parent, string text, int size, Color color)
    {
        var rt = NewRT("Label", parent);
        Stretch(rt);
        var txt = rt.gameObject.AddComponent<Text>();
        txt.text      = text;
        txt.font      = GetFont();
        txt.fontSize  = size;
        txt.color     = color;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private static Font _cachedFont;
    private static Font GetFont()
    {
        if (_cachedFont == null)
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _cachedFont;
    }
}
