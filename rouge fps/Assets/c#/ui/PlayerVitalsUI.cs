using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays player HP and Armor on the HUD.
///
/// HP:    filled Image (fillAmount = hp/maxHp)  +  TMP text
/// Armor: filled Image (fillAmount = armor/100, capped at 1)  +  TMP text showing armor value
///        + icon that hides when armor == 0
/// </summary>
public class PlayerVitalsUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The PlayerVitals component on the player.")]
    public PlayerVitals vitals;

    [Header("HP UI")]
    [Tooltip("Filled Image for HP bar. fillAmount = hp / maxHp.")]
    public Image hpFill;

    [Tooltip("TMP text showing HP. Supports rich text styling for max HP.")]
    public TMP_Text hpText;

    [Header("HP Text Style (Max HP)")]
    [Tooltip("max HP 显示字号（例如：18）。")]
    [Min(1)] public int maxHpFontSize = 18;

    [Tooltip("max HP 文本透明度（0~1，值越小越透明）。")]
    [Range(0f, 1f)] public float maxHpAlpha = 0.6f;

    [Header("Armor UI")]
    [Tooltip("Filled Image for Armor bar. fillAmount = armor / 100, capped at 1.")]
    public Image armorFill;

    [Tooltip("TMP text showing current armor value.")]
    public TMP_Text armorText;

    [Tooltip("Icon shown when armor > 0, hidden when armor == 0.")]
    public GameObject armorIcon;

    private void OnEnable()
    {
        if (vitals == null) return;

        if (hpText != null)
            hpText.richText = true;

        vitals.OnHpChanged += RefreshHp;
        vitals.OnArmorChanged += RefreshArmor;

        // 立即同步一次 UI
        RefreshHp(vitals.hp, vitals.maxHp);
        RefreshArmor(vitals.armor, vitals.maxArmor);
    }

    private void OnDisable()
    {
        if (vitals == null) return;
        vitals.OnHpChanged -= RefreshHp;
        vitals.OnArmorChanged -= RefreshArmor;
    }

    private void RefreshHp(int current, int max)
    {
        if (hpFill != null)
            hpFill.fillAmount = (max > 0) ? (float)current / max : 0f;

        if (hpText != null)
        {
            // 使用 color 标签而不是 alpha 标签，避免个别版本下 alpha 标签被当普通文本显示
            Color c = hpText.color;
            c.a = Mathf.Clamp01(maxHpAlpha);
            string colorHex = ColorUtility.ToHtmlStringRGBA(c);
            hpText.text = $"{current}<size={maxHpFontSize}><color=#{colorHex}>/{max}</color></size>";
        }
    }

    private void RefreshArmor(int current, int max)
    {
        if (armorFill != null)
            armorFill.fillAmount = Mathf.Clamp01(current / 100f);

        bool hasArmor = current > 0;

        if (armorText != null)
            armorText.gameObject.SetActive(hasArmor);

        if (armorIcon != null)
            armorIcon.SetActive(hasArmor);

        if (hasArmor && armorText != null)
            armorText.text = current.ToString();
    }
}
