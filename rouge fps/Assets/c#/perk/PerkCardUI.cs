using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 挂在你制作的 perk 卡片 Prefab 上。
/// 在 Inspector 里把图片、名字文本、描述文本的引用拖进来即可，
/// 位置/大小完全由 Prefab 自己的 RectTransform 控制。
/// </summary>
public sealed class PerkCardUI : MonoBehaviour
{
    [Header("文本位置标记（代码生成模式下使用）")]
    [Tooltip("名字文本的位置：在卡片 Prefab 里新建一个空子节点，调好 RectTransform，拖进来。")]
    public RectTransform nameTextAnchor;

    [Tooltip("描述文本的位置：同上。")]
    public RectTransform descTextAnchor;

    [Header("卡片图片（可选）")]
    [Tooltip("Perk 图标图片组件，留空则不显示图标。")]
    public Image iconImage;

    [Header("文本 — TMP（二选一，优先 TMP）")]
    public TMP_Text perkNameTMP;
    public TMP_Text descriptionTMP;

    [Header("文本 — 传统 Text（若不用 TMP 则用这个）")]
    public Text perkNameText;
    public Text descriptionText;

    [Header("整张卡片的点击按钮")]
    [Tooltip("整个卡片或卡片上某个按钮，点击后触发选择事件。")]
    public Button selectButton;

    /// <summary>此卡片代表的 perk prefab。</summary>
    public GameObject PerkPrefab { get; private set; }

    private void Awake()
    {
        EnsureTextComponents();
    }

    /// <summary>
    /// 如果 anchor 已设置但尚无文本组件，直接在 anchor 节点上生成 Text，
    /// 这样文本就出现在用户摆好的位置上。
    /// </summary>
    private void EnsureTextComponents()
    {
        if (nameTextAnchor != null && perkNameTMP == null && perkNameText == null)
        {
            perkNameText           = nameTextAnchor.gameObject.AddComponent<Text>();
            perkNameText.font      = GetBuiltinFont();
            perkNameText.fontSize  = 20;
            perkNameText.fontStyle = FontStyle.Bold;
            perkNameText.color     = Color.white;
            perkNameText.alignment = TextAnchor.MiddleLeft;
        }

        if (descTextAnchor != null && descriptionTMP == null && descriptionText == null)
        {
            descriptionText           = descTextAnchor.gameObject.AddComponent<Text>();
            descriptionText.font      = GetBuiltinFont();
            descriptionText.fontSize  = 13;
            descriptionText.color     = new Color(0.88f, 0.88f, 0.88f, 1f);
            descriptionText.alignment = TextAnchor.UpperLeft;
        }
    }

    private static Font _builtinFont;
    private static Font GetBuiltinFont()
    {
        if (_builtinFont == null)
            _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _builtinFont;
    }

    /// <summary>
    /// 用 perkPrefab 的 PerkMeta 数据填充卡片显示内容。
    /// </summary>
    public void Populate(GameObject perkPrefab)
    {
        PerkPrefab = perkPrefab;
        if (perkPrefab == null) return;

        var meta = perkPrefab.GetComponent<PerkMeta>();

        string perkName = (meta != null && !string.IsNullOrWhiteSpace(meta.EffectiveId))
            ? meta.EffectiveId
            : perkPrefab.name;

        string desc = meta != null ? meta.description : "";
        Sprite icon = meta != null ? meta.icon : null;

        // 名字
        if (perkNameTMP != null)      perkNameTMP.text = perkName;
        if (perkNameText != null)     perkNameText.text = perkName;

        // 描述
        if (descriptionTMP != null)   descriptionTMP.text = desc;
        if (descriptionText != null)  descriptionText.text = desc;

        // 图标：有 sprite 时显示，没有时隐藏——但不影响背景图（背景图不要绑到此字段）
        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite  = icon;
            }

            iconImage.enabled = true;
        }
    }
}
