using UnityEngine;

public class CameraGunChannel : MonoBehaviour
{
    public enum Role { Primary, Secondary }
    public enum FireMode { Semi, Auto }
    public enum ShotType { Single, Shotgun }
    public enum BallisticsMode { Hitscan, Projectile }

    [Header("Role")]
    public Role role = Role.Primary;

    [Header("Input")]
    public KeyCode fireKey = KeyCode.Mouse0;

    [Header("Refs")]
    public Transform firePoint;
    public GunAmmo ammo;
    public GunRecoil recoil;
    public GunSpread spread;

    [Header("Modes")]
    public FireMode fireMode = FireMode.Auto;
    public ShotType shotType = ShotType.Single;
    public BallisticsMode ballisticsMode = BallisticsMode.Hitscan;

    [Header("Fire")]
    [Min(0.01f)] public float fireRate = 10f;
    [Min(0f)] public float maxRange = 200f;
    [Min(1)] public int pelletsPerShot = 8;

    [Header("Semi Extra Cooldown")]
    [Min(0f)] public float semiFireCooldown = 0.15f;

    [Header("Projectile")]
    public BulletProjectile projectilePrefab;
    [Min(0.01f)] public float bulletSpeed = 80f;
    [Min(0f)] public float bulletGravity = 0f;
    [Min(0.01f)] public float bulletLifetime = 3f;

    [Header("Projectile Damage Falloff（实体子弹专用）")]
    [Min(0f)]
    [Tooltip("实体子弹从多少米开始线性衰减伤害：\n- <=该距离：满伤害\n- 到 maxRange：衰减到 0\n注意：只对 Projectile 生效，Hitscan 仍使用下方曲线。")]
    public float projectileFalloffStartMeters = 0f;

    [Header("Damage")]
    [Min(0f)] public float baseDamage = 10f;

    [Tooltip("仅对 Hitscan 生效（FireHitscan 使用该曲线）。Projectile 已改为使用 projectileFalloffStartMeters 线性衰减。")]
    public AnimationCurve damageFalloffByDistance01 = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Hit")]
    public LayerMask hitMask = ~0;

    // ===================== 新增：准星中心瞄准修正（保留原行为可关闭） =====================
    [Header("Aim (Crosshair Center)")]
    [Tooltip("若开启：开火方向以屏幕中心准星为目标点（即使 firePoint 不在摄像机中心也能对齐）。")]
    public bool aimFromScreenCenter = true;

    [Tooltip("用于计算屏幕中心射线的摄像机；为空则尝试 Camera.main。")]
    public Camera aimCamera;

    [Tooltip("用于屏幕中心射线的遮罩；为空则使用 hitMask。")]
    public LayerMask aimMask = ~0;

    [Tooltip("屏幕中心射线是否忽略 Trigger。通常建议 Ignore。")]
    public QueryTriggerInteraction aimTriggerInteraction = QueryTriggerInteraction.Ignore;

    public System.Action<CameraGunChannel> OnShot;

    private float _nextFireTime;
    private float _nextSemiAllowedTime;

    private CameraGunDual _dual;

    private void Awake()
    {
        if (firePoint == null) firePoint = transform;

        AutoAssignIfSafe();

        _dual = GetComponent<CameraGunDual>();
        if (_dual == null) _dual = FindFirstObjectByType<CameraGunDual>();

        HookAmmoEvents();
    }

    private void OnEnable()
    {
        HookAmmoEvents();
    }

    private void OnDisable()
    {
        UnhookAmmoEvents();
    }

    private void OnDestroy()
    {
        UnhookAmmoEvents();
    }

    private void Update()
    {
        HandleFireInput();
    }

    public bool HasValidSetup()
    {
        return firePoint != null && ammo != null && recoil != null && spread != null;
    }

    public void ApplyInterferenceScaled(float scale)
    {
        if (spread == null) return;
        spread.ApplyInterferenceScaled(scale);
    }

    private void HandleFireInput()
    {
        if (!HasValidSetup()) return;

        // 弹匣换弹不可中断阶段：完全禁止开火
        if (_dual != null && _dual.ShouldBlockFiring())
            return;

        // 是否想开火：交由 Dual 决定，保证半自动能抢占自动
        bool wantsFire = (_dual != null)
            ? _dual.GetWantsFire(this)
            : (!PerkSceneCanvasUI.IsFireBlocked && (fireMode == FireMode.Auto ? Input.GetKey(fireKey) : Input.GetKeyDown(fireKey)));

        if (!wantsFire) return;

        // 半自动：只用 semiFireCooldown 限制，不受 fireRate/_nextFireTime 影响
        if (fireMode == FireMode.Semi && semiFireCooldown > 0f)
        {
            if (Time.time < _nextSemiAllowedTime) return;
        }

        // 遂发填弹进行中：开火会中断遂发填弹（弹匣阶段不可中断）
        if (_dual != null)
            _dual.InterruptPerBulletReloadForFire();

        if (ammo.IsReloading && ammo.IsUninterruptible) return;

        // 自动：受 fireRate 限制
        if (fireMode == FireMode.Auto)
        {
            if (Time.time < _nextFireTime) return;
        }

        if (!ammo.HasAmmoInMag())
        {
            // 自动换弹由 Dual 处理
            return;
        }

        if (!ammo.TryConsumeOne())
            return;

        OnShot?.Invoke(this);

        recoil.Kick();
        spread.OnShotFired();

        // ===================== 关键改动 1：霰弹枪弹丸数量走“最终倍率” =====================
        int shots = (shotType == ShotType.Shotgun) ? GetFinalPelletsPerShot() : 1;
        bool isShotgun = (shotType == ShotType.Shotgun);

        // ===================== 新增：每次开火先解算“准星目标点 + 基向量” =====================
        ResolveAimBasis(out Vector3 baseForward, out Vector3 basisRight, out Vector3 basisUp);

        for (int i = 0; i < shots; i++)
        {
            Vector3 dir = spread.GetDirection(
                baseForward,
                basisRight,
                basisUp,
                isShotgun
            );

            if (ballisticsMode == BallisticsMode.Hitscan)
                FireHitscan(dir);
            else
                FireProjectile(dir);
        }

        // 自动：冷却按最终 fireRate 计算（GunStatContext 会回写）
        if (fireMode == FireMode.Auto)
            _nextFireTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));

        CombatEventHub.RaiseFire(new CombatEventHub.FireEvent
        {
            source = this,
            pellets = shots,
            isProjectile = (ballisticsMode == BallisticsMode.Projectile),
            time = Time.time
        });

        // 半自动：只受 semiFireCooldown 限制
        if (fireMode == FireMode.Semi && semiFireCooldown > 0f)
            _nextSemiAllowedTime = Time.time + semiFireCooldown;
    }

    /// <summary>
    /// 获取本次开火的“最终弹丸数”
    /// </summary>
    public int GetFinalPelletsPerShot()
    {
        int basePellets = Mathf.Max(1, pelletsPerShot);

        float mul = PelletCountMultiplierRegistry.GetFinalMultiplier(this);

        int finalPellets = Mathf.FloorToInt(basePellets * mul + 0.0001f);
        return Mathf.Max(1, finalPellets);
    }

    // ===================== 新增：准星对齐核心 =====================

    private void ResolveAimBasis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        // 默认保持你原逻辑：用 firePoint 的局部轴
        forward = firePoint.forward;
        right = firePoint.right;
        up = firePoint.up;

        if (!aimFromScreenCenter) return;

        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return;

        LayerMask mask = aimMask.value != 0 ? aimMask : hitMask;

        Ray aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint;
        if (Physics.Raycast(aimRay, out RaycastHit aimHit, maxRange, mask, aimTriggerInteraction))
            targetPoint = aimHit.point;
        else
            targetPoint = aimRay.origin + aimRay.direction * maxRange;

        Vector3 toTarget = targetPoint - firePoint.position;
        if (toTarget.sqrMagnitude < 0.000001f) return;

        forward = toTarget.normalized;

        // 霰弹/散布基向量用摄像机轴更稳定：围绕准星方向散布
        right = cam.transform.right;
        up = cam.transform.up;

        // 保险：如果 forward 接近 up 导致 spread 算法可能退化，可在这里微调，但一般不会发生
    }

    private void FireHitscan(Vector3 dir)
    {
        Ray ray = new Ray(firePoint.position, dir);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            float mr = maxRange;
            float t01 = mr <= 0.0001f ? 1f : Mathf.Clamp01(hit.distance / mr);
            float mult = Mathf.Max(0f, damageFalloffByDistance01.Evaluate(t01));
            float finalDamage = baseDamage * mult;

            var armorPayload = GetComponentInChildren<BulletArmorPayload>(true);
            var statusPayload = GetComponentInChildren<BulletStatusPayload>(true);

            DamageResolver.ApplyHit(
                baseInfo: new DamageInfo
                {
                    source = this,
                    damage = finalDamage
                },
                hitCol: hit.collider,
                hitPoint: hit.point,
                source: this,
                armorPayload: armorPayload,
                statusPayload: statusPayload,
                showHitUI: true
            );
        }
    }

    private void FireProjectile(Vector3 dir)
    {
        if (projectilePrefab == null) return;

        BulletProjectile p = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(dir));
        p.gameObject.SetActive(true);

        var hitDamage = p.GetComponentInChildren<BulletHitDamage>(true);
        if (hitDamage != null)
        {
            hitDamage.source = this;
            hitDamage.baseDamage = baseDamage;

            hitDamage.Init(baseDamage, this);
        }

        var r = p.GetComponentInChildren<Renderer>();
        if (r != null) r.enabled = true;

        p.Init(new BulletProjectile.Config
        {
            source = this,

            speed = Mathf.Max(0.01f, bulletSpeed),
            gravity = bulletGravity,
            lifetime = bulletLifetime,

            maxRange = maxRange,
            baseDamage = baseDamage,

            falloffStartMeters = projectileFalloffStartMeters,

            hitMask = hitMask
        });
    }

    public void FireBonusPellets(int pellets)
    {
        if (!HasValidSetup()) return;
        if (pellets <= 0) return;

        bool isShotgun = (shotType == ShotType.Shotgun);

        // 额外子弹也要对齐准星
        ResolveAimBasis(out Vector3 baseForward, out Vector3 basisRight, out Vector3 basisUp);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = spread.GetDirection(
                baseForward,
                basisRight,
                basisUp,
                isShotgun
            );

            if (ballisticsMode == BallisticsMode.Hitscan)
                FireHitscan(dir);
            else
                FireProjectile(dir);
        }
    }

    private void AutoAssignIfSafe()
    {
        if (ammo == null)
        {
            var arr = GetComponents<GunAmmo>();
            if (arr.Length == 1) ammo = arr[0];
        }

        if (recoil == null)
        {
            var arr = GetComponents<GunRecoil>();
            if (arr.Length == 1) recoil = arr[0];
        }

        if (spread == null)
        {
            var arr = GetComponents<GunSpread>();
            if (arr.Length == 1) spread = arr[0];
        }
    }

    private void HookAmmoEvents()
    {
        if (ammo == null) return;

        ammo.OnReloadStart -= HandleReloadStart;
        ammo.OnReloadEnd -= HandleReloadEnd;

        ammo.OnReloadStart += HandleReloadStart;
        ammo.OnReloadEnd += HandleReloadEnd;
    }

    private void UnhookAmmoEvents()
    {
        if (ammo == null) return;
        ammo.OnReloadStart -= HandleReloadStart;
        ammo.OnReloadEnd -= HandleReloadEnd;
    }

    private void HandleReloadStart()
    {
        CombatEventHub.RaiseReload(new CombatEventHub.ReloadEvent
        {
            source = this,
            isStart = true,
            time = Time.time
        });
    }

    private void HandleReloadEnd()
    {
        CombatEventHub.RaiseReload(new CombatEventHub.ReloadEvent
        {
            source = this,
            isStart = false,
            time = Time.time
        });
    }

    // ========== FireMode 变化通知 + 立即刷新（新增） ==========

    public System.Action<CameraGunChannel, FireMode, FireMode> OnFireModeChanged;

    public void SetFireMode(FireMode newMode, bool forceRebuildNow = true)
    {
        if (fireMode == newMode) return;

        FireMode prev = fireMode;
        fireMode = newMode;

        OnFireModeChanged?.Invoke(this, prev, newMode);

        var ctx = GetComponent<GunStatContext>();
        if (ctx == null) ctx = GetComponentInParent<GunStatContext>();
        if (ctx != null)
        {
            if (forceRebuildNow) ctx.ForceRebuildNow();
            else ctx.MarkDirty();
        }
    }
}

// Keep ONLY ONE copy of this interface in your whole project.
public interface IDamageable
{
    void TakeDamage(float amount);
}
