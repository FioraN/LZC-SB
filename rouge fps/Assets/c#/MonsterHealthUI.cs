using UnityEngine;

public class MonsterHealth : MonoBehaviour, IDamageable, IDamageableEx, IDamageableArmorEx
{
    [Header("Health")]
    [Min(1f)] public float maxHp = 100f;
    [Min(0f)] public float hp = 100f;

    [Header("Hit UI")]
    public bool autoFindHitUI = true;

    private HitFeedbackUI _hitUI;
    private bool _didDie;

    // Last-hit tracking (integrated)
    private CameraGunChannel _lastHitSource;
    private float _lastHitTime;

    public event System.Action<DamageInfo> Damaged;
    public event System.Action Died;

    public CameraGunChannel LastHitSource => _lastHitSource;
    public float LastHitTime => _lastHitTime;

    public bool IsDead => hp <= 0.0001f;

    private void Awake()
    {
        if (maxHp < 1f) maxHp = 1f;
        hp = Mathf.Clamp(hp, 0f, maxHp);

        if (autoFindHitUI)
            TryResolveHitUI();
    }

    private void OnEnable()
    {
        CombatEventHub.OnHit += HandleHitEvent;
    }

    private void OnDisable()
    {
        CombatEventHub.OnHit -= HandleHitEvent;
    }

    private void TryResolveHitUI()
    {
        _hitUI = HitFeedbackUI.Instance;
        if (_hitUI == null)
            _hitUI = FindFirstObjectByType<HitFeedbackUI>();
    }

    private void HandleHitEvent(CombatEventHub.HitEvent e)
    {
        if (IsDead) return;
        if (e.target == null) return;

        // Most of your pipeline sets target to MonsterHealth.gameObject
        if (e.target == gameObject)
        {
            if (e.source != null)
            {
                _lastHitSource = e.source;
                _lastHitTime = Time.time;
            }
            return;
        }

        // Fallback: if someone raised hit with a child object as target
        if (e.target.transform != null && e.target.transform.IsChildOf(transform))
        {
            if (e.source != null)
            {
                _lastHitSource = e.source;
                _lastHitTime = Time.time;
            }
        }
    }

    // Required by IDamageable
    public void TakeDamage(float damage)
    {
        TakeDamage(new DamageInfo
        {
            damage = damage,
            source = null,
            isHeadshot = false,
            hitPoint = transform.position,
            hitCollider = null,
            flags = DamageFlags.SkipHitEvent
        });
    }

    /// <summary>
    /// Direct HP damage path (DOT/burn/environment/etc.)
    /// - Does NOT touch armor
    /// - Does NOT use vulnerable window multiplier
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        if (info.damage <= 0f || IsDead) return;

        hp = Mathf.Max(0f, hp - info.damage);

        Damaged?.Invoke(info);

        if ((info.flags & DamageFlags.SkipHitEvent) == 0 && info.source != null)
        {
            if (_hitUI == null && autoFindHitUI) TryResolveHitUI();
            if (_hitUI != null) _hitUI.ShowHit(info.isHeadshot);
        }

        TryRaiseKill(info.source);
    }

    /// <summary>
    /// OnHit / ApplyHit path:
    /// - Damage hits armor first
    /// - Overflow can hit HP
    /// - Vulnerable window multiplier applies ONLY to overflow-to-HP
    /// </summary>
    public void TakeDamage(DamageInfo info, ArmorHitInfo armorInfo)
    {
        if (info.damage <= 0f || IsDead) return;

        float damage = info.damage;
        float overflowDamage = damage;

        // Armor is only used for hit-based damage
        EnemyArmor armor = GetComponentInParent<EnemyArmor>();

        if (armor != null && armor.HasArmor)
        {
            float armorTaken = armor.DamageArmor(damage);
            overflowDamage = damage - armorTaken;
        }

        if (overflowDamage > 0f)
        {
            float hpDamage = overflowDamage;

            // Only amplify overflow HP damage during vulnerable window
            if (armor != null && armor.InVulnerableWindow)
            {
                hpDamage *= armor.HpDamageMultiplier;
            }

            hp = Mathf.Max(0f, hp - hpDamage);
        }

        Damaged?.Invoke(info);

        if ((info.flags & DamageFlags.SkipHitEvent) == 0 && info.source != null)
        {
            if (_hitUI == null && autoFindHitUI) TryResolveHitUI();
            if (_hitUI != null) _hitUI.ShowHit(info.isHeadshot);
        }

        TryRaiseKill(info.source);
    }

    private void TryRaiseKill(CameraGunChannel source)
    {
        if (_didDie) return;
        if (!IsDead) return;

        _didDie = true;
        Died?.Invoke();

        CombatEventHub.RaiseKill(new CombatEventHub.KillEvent
        {
            source = source,
            target = gameObject,
            time = Time.time
        });
    }
}
