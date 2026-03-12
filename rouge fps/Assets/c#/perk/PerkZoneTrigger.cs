using UnityEngine;

/// <summary>
/// 放在场景里的触发区域。
/// 玩家走进来时打开 perk 选择面板。
///
/// 使用方式：
///   1. 新建一个 GameObject，挂上此脚本。
///   2. 给它添加一个 Collider（BoxCollider / SphereCollider 等），勾选 Is Trigger。
///   3. 在 Inspector 里拖入 selectionUI。
///   4. 确保玩家 GameObject 的 Tag 设置为 playerTag（默认 "Player"）。
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class PerkZoneTrigger : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("场景里的 PerkSelectionUI 脚本所在的 GameObject。")]
    public PerkSelectionUI selectionUI;

    [Header("设置")]
    [Tooltip("玩家 Tag，触碰到此 Tag 的 Collider 才会开启面板。")]
    public string playerTag = "Player";

    [Tooltip("触发后是否禁用此区域（每次进关卡只触发一次）。")]
    public bool oneTimeOnly = true;

    [Tooltip("区域触发过一次后，是否让 GameObject 不可见（可选）。")]
    public bool hideOnUsed = false;

    private bool _used;

    private void Awake()
    {
        // 确保触发器已设置
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[PerkZoneTrigger] '{name}' 的 Collider 未勾选 Is Trigger，已自动设置。");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_used && oneTimeOnly) return;
        if (selectionUI == null) return;

        bool isPlayer = string.IsNullOrWhiteSpace(playerTag)
            || other.CompareTag(playerTag)
            || other.transform.root.CompareTag(playerTag);

        if (!isPlayer) return;

        _used = true;

        selectionUI.Open();

        if (hideOnUsed)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }
    }

    /// <summary>重置触发状态，允许再次触发（如关卡重载时调用）。</summary>
    public void Reset()
    {
        _used = false;

        if (hideOnUsed)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }
}
