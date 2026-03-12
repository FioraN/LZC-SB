using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责刷新当前候选 perk，并根据 PerkMeta 规则判断是否可装备。
/// 将候选刷新逻辑与 PerkSelectionUI 的纯显示逻辑拆开。
/// </summary>
public sealed class PerkSelectionRefresher : MonoBehaviour
{
    [Header("Core References")]
    public PerkManager perkManager;

    [Header("Perk Pool")]
    public List<GameObject> perkPool = new();

    [Min(1)] public int candidateCount = 3;

    [Header("Base Perk Gate")]
    [Min(0)]
    [Tooltip("当已选择的基础 perk 总数小于该值时，非基础 perk 不会进入刷新池。0 表示关闭此限制。")]
    public int requiredBasePerkCountForAdvancedPerks = 0;

    [Min(0)]
    [Tooltip("当已选择的基础 perk 总数达到该值后，基础 perk 不再进入刷新池。0 表示关闭此限制。")]
    public int stopRefreshingBasePerksAtCount = 0;

    private readonly List<GameObject> _currentCandidates = new();

    public IReadOnlyList<GameObject> CurrentCandidates => _currentCandidates;
    public bool HasCachedCandidates => _currentCandidates.Count > 0;

    public IReadOnlyList<GameObject> RefreshCandidates(bool force = false)
    {
        if (!force && HasCachedCandidates)
            return _currentCandidates;

        if (perkManager != null)
            perkManager.RefreshAll(force: true);

        _currentCandidates.Clear();

        var pool = new List<GameObject>();
        foreach (var perk in perkPool)
        {
            if (perk == null) continue;
            if (!CanRefreshByBasePerkGate(perk)) continue;
            if (perkManager != null && !perkManager.HasRemainingSelectableCount(perk)) continue;
            if (perkManager != null && !HasPrerequisiteForAnyGun(perk)) continue;

            if (perk != null)
                pool.Add(perk);
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int takeCount = Mathf.Min(candidateCount, pool.Count);
        for (int i = 0; i < takeCount; i++)
            _currentCandidates.Add(pool[i]);

        return _currentCandidates;
    }

    public void ClearCachedCandidates()
    {
        _currentCandidates.Clear();
    }

    public bool IsPerkSelectableForGun(GameObject perkPrefab, int gunIndex)
    {
        if (perkPrefab == null) return false;
        if (perkManager == null) return true;
        return perkManager.CanEquipPerkToGun(perkPrefab, gunIndex);
    }

    public bool IsPerkSelectableForAnyGun(GameObject perkPrefab)
    {
        return IsPerkSelectableForGun(perkPrefab, 0) || IsPerkSelectableForGun(perkPrefab, 1);
    }

    private bool HasPrerequisiteForAnyGun(GameObject perkPrefab)
    {
        if (perkPrefab == null) return false;
        if (perkManager == null) return true;

        return perkManager.PrerequisitesMet(perkPrefab, 0) || perkManager.PrerequisitesMet(perkPrefab, 1);
    }

    private bool CanRefreshByBasePerkGate(GameObject perkPrefab)
    {
        if (perkPrefab == null) return false;
        if (perkManager == null) return true;

        var meta = perkPrefab.GetComponent<PerkMeta>();
        if (meta == null) return true;

        int ownedBasePerkCount = perkManager.GetOwnedBasePerkCount();

        if (meta.isBasePerk)
        {
            if (stopRefreshingBasePerksAtCount > 0 && ownedBasePerkCount >= stopRefreshingBasePerksAtCount)
                return false;

            return true;
        }

        if (requiredBasePerkCountForAdvancedPerks <= 0) return true;

        return ownedBasePerkCount >= requiredBasePerkCountForAdvancedPerks;
    }
}
