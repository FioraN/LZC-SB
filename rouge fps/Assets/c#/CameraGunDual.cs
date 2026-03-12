using System.Collections;
using UnityEngine;

public class CameraGunDual : MonoBehaviour
{
    public enum ReloadPriority
    {
        MagazineFirst,
        PerBulletFirst
    }

    [Header("Refs")]
    public CameraGunChannel primary;
    public CameraGunChannel secondary;

    [Header("Reload")]
    public KeyCode reloadKey = KeyCode.R;

    [Header("Priority")]
    public ReloadPriority reloadPriority = ReloadPriority.MagazineFirst;

    [Header("Allow Simultaneous Fire")]
    [Tooltip("关闭时：后按下的开火键会抢占，打断正在开火的那把枪（独占开火）。")]
    public bool allowSimultaneousFire = true;

    [Header("Auto Reload (Empty -> Release -> Wait)")]
    [Tooltip("自动换弹：空仓 + 两个开火键都松开，并持续松开这么多秒才触发。")]
    [Min(0f)] public float autoReloadDelay = 0.25f;

    [Header("Cross Spread Interference (Scaled)")]
    [Range(0f, 3f)] public float primaryToSecondaryInterference = 0.6f;
    [Range(0f, 3f)] public float secondaryToPrimaryInterference = 0.6f;

    // ===== Runtime Info / API =====
    public bool IsReloading => _reloadCo != null;

    // Channel uses this to block firing during magazine phase.
    public bool ShouldBlockFiring() => _reloadCo != null && _uninterruptiblePhase;

    /// <summary>
    /// Channel asks Dual: "do I want to fire this frame?"
    /// Handles exclusive fire + preemption + semi/auto differences.
    /// </summary>
    public bool GetWantsFire(CameraGunChannel ch)
    {
        if (ch == null) return false;

        CacheInputOncePerFrame();

        if (PerkSceneCanvasUI.IsFireBlocked) return false;

        bool rawWants = (ch.fireMode == CameraGunChannel.FireMode.Auto)
            ? Input.GetKey(ch.fireKey)
            : Input.GetKeyDown(ch.fireKey);

        if (allowSimultaneousFire) return rawWants;

        // Exclusive: only owner may fire
        if (_exclusiveOwner != null && ch != _exclusiveOwner)
            return false;

        return rawWants;
    }

    // Compatibility overload (in case you still have old calls somewhere)
    public bool GetWantsFire(CameraGunChannel ch, bool isAutoMode, KeyCode key)
    {
        if (ch == null) return false;
        // Prefer channel's own settings; keep signature for old code.
        return GetWantsFire(ch);
    }

    /// <summary>
    /// Called by Channel right before actually firing.
    /// If we are in per-bullet stage, any fire should interrupt it.
    /// Magazine stage is never interruptible.
    /// </summary>
    public void InterruptPerBulletReloadForFire()
    {
        if (_reloadCo == null) return;
        if (_uninterruptiblePhase) return;               // magazine stage never interruptible
        if (!_perBulletStageActive) return;              // only interrupt per-bullet stage
        if (_ignoreFireInterruptUntilReleasedOnce) return;

        StopPerBulletReloadNow(_perA, _perB);
    }

    // ===== Internals =====
    private Coroutine _reloadCo;

    // magazine phase gate
    private bool _uninterruptiblePhase;

    // per-bullet stage tracking (so we can interrupt only that stage)
    private bool _perBulletStageActive;
    private bool _perA;
    private bool _perB;

    // manual reload: if R pressed while holding fire, don't instantly interrupt per-bullet until both released once
    private bool _ignoreFireInterruptUntilReleasedOnce;

    // auto-reload gate (empty -> release -> wait)
    private bool _autoReloadPending;
    private float _autoReloadStartTime;

    // exclusive owner logic
    private CameraGunChannel _exclusiveOwner;

    // per-frame input cache
    private int _inputFrame = -1;
    private bool _pHeld, _sHeld, _pDown, _sDown;

    private void Awake()
    {
        AutoFindIfNeeded();
        Hook(true);
    }

    private void OnDestroy()
    {
        Hook(false);
    }

    private void Update()
    {
        CacheInputOncePerFrame();

        if (_ignoreFireInterruptUntilReleasedOnce && !AnyFireHeldRaw())
            _ignoreFireInterruptUntilReleasedOnce = false;

        // Manual reload: no need to wait release
        if (Input.GetKeyDown(reloadKey))
        {
            StartManualReload();
        }

        // Auto reload: empty -> both released -> stay released autoReloadDelay -> reload
        HandleAutoReloadGate();
    }

    private void StartManualReload()
    {
        if (_reloadCo != null) return;
        if (!HasValid()) return;

        if (AnyFireHeldRaw())
            _ignoreFireInterruptUntilReleasedOnce = true;

        _autoReloadPending = false;
        _reloadCo = StartCoroutine(LinkedReloadRoutine(
            reloadA: true,
            reloadB: true,
            applyPriority: true   // manual always follows priority policy
        ));
    }

    private void HandleAutoReloadGate()
    {
        if (_reloadCo != null)
        {
            _autoReloadPending = false;
            return;
        }

        bool needP = NeedsAutoReloadEmpty(primary);
        bool needS = NeedsAutoReloadEmpty(secondary);

        if (!needP && !needS)
        {
            _autoReloadPending = false;
            return;
        }

        // must be fully released
        if (AnyFireHeldRaw())
        {
            _autoReloadPending = false;
            return;
        }

        if (!_autoReloadPending)
        {
            _autoReloadPending = true;
            _autoReloadStartTime = Time.time;
            return;
        }

        if (Time.time - _autoReloadStartTime < autoReloadDelay)
            return;

        // re-check after wait
        needP = NeedsAutoReloadEmpty(primary);
        needS = NeedsAutoReloadEmpty(secondary);

        if (needP || needS)
        {
            // AUTO rules:
            // - Only one empty -> reload only that gun, do NOT apply priority.
            // - Both empty -> linked reload, apply priority.
            if (needP && needS)
            {
                _reloadCo = StartCoroutine(LinkedReloadRoutine(
                    reloadA: true,
                    reloadB: true,
                    applyPriority: true
                ));
            }
            else if (needP)
            {
                _reloadCo = StartCoroutine(LinkedReloadRoutine(
                    reloadA: true,
                    reloadB: false,
                    applyPriority: false
                ));
            }
            else
            {
                _reloadCo = StartCoroutine(LinkedReloadRoutine(
                    reloadA: false,
                    reloadB: true,
                    applyPriority: false
                ));
            }
        }

        _autoReloadPending = false;
    }

    private IEnumerator LinkedReloadRoutine(bool reloadA, bool reloadB, bool applyPriority)
    {
        GunAmmo a = primary.ammo;
        GunAmmo b = secondary.ammo;

        bool aActive = reloadA;
        bool bActive = reloadB;

        bool aMag = aActive && (a.reloadType == GunAmmo.ReloadType.Magazine);
        bool bMag = bActive && (b.reloadType == GunAmmo.ReloadType.Magazine);

        bool aPer = aActive && !aMag;
        bool bPer = bActive && !bMag;

        // If not applying priority (AUTO single-empty case), just do what's needed for active guns:
        // - if active gun is magazine -> magazine phase only
        // - if active gun is per-bullet -> per-bullet phase only
        if (!applyPriority)
        {
            if (aMag || bMag)
            {
                yield return MagazinePhase(a, b, aMag, bMag);
                if (_reloadCo == null) yield break;
            }

            if (aPer || bPer)
            {
                yield return PerBulletPhase(a, b, aPer, bPer);
                if (_reloadCo == null) yield break;
            }

            _reloadCo = null;
            yield break;
        }

        // applyPriority == true (manual or both-empty auto)
        if (reloadPriority == ReloadPriority.MagazineFirst)
        {
            if (aMag || bMag)
            {
                yield return MagazinePhase(a, b, aMag, bMag);
                if (_reloadCo == null) yield break;
            }

            if (aPer || bPer)
            {
                yield return PerBulletPhase(a, b, aPer, bPer);
                if (_reloadCo == null) yield break;
            }

            _reloadCo = null;
            yield break;
        }
        else
        {
            if (aPer || bPer)
            {
                yield return PerBulletPhase(a, b, aPer, bPer);
                if (_reloadCo == null) yield break;
            }

            if (aMag || bMag)
            {
                yield return MagazinePhase(a, b, aMag, bMag);
                if (_reloadCo == null) yield break;
            }

            _reloadCo = null;
            yield break;
        }
    }

    private IEnumerator MagazinePhase(GunAmmo a, GunAmmo b, bool aMag, bool bMag)
    {
        _uninterruptiblePhase = true;

        if (aMag) a.BeginExternalReload(uninterruptible: true);
        if (bMag) b.BeginExternalReload(uninterruptible: true);

        float tA = aMag ? a.reloadTimeMagazine : 0f;
        float tB = bMag ? b.reloadTimeMagazine : 0f;

        float maxT = Mathf.Max(tA, tB);
        float elapsed = 0f;

        bool aDone = !aMag;
        bool bDone = !bMag;

        while (elapsed < maxT)
        {
            elapsed += Time.deltaTime;

            if (aMag && !aDone && elapsed >= tA)
            {
                a.ApplyMagazineReloadNow();
                aDone = true;
            }

            if (bMag && !bDone && elapsed >= tB)
            {
                b.ApplyMagazineReloadNow();
                bDone = true;
            }

            yield return null;
        }

        if (aMag && !aDone) a.ApplyMagazineReloadNow();
        if (bMag && !bDone) b.ApplyMagazineReloadNow();

        _uninterruptiblePhase = false;

        if (aMag) a.EndExternalReload();
        if (bMag) b.EndExternalReload();
    }

    private IEnumerator PerBulletPhase(GunAmmo a, GunAmmo b, bool aPer, bool bPer)
    {
        // track per-bullet stage so firing can interrupt it
        _perBulletStageActive = true;
        _perA = aPer;
        _perB = bPer;

        if (aPer) a.BeginExternalReload(uninterruptible: false);
        if (bPer) b.BeginExternalReload(uninterruptible: false);

        yield return PerBulletStageRoutine(a, b, aPer, bPer);

        if (_reloadCo == null) yield break;

        if (aPer) a.EndExternalReload();
        if (bPer) b.EndExternalReload();

        _perBulletStageActive = false;
        _perA = _perB = false;
    }

    private IEnumerator PerBulletStageRoutine(GunAmmo a, GunAmmo b, bool perA, bool perB)
    {
        float startMax = Mathf.Max(
            perA ? a.reloadStartTime : 0f,
            perB ? b.reloadStartTime : 0f
        );

        yield return WaitPerBulletInterruptible(startMax, perA, perB);
        if (_reloadCo == null) yield break;

        bool turnA = true;

        while ((perA && a.CanInsertOne()) || (perB && b.CanInsertOne()))
        {
            if (CanInterruptPerBullet())
            {
                StopPerBulletReloadNow(perA, perB);
                yield break;
            }

            if (turnA)
            {
                if (perA && a.CanInsertOne())
                {
                    yield return WaitPerBulletInterruptible(a.insertOneTime, perA, perB);
                    if (_reloadCo == null) yield break;
                    a.InsertOneNow();
                }
            }
            else
            {
                if (perB && b.CanInsertOne())
                {
                    yield return WaitPerBulletInterruptible(b.insertOneTime, perA, perB);
                    if (_reloadCo == null) yield break;
                    b.InsertOneNow();
                }
            }

            turnA = !turnA;

            if (!(perA && a.CanInsertOne())) turnA = false;
            if (!(perB && b.CanInsertOne())) turnA = true;

            yield return null;
        }

        float endMax = Mathf.Max(
            perA ? a.reloadEndTime : 0f,
            perB ? b.reloadEndTime : 0f
        );

        yield return WaitPerBulletInterruptible(endMax, perA, perB);
    }

    private IEnumerator WaitPerBulletInterruptible(float seconds, bool perA, bool perB)
    {
        if (seconds <= 0f) yield break;

        float t = 0f;
        while (t < seconds)
        {
            if (CanInterruptPerBullet())
            {
                StopPerBulletReloadNow(perA, perB);
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }
    }

    private bool CanInterruptPerBullet()
    {
        if (_uninterruptiblePhase) return false;
        if (_ignoreFireInterruptUntilReleasedOnce) return false;
        return AnyFireHeldRaw();
    }

    private void StopPerBulletReloadNow(bool perA, bool perB)
    {
        if (_reloadCo != null)
        {
            StopCoroutine(_reloadCo);
            _reloadCo = null;
        }

        if (perA && primary != null && primary.ammo != null && primary.ammo.IsReloading && !primary.ammo.IsUninterruptible)
            primary.ammo.EndExternalReload();

        if (perB && secondary != null && secondary.ammo != null && secondary.ammo.IsReloading && !secondary.ammo.IsUninterruptible)
            secondary.ammo.EndExternalReload();

        _perBulletStageActive = false;
        _perA = _perB = false;
        _uninterruptiblePhase = false;
    }

    // ===== Spread interference hook =====
    private void OnChannelShot(CameraGunChannel ch)
    {
        if (ch == null) return;

        if (ch.role == CameraGunChannel.Role.Primary)
        {
            if (secondary != null && primaryToSecondaryInterference > 0f)
                secondary.ApplyInterferenceScaled(primaryToSecondaryInterference);
        }
        else
        {
            if (primary != null && secondaryToPrimaryInterference > 0f)
                primary.ApplyInterferenceScaled(secondaryToPrimaryInterference);
        }
    }

    private void Hook(bool hook)
    {
        if (primary != null)
        {
            if (hook) primary.OnShot += OnChannelShot;
            else primary.OnShot -= OnChannelShot;
        }

        if (secondary != null)
        {
            if (hook) secondary.OnShot += OnChannelShot;
            else secondary.OnShot -= OnChannelShot;
        }
    }

    // ===== Input caching & exclusive owner =====
    private void CacheInputOncePerFrame()
    {
        if (_inputFrame == Time.frameCount) return;
        _inputFrame = Time.frameCount;

        _pHeld = primary != null && Input.GetKey(primary.fireKey);
        _pDown = primary != null && Input.GetKeyDown(primary.fireKey);

        _sHeld = secondary != null && Input.GetKey(secondary.fireKey);
        _sDown = secondary != null && Input.GetKeyDown(secondary.fireKey);

        UpdateExclusiveOwnerFromCachedInput();
    }

    private void UpdateExclusiveOwnerFromCachedInput()
    {
        if (allowSimultaneousFire)
        {
            _exclusiveOwner = null;
            return;
        }

        if (primary == null || secondary == null) return;

        // During magazine stage, firing is blocked anyway; keep owner stable
        if (ShouldBlockFiring()) return;

        // Preempt: later KeyDown becomes owner (semi can steal auto)
        if (_pDown) _exclusiveOwner = primary;
        if (_sDown) _exclusiveOwner = secondary;

        // If owner released, switch to other if still held
        if (_exclusiveOwner == primary && !_pHeld)
            _exclusiveOwner = _sHeld ? secondary : null;

        if (_exclusiveOwner == secondary && !_sHeld)
            _exclusiveOwner = _pHeld ? primary : null;

        // No owner but someone held -> stable default
        if (_exclusiveOwner == null)
        {
            if (_pHeld && !_sHeld) _exclusiveOwner = primary;
            else if (!_pHeld && _sHeld) _exclusiveOwner = secondary;
            else if (_pHeld && _sHeld) _exclusiveOwner = primary;
        }
    }

    private bool AnyFireHeldRaw() => _pHeld || _sHeld;

    private bool NeedsAutoReloadEmpty(CameraGunChannel ch)
    {
        if (ch == null || ch.ammo == null) return false;
        return ch.ammo.ammoInMag <= 0 && ch.ammo.ammoReserve > 0;
    }

    private bool HasValid()
    {
        return primary != null && secondary != null &&
               primary.ammo != null && secondary.ammo != null;
    }

    private void AutoFindIfNeeded()
    {
        if (primary != null && secondary != null) return;

        var channels = GetComponents<CameraGunChannel>();
        for (int i = 0; i < channels.Length; i++)
        {
            if (channels[i].role == CameraGunChannel.Role.Primary) primary = channels[i];
            if (channels[i].role == CameraGunChannel.Role.Secondary) secondary = channels[i];
        }
    }
}

