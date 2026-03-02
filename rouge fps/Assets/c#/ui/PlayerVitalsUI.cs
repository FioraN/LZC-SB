using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays player HP and Armor on the HUD.
///
/// HP:    filled Image (fillAmount = hp/maxHp)  +  TMP text "hp/maxHp"
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

    [Tooltip("TMP text showing 'hp / maxHp' (e.g. 100/100).")]
    public TMP_Text hpText;

    [Header("Armor UI")]
    [Tooltip("Filled Image for Armor bar. fillAmount = armor / 100, capped at 1.")]
    public Image armorFill;

    [Tooltip("TMP text showing current armor value.")]
    public TMP_Text armorText;

    [Tooltip("Icon shown when armor > 0, hidden when armor == 0.")]
    public GameObject armorIcon;

    // ─────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (vitals == null) return;
        vitals.OnHpChanged    += RefreshHp;
        vitals.OnArmorChanged += RefreshArmor;

        // Sync immediately
        RefreshHp(vitals.hp, vitals.maxHp);
        RefreshArmor(vitals.armor, vitals.maxArmor);
    }

    private void OnDisable()
    {
        if (vitals == null) return;
        vitals.OnHpChanged    -= RefreshHp;
        vitals.OnArmorChanged -= RefreshArmor;
    }

    // ─────────────────────────────────────────────────────────────────────

    private void RefreshHp(int current, int max)
    {
        if (hpFill != null)
            hpFill.fillAmount = (max > 0) ? (float)current / max : 0f;

        if (hpText != null)
            hpText.text = $"{current}/{max}";
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
