using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the perk card prefab and wire pre-positioned TMP texts in the Inspector.
/// </summary>
public sealed class PerkCardUI : MonoBehaviour
{
    [Header("Card Image")]
    [Tooltip("Optional perk icon image. Leave empty to hide icon updates.")]
    public Image iconImage;

    [Header("Texts")]
    [Tooltip("TMP used to display the perk name.")]
    public TMP_Text perkNameTMP;

    [Tooltip("TMP used to display the perk description.")]
    public TMP_Text descriptionTMP;

    [Header("Interaction")]
    [Tooltip("Button used to select this card.")]
    public Button selectButton;

    private Image[] _cachedImages;
    private Color[] _cachedImageColors;

    private void Awake()
    {
        CacheImageColors();
    }

    private void CacheImageColors()
    {
        if (_cachedImages != null) return;

        _cachedImages = GetComponentsInChildren<Image>(true);
        _cachedImageColors = new Color[_cachedImages.Length];

        for (int i = 0; i < _cachedImages.Length; i++)
            _cachedImageColors[i] = _cachedImages[i] != null ? _cachedImages[i].color : Color.white;
    }

    public void Populate(GameObject perkPrefab)
    {
        if (perkPrefab == null) return;

        var meta = perkPrefab.GetComponent<PerkMeta>();

        string perkName = meta != null && !string.IsNullOrWhiteSpace(meta.EffectiveDisplayName)
            ? meta.EffectiveDisplayName
            : perkPrefab.name;

        string desc = meta != null ? meta.description : "";
        Sprite icon = meta != null ? meta.icon : null;

        if (perkNameTMP != null)
            perkNameTMP.text = perkName;

        if (descriptionTMP != null)
            descriptionTMP.text = desc;

        if (iconImage != null)
        {
            if (icon != null)
                iconImage.sprite = icon;

            iconImage.enabled = true;
        }
    }

    public void SetSelectableVisual(bool selectable)
    {
        CacheImageColors();

        for (int i = 0; i < _cachedImages.Length; i++)
        {
            var image = _cachedImages[i];
            if (image == null) continue;

            var originalColor = _cachedImageColors[i];
            image.color = selectable ? originalColor : ToGray(originalColor);
        }

        if (selectButton != null)
            selectButton.interactable = selectable;
    }

    private static Color ToGray(Color color)
    {
        float gray = color.grayscale;
        return new Color(gray, gray, gray, color.a * 0.75f);
    }
}
