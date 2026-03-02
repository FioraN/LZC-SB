using UnityEngine;
using UnityEngine.UI;

public sealed class PerkRerollUIBuilder : MonoBehaviour
{
    [System.Serializable]
    public sealed class BuiltUI
    {
        public GameObject root;

        public GameObject candidateListRoot;
        public RectTransform candidateContent;
        public Button candidateButtonTemplate;
        public Text emptyMessage;

        public GameObject gunSelectRoot;
        public Text gunSelectTitle;

        public Button refreshButton;
        public Button closeButton;
        public Button gunAButton;
        public Button gunBButton;
        public Button backButton;
    }

    [Header("自动构建配置")]
    public string canvasName = "PerkRerollCanvas";
    public int sortingOrder = 100;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("自动样式")]
    public Vector2 panelSize = new Vector2(980f, 740f);
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    public Color panelColor = new Color(0.08f, 0.08f, 0.10f, 0.98f);
    public Color headerColor = new Color(0.13f, 0.13f, 0.16f, 1f);

    private static Font _cachedFont;

    // 构建一套可直接给 PerkRerollUI 使用的界面引用
    public BuiltUI Build(Font overrideFont = null)
    {
        var ui = new BuiltUI();

        var root = new GameObject(canvasName);
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        ui.root = root;

        var dim = NewRT("Dimmer", root.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = dimColor;

        var panel = NewRT("MainPanel", root.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = panelSize;
        panel.gameObject.AddComponent<Image>().color = panelColor;

        var header = NewRT("Header", panel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 64f);
        header.gameObject.AddComponent<Image>().color = headerColor;

        var titleRT = NewRT("Title", header);
        Stretch(titleRT);
        titleRT.offsetMin = new Vector2(24f, 0f);
        titleRT.offsetMax = new Vector2(-72f, 0f);
        var title = titleRT.gameObject.AddComponent<Text>();
        title.text = "PERK REROLL";
        title.font = GetFont(overrideFont);
        title.fontSize = 24;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        var closeRT = NewRT("Close", header);
        closeRT.anchorMin = new Vector2(1f, 0f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.sizeDelta = new Vector2(64f, 0f);
        closeRT.gameObject.AddComponent<Image>().color = new Color(0.50f, 0.08f, 0.08f, 1f);
        ui.closeButton = closeRT.gameObject.AddComponent<Button>();
        AddCenteredText(closeRT, "X", 22, Color.white, overrideFont);

        var body = NewRT("Body", panel);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = Vector2.zero;
        body.offsetMax = new Vector2(0f, -64f);

        ui.refreshButton = SpawnButton(body, "Refresh Candidates", new Color(0.18f, 0.30f, 0.55f, 1f), 56f, 18, TextAnchor.MiddleCenter, 0f, overrideFont);
        var rollRT = ui.refreshButton.GetComponent<RectTransform>();
        rollRT.anchorMin = new Vector2(0.5f, 1f);
        rollRT.anchorMax = new Vector2(0.5f, 1f);
        rollRT.pivot = new Vector2(0.5f, 1f);
        rollRT.anchoredPosition = new Vector2(0f, -14f);
        rollRT.sizeDelta = new Vector2(280f, 56f);

        var candidateRoot = NewRT("CandidatePanel", body);
        candidateRoot.anchorMin = Vector2.zero;
        candidateRoot.anchorMax = Vector2.one;
        candidateRoot.offsetMin = new Vector2(30f, 20f);
        candidateRoot.offsetMax = new Vector2(-30f, -86f);

        ui.candidateListRoot = candidateRoot.gameObject;
        BuildScrollView(candidateRoot, out ui.candidateContent);

        ui.emptyMessage = SpawnLabel(ui.candidateContent, "", 52f, 16, overrideFont);
        ui.emptyMessage.alignment = TextAnchor.MiddleCenter;
        ui.emptyMessage.gameObject.SetActive(false);

        ui.candidateButtonTemplate = SpawnButton(ui.candidateContent, "PerkTemplate", new Color(0.15f, 0.17f, 0.20f, 1f), 72f, 17, TextAnchor.MiddleLeft, 18f, overrideFont);
        ui.candidateButtonTemplate.gameObject.SetActive(false);

        var gunSelect = NewRT("GunSelectPanel", body);
        Stretch(gunSelect);
        ui.gunSelectRoot = gunSelect.gameObject;

        BuildGunSelectPanel(gunSelect, out ui.gunSelectTitle, out ui.gunAButton, out ui.gunBButton, out ui.backButton, overrideFont);

        return ui;
    }

    private static void BuildScrollView(RectTransform parent, out RectTransform content)
    {
        var area = NewRT("ScrollArea", parent);
        Stretch(area);

        var sr = area.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 40f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = NewRT("Viewport", area);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = viewport;

        content = NewRT("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        sr.content = content;

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(0, 0, 4, 4);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void BuildGunSelectPanel(
        RectTransform parent,
        out Text title,
        out Button btnA,
        out Button btnB,
        out Button btnBack,
        Font overrideFont)
    {
        var col = NewRT("Column", parent);
        col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
        col.anchoredPosition = Vector2.zero;
        col.sizeDelta = new Vector2(560f, 0f);

        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        col.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleRT = NewRT("EquipTitle", col);
        titleRT.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        title = titleRT.gameObject.AddComponent<Text>();
        title.font = GetFont(overrideFont);
        title.fontSize = 19;
        title.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        title.alignment = TextAnchor.MiddleCenter;
        title.text = "Equip to:";

        btnA = SpawnButton(col, "Gun A", new Color(0.08f, 0.36f, 0.12f, 1f), 82f, 24, TextAnchor.MiddleCenter, 0f, overrideFont);
        btnB = SpawnButton(col, "Gun B", new Color(0.08f, 0.12f, 0.38f, 1f), 82f, 24, TextAnchor.MiddleCenter, 0f, overrideFont);
        btnBack = SpawnButton(col, "Back", new Color(0.22f, 0.08f, 0.08f, 1f), 56f, 16, TextAnchor.MiddleCenter, 0f, overrideFont);
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
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Button SpawnButton(RectTransform parent, string label, Color bg, float height, int fontSize, TextAnchor anchor, float leftPad, Font overrideFont)
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
        lblRT.offsetMin = new Vector2(leftPad, 3f);
        lblRT.offsetMax = new Vector2(-8f, -3f);

        var txt = lblRT.gameObject.AddComponent<Text>();
        txt.text = label;
        txt.font = GetFont(overrideFont);
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.alignment = anchor;

        return btn;
    }

    private static Text SpawnLabel(RectTransform parent, string text, float height, int fontSize, Font overrideFont)
    {
        var rt = NewRT("Label", parent);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

        var txt = rt.gameObject.AddComponent<Text>();
        txt.text = text;
        txt.font = GetFont(overrideFont);
        txt.fontSize = fontSize;
        txt.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        txt.alignment = TextAnchor.MiddleLeft;
        return txt;
    }

    private static void AddCenteredText(RectTransform parent, string text, int size, Color color, Font overrideFont)
    {
        var rt = NewRT("Label", parent);
        Stretch(rt);

        var txt = rt.gameObject.AddComponent<Text>();
        txt.text = text;
        txt.font = GetFont(overrideFont);
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    private static Font GetFont(Font overrideFont)
    {
        if (overrideFont != null) return overrideFont;

        if (_cachedFont == null)
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return _cachedFont;
    }
}
