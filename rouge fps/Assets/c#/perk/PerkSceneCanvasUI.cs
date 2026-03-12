using UnityEngine;

/// <summary>
/// 全局静态标志：Perk 选择 UI 打开时阻止开火。
/// 在打开/关闭 Perk 面板时设置 IsFireBlocked。
/// </summary>
public static class PerkSceneCanvasUI
{
    /// <summary>
    /// 为 true 时，所有开火逻辑（CameraGunChannel、CameraGunDual、Projectile）均会跳过。
    /// 打开 Perk 选择面板时设为 true，关闭时设回 false。
    /// </summary>
    public static bool IsFireBlocked { get; set; } = false;
}
