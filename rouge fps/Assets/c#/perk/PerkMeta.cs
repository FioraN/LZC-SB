using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class PerkConditionGroup
{
    [Tooltip("同一组内是 OR 关系：拥有其中任意一个 ID 即满足该组。")]
    public List<string> anyOfPerkIds = new();
}

public sealed class PerkMeta : MonoBehaviour
{
    [Header("身份信息")]
    [Tooltip("唯一ID，用于前置与互斥检测。如果为空，默认使用 GameObject 名称。")]
    public string perkId = "";

    [Header("阶级")]
    [Range(1, 2)]
    public int perkTier = 1;

    [Header("前置条件（组间 AND，组内 OR）")]
    [Tooltip("每一组是一个条件；组内任意一个 ID 满足即可；所有组都满足才通过。")]
    public List<PerkConditionGroup> requiredPerkGroups = new();

    [Header("互斥条件（组间 AND，组内 OR）")]
    [Tooltip("每一组是一个互斥条件；组内任意一个 ID 命中即该组命中；所有组都命中时判定互斥。")]
    public List<PerkConditionGroup> mutuallyExclusivePerkGroups = new();

    [Header("文本信息")]
    [Tooltip("风味文字（用于展示氛围或世界观描述）。")]
    [TextArea(2, 4)]
    public string flavorText = "";

    [Tooltip("功能描述文字（用于 UI 展示效果说明）。")]
    [TextArea(2, 6)]
    public string description = "";

    /// <summary>
    /// 获取最终生效 ID。
    /// 若 perkId 为空，则默认使用 GameObject 名称。
    /// </summary>
    public string EffectiveId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(perkId))
                return perkId;

            return gameObject.name;
        }
    }
}
