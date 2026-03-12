using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrototypeFPC;

/// <summary>
/// Perk 选择面板控制器。
/// 挂在场景中的任意 GameObject 上，Awake 时会自动创建 Canvas 和完整 UI，
/// 不需要手动在层级里搭建面板。
///
/// 流程：
///   1. PerkZoneTrigger 调用 Open()
///   2. 随机展示 candidateCount 张 perk 卡片
///   3. 玩家点击卡片后，选择装备到 Gun A / Gun B
///   4. 安装 perk，关闭面板，并恢复开火
/// </summary>
public sealed class PerkSelectionUI : MonoBehaviour
{
    // 核心引用
    [Header("核心引用")]
    public PerkManager  perkManager;
    public Dependencies fpcDependencies;

    // 卡片配置
    [Header("Card Setup")]
    [Tooltip("Perk card prefab with a PerkCardUI component. Leave empty to build a default card in code.")]
    public PerkCardUI cardPrefab;

    [Tooltip("Background sprite for code-built cards. Leave empty to use a solid color.")]
    public Sprite cardBackgroundSprite;

    [Tooltip("Optional layout template used to copy name/description anchors from an existing card.")]
    public PerkCardUI cardLayoutTemplate;

    // Perk 池
    [Header("Perk Pool")]
    [Tooltip("All perk prefabs that can be randomly offered.")]
    public List<GameObject> perkPool = new List<GameObject>();

    [Min(1)] public int candidateCount = 3;

    [Header("卡片布局")]
    [Tooltip("Center card stays on screen center. Adjust center offset / upper / lower distance here.")]
    public Vector2 centerCardOffset = Vector2.zero;
    public float upperCardDistance = 220f;
    public float lowerCardDistance = 220f;

    // 内部状态
    private bool       _isOpen;
    private GameObject _pendingPrefab;

    private Canvas        _canvas;
    private RectTransform _cardListRoot;
    private RectTransform _gunSelectRoot;
    private Text          _gunSelectTitle;

    private readonly List<PerkCardUI> _spawnedCards = new List<PerkCardUI>();

    // 生命周期
    private void Awake()
    {
        BuildPanel();
        SetOpen(false);
    }

    // 公共 API
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

    // 开关逻辑
    private void SetOpen(bool open)
    {
        _isOpen = open;
        _canvas.gameObject.SetActive(open);

        PerkSceneCanvasUI.IsFireBlocked = open;

        Cursor.lockState = open ? CursorLockMode.None  : CursorLockMode.Locked;
        Cursor.visible   = open;

        if (fpcDependencies != null)
            fpcDependencies.isInspecting = open;

        if (open) ShowCardList();
    }

    // 流程切换
    private void ShowCardList()
    {
        _pendingPrefab = null;
        _cardListRoot.gameObject.SetActive(true);
        _gunSelectRoot.gameObject.SetActive(false);
        SpawnCandidateCards();
    }

    private void ShowGunSelect(GameObject perkPrefab)
    {
        _pendingPrefab = perkPrefab;
        _cardListRoot.gameObject.SetActive(false);
        _gunSelectRoot.gameObject.SetActive(true);

        if (_gunSelectTitle != null)
        {
            var meta = perkPrefab != null ? perkPrefab.GetComponent<PerkMeta>() : null;
            string n = meta != null ? meta.EffectiveId : (perkPrefab != null ? perkPrefab.name : "");
            _gunSelectTitle.text = $"Equip \"{n}\" To:";
        }
    }

    private void OnGunSelected(int gunIndex)
    {
        if (_pendingPrefab == null || perkManager == null)
        {
            ShowCardList();
            return;
        }

        perkManager.RefreshAll(force: true);

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.root == null)
        {
            Debug.LogError($"[PerkSelectionUI] GunRefs.root is null (gunIndex={gunIndex}). Check PerkManager setup.");
            ShowCardList();
            return;
        }

        var inst = perkManager.InstantiatePerkToGun(_pendingPrefab, gunIndex, gunRefs.root.transform);
        if (inst == null)
            Debug.LogWarning($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} failed (prerequisite/conflict/already owned).");
        else
            Debug.Log($"[PerkSelectionUI] '{_pendingPrefab.name}' -> Gun{(gunIndex == 0 ? 'A' : 'B')} success.");

        Close();
    }

    // 卡片生成
    private void SpawnCandidateCards()
    {
        ClearCards();

        // 读取 prefab 的原始尺寸，作为 LayoutElement 的 preferred size 交给 LayoutGroup。
        // 当 childControlHeight/Width = true 时，LayoutGroup 会依赖这个尺寸计算排布结果。
        Vector2 nativeSize = Vector2.zero;
        if (cardPrefab != null)
        {
            var prefabRT = cardPrefab.GetComponent<RectTransform>();
            if (prefabRT != null && prefabRT.sizeDelta.x > 0f && prefabRT.sizeDelta.y > 0f)
                nativeSize = prefabRT.sizeDelta;
        }

        foreach (var perkPrefab in PickCandidates(candidateCount))
        {
            if (perkPrefab == null) continue;

            var cap  = perkPrefab;
            PerkCardUI card;
            if (cardPrefab != null)
            {
                card = Instantiate(cardPrefab, _cardListRoot);
                // 把 prefab 的原始尺寸写入 LayoutElement，避免布局时被错误拉伸。
                if (nativeSize != Vector2.zero)
                {
                    var le = card.GetComponent<LayoutElement>() ?? card.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth  = nativeSize.x;
                    le.preferredHeight = nativeSize.y;
                }
            }
            else
            {
                card = BuildDefaultCard(_cardListRoot, cardBackgroundSprite,
                    cardLayoutTemplate != null ? cardLayoutTemplate.nameTextAnchor : null,
                    cardLayoutTemplate != null ? cardLayoutTemplate.descTextAnchor : null);
            }

            EnsureCardBackgroundVisible(card);
            card.Populate(cap);

            if (card.selectButton != null)
                card.selectButton.onClick.AddListener(() => ShowGunSelect(cap));

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

    private void ClearCards()
    {
        foreach (var c in _spawnedCards)
            if (c != null) Destroy(c.gameObject);
        _spawnedCards.Clear();
    }

    private List<GameObject> PickCandidates(int count)
    {
        var pool = new List<GameObject>();
        foreach (var p in perkPool)
            if (p != null) pool.Add(p);

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, Mathf.Min(count, pool.Count));
    }

    // 默认 perk 卡片，会在没有 prefab 时按代码动态生成。
    // 布局约定：
    // - 背景图覆盖整张卡片
    // - 名称默认放在右上角标题区域
    // - 描述默认放在左下角说明区域
    private static PerkCardUI BuildDefaultCard(
        Transform parent, Sprite bgSprite,
        RectTransform nameAnchor, RectTransform descAnchor)
    {
        // 卡片根节点
        var root = new GameObject("PerkCard");
        root.transform.SetParent(parent, false);
        root.AddComponent<LayoutElement>().preferredHeight = 180f;

        var bg = root.AddComponent<Image>();
        bg.color  = new Color(0.35f, 0.40f, 0.48f, 1f);
        bg.sprite = bgSprite;
        if (bgSprite != null) bg.type = Image.Type.Sliced;

        var btn = root.AddComponent<Button>();
        var bc  = btn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        bc.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        bc.selectedColor    = Color.white;
        btn.colors = bc;

        // Name 文本
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(root.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        if (nameAnchor != null)
            CopyRect(nameAnchor, nameRT);   // 使用模板里定义的位置
        else
        {
            // 默认：右上角标题区
            nameRT.anchorMin        = new Vector2(0.42f, 0.72f);
            nameRT.anchorMax        = new Vector2(1f,    1f);
            nameRT.offsetMin        = new Vector2(10f,   2f);
            nameRT.offsetMax        = new Vector2(-10f, -2f);
        }
        var nameTxt = nameGO.AddComponent<Text>();
        nameTxt.font      = GetFont();
        nameTxt.fontSize  = 20;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.color     = Color.white;
        nameTxt.alignment = nameAnchor != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

        // Description 文本
        var descGO = new GameObject("DescText");
        descGO.transform.SetParent(root.transform, false);
        var descRT = descGO.AddComponent<RectTransform>();
        if (descAnchor != null)
            CopyRect(descAnchor, descRT);   // 使用模板里定义的位置
        else
        {
            // 默认：左下角描述区
            descRT.anchorMin = new Vector2(0f, 0f);
            descRT.anchorMax = new Vector2(1f, 0.44f);
            descRT.offsetMin = new Vector2(12f,  6f);
            descRT.offsetMax = new Vector2(-8f, -6f);
        }
        var descTxt = descGO.AddComponent<Text>();
        descTxt.font      = GetFont();
        descTxt.fontSize  = 13;
        descTxt.color     = new Color(0.88f, 0.88f, 0.88f, 1f);
        descTxt.alignment = TextAnchor.UpperLeft;

        // 组装 PerkCardUI
        var card             = root.AddComponent<PerkCardUI>();
        card.perkNameText    = nameTxt;
        card.descriptionText = descTxt;
        card.selectButton    = btn;

        return card;
    }

    /// <summary>把 src 的 RectTransform 布局属性复制到 dst。</summary>
    private static void CopyRect(RectTransform src, RectTransform dst)
    {
        dst.anchorMin        = src.anchorMin;
        dst.anchorMax        = src.anchorMax;
        dst.pivot            = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta        = src.sizeDelta;
        dst.offsetMin        = src.offsetMin;
        dst.offsetMax        = src.offsetMax;
    }

    // 完整面板构建，整体思路类似 PerkDebugUI.BuildUI
    private void BuildPanel()
    {
        // Canvas
        var cgo = new GameObject("PerkSelectionCanvas");
        DontDestroyOnLoad(cgo);

        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();

        // 半透明遮罩
        var dim = NewRT("Dimmer", cgo.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);

        // 主面板
        var panel = NewRT("MainPanel", cgo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta        = new Vector2(1000f, 740f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 0.97f);

        // Header
        var header = NewRT("Header", panel);
        header.anchorMin        = new Vector2(0f, 1f);
        header.anchorMax        = new Vector2(1f, 1f);
        header.pivot            = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta        = new Vector2(0f, 60f);
        header.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 1f);

        var titleRT = NewRT("Title", header);
        Stretch(titleRT);
        titleRT.offsetMin = new Vector2(24f, 0f);
        titleRT.offsetMax = new Vector2(-70f, 0f);
        var titleTxt = titleRT.gameObject.AddComponent<Text>();
        titleTxt.text      = "Choose A Perk";
        titleTxt.font      = GetFont();
        titleTxt.fontSize  = 22;
        titleTxt.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        titleTxt.alignment = TextAnchor.MiddleLeft;

        var xRT = NewRT("CloseBtn", header);
        xRT.anchorMin        = new Vector2(1f, 0f);
        xRT.anchorMax        = new Vector2(1f, 1f);
        xRT.pivot            = new Vector2(1f, 0.5f);
        xRT.anchoredPosition = Vector2.zero;
        xRT.sizeDelta        = new Vector2(60f, 0f);
        xRT.gameObject.AddComponent<Image>().color = new Color(0.50f, 0.08f, 0.08f, 1f);
        var xBtn = xRT.gameObject.AddComponent<Button>();
        var xc   = xBtn.colors;
        xc.highlightedColor = new Color(0.78f, 0.12f, 0.12f, 1f);
        xc.pressedColor     = new Color(0.30f, 0.04f, 0.04f, 1f);
        xBtn.colors = xc;
        xBtn.onClick.AddListener(Close);
        AddCentredText(xRT, "X", 20, Color.white);

        // Body
        var body = NewRT("Body", panel);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(0f,   0f);
        body.offsetMax = new Vector2(0f, -60f);

        // 卡片列表根节点
        _cardListRoot = NewRT("CardListRoot", cgo.transform);
        Stretch(_cardListRoot);

        // 枪械选择面板
        _gunSelectRoot = NewRT("GunSelectPanel", body);
        Stretch(_gunSelectRoot);
        BuildGunSelectPanel(_gunSelectRoot);
    }

    private void BuildGunSelectPanel(RectTransform parent)
    {
        var col = NewRT("Column", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
        col.anchoredPosition = Vector2.zero;
        col.sizeDelta        = new Vector2(520f, 0f);

        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 20f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        col.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        var titleRT = NewRT("EquipTitle", col);
        titleRT.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        _gunSelectTitle           = titleRT.gameObject.AddComponent<Text>();
        _gunSelectTitle.font      = GetFont();
        _gunSelectTitle.fontSize  = 20;
        _gunSelectTitle.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        _gunSelectTitle.alignment = TextAnchor.MiddleCenter;
        _gunSelectTitle.text      = "Equip To:";

        var btnA = SpawnButton(col, "Gun  A", new Color(0.08f, 0.36f, 0.12f, 1f), 82f, 24);
        btnA.onClick.AddListener(() => OnGunSelected(0));

        var btnB = SpawnButton(col, "Gun  B", new Color(0.08f, 0.12f, 0.38f, 1f), 82f, 24);
        btnB.onClick.AddListener(() => OnGunSelected(1));

        var btnBack = SpawnButton(col, "返回", new Color(0.22f, 0.08f, 0.08f, 1f), 54f, 16);
        btnBack.onClick.AddListener(ShowCardList);
    }

    // UI 工具
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
        var c   = btn.colors;
        c.normalColor      = bg;
        c.highlightedColor = Color.Lerp(bg, Color.white, 0.22f);
        c.pressedColor     = Color.Lerp(bg, Color.black, 0.28f);
        c.selectedColor    = bg;
        btn.colors = c;

        var lblRT = NewRT("Label", rt);
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        var txt = lblRT.gameObject.AddComponent<Text>();
        txt.text      = label;
        txt.font      = GetFont();
        txt.fontSize  = fontSize;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

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

    private static Font _font;
    private static Font GetFont()
    {
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }
}
