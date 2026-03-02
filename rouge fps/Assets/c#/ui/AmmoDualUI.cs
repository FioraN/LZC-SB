using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoDualUI : MonoBehaviour
{
    [Header("Refs")]
    public CameraGunDual dual;
    public CameraGunChannel primaryChannel;
    public CameraGunChannel secondaryChannel;

    [Header("Primary UI")]
    public TMP_Text primaryTMP;
    public Text primaryText;

    [Header("Secondary UI")]
    public TMP_Text secondaryTMP;
    public Text secondaryText;

    [Header("Format")]
    [Tooltip("How many digits to display (e.g. 3 → 020 / 100).")]
    public int digitCount = 3;

    [Tooltip("Color applied to leading zeros — adjust the alpha to control transparency.")]
    public Color leadingZeroColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("Fixed character width in em for TMP (0 = disabled). Prevents text jittering when digits change.")]
    public float monoSpaceEm = 0.6f;

    [Header("Update")]
    public bool updateEveryFrame = true;

    private GunAmmo _pAmmo;
    private GunAmmo _sAmmo;

    // Cached hex string; rebuilt whenever the colour changes in the Inspector.
    private Color _prevLeadingColor;
    private string _leadingHex;

    private void Awake()
    {
        CacheLeadingHex();
        TryAutoWire();
        CacheAmmoRefs();
        RefreshAll();
    }

    private void OnEnable()
    {
        TryAutoWire();
        CacheAmmoRefs();
        HookEvents(true);
        RefreshAll();
    }

    private void OnDisable()
    {
        HookEvents(false);
    }

    private void Update()
    {
        // Rebuild hex cache if colour was tweaked at runtime.
        if (leadingZeroColor != _prevLeadingColor)
            CacheLeadingHex();

        if (!updateEveryFrame) return;
        RefreshAll();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Auto-wiring & event hooks (unchanged)
    // ─────────────────────────────────────────────────────────────────────

    private void TryAutoWire()
    {
        DualGunResolver.TryResolve(ref dual, ref primaryChannel, ref secondaryChannel);
    }

    private void CacheAmmoRefs()
    {
        _pAmmo = (primaryChannel != null) ? primaryChannel.ammo : null;
        _sAmmo = (secondaryChannel != null) ? secondaryChannel.ammo : null;
    }

    private void HookEvents(bool hook)
    {
        if (_pAmmo != null)
        {
            if (hook) _pAmmo.OnAmmoChanged += OnPrimaryAmmoChanged;
            else _pAmmo.OnAmmoChanged -= OnPrimaryAmmoChanged;
        }

        if (_sAmmo != null)
        {
            if (hook) _sAmmo.OnAmmoChanged += OnSecondaryAmmoChanged;
            else _sAmmo.OnAmmoChanged -= OnSecondaryAmmoChanged;
        }
    }

    private void OnPrimaryAmmoChanged(int mag, int reserve) => SetPrimaryText(mag, reserve);
    private void OnSecondaryAmmoChanged(int mag, int reserve) => SetSecondaryText(mag, reserve);

    private void RefreshAll()
    {
        TryAutoWire();

        var oldP = _pAmmo;
        var oldS = _sAmmo;

        CacheAmmoRefs();

        if (oldP != _pAmmo || oldS != _sAmmo)
        {
            if (oldP != null) oldP.OnAmmoChanged -= OnPrimaryAmmoChanged;
            if (oldS != null) oldS.OnAmmoChanged -= OnSecondaryAmmoChanged;

            if (_pAmmo != null) _pAmmo.OnAmmoChanged += OnPrimaryAmmoChanged;
            if (_sAmmo != null) _sAmmo.OnAmmoChanged += OnSecondaryAmmoChanged;
        }

        if (_pAmmo != null) SetPrimaryText(_pAmmo.ammoInMag, _pAmmo.ammoReserve);
        else SetPrimaryText(-1, -1);

        if (_sAmmo != null) SetSecondaryText(_sAmmo.ammoInMag, _sAmmo.ammoReserve);
        else SetSecondaryText(-1, -1);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Formatting
    // ─────────────────────────────────────────────────────────────────────

    private void CacheLeadingHex()
    {
        _prevLeadingColor = leadingZeroColor;
        _leadingHex = ColorUtility.ToHtmlStringRGBA(leadingZeroColor);
    }

    /// <summary>
    /// Formats an integer to a fixed number of digits.
    /// Leading zeros are wrapped in a rich-text colour tag so they appear semi-transparent.
    /// Examples (digitCount = 3):
    ///   5   → "<color=#…>00</color>5"
    ///   20  → "<color=#…>0</color>20"
    ///   100 → "100"
    /// </summary>
    private string FormatNumber(int value)
    {
        if (value < 0) return "--";

        int digits = Mathf.Max(digitCount, 1);
        string padded = value.ToString("D" + digits);

        // How many digits does the raw number actually need?
        string raw = value.ToString();
        int leadingCount = padded.Length - raw.Length;

        if (leadingCount <= 0)
            return padded;

        // Wrap leading zeros in <color> tag
        return $"<color=#{_leadingHex}>{padded.Substring(0, leadingCount)}</color>{padded.Substring(leadingCount)}";
    }

    /// <summary>Wraps the final display string in mspace tags for TMP so every character occupies equal width.</summary>
    private string WrapMono(string inner)
    {
        if (monoSpaceEm > 0f)
            return $"<mspace={monoSpaceEm}em>{inner}</mspace>";
        return inner;
    }

    private void SetPrimaryText(int mag, int reserve)
    {
        string s = (mag < 0)
            ? "-- / --"
            : $"{FormatNumber(mag)} / {FormatNumber(reserve)}";

        if (primaryTMP  != null) primaryTMP.text  = WrapMono(s);
        if (primaryText != null) primaryText.text = s;   // legacy Text 不支持 mspace
    }

    private void SetSecondaryText(int mag, int reserve)
    {
        string s = (mag < 0)
            ? "-- / --"
            : $"{FormatNumber(mag)} / {FormatNumber(reserve)}";

        if (secondaryTMP  != null) secondaryTMP.text  = WrapMono(s);
        if (secondaryText != null) secondaryText.text = s;
    }
}
