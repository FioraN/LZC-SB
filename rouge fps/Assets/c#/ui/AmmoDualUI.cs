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
    [Tooltip("How many digits to display (e.g. 3 -> 020 / 100).")]
    public int digitCount = 3;

    [Tooltip("Color applied to leading zeros - adjust the alpha to control transparency.")]
    public Color leadingZeroColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("Fixed character width in em for TMP (0 = disabled). Prevents text jittering when digits change.")]
    public float monoSpaceEm = 0.6f;

    [Tooltip("斜杠两侧的空格数量（0 表示紧贴斜杠）。")]
    [Min(0)] public int slashPaddingSpaces = 1;

    [Header("Update")]
    public bool updateEveryFrame = true;

    private GunAmmo _pAmmo;
    private GunAmmo _sAmmo;

    // 缓存颜色对应的十六进制字符串，避免每帧重复转换
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
        // 运行时改了颜色时，刷新缓存
        if (leadingZeroColor != _prevLeadingColor)
            CacheLeadingHex();

        if (!updateEveryFrame) return;
        RefreshAll();
    }

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

    private void CacheLeadingHex()
    {
        _prevLeadingColor = leadingZeroColor;
        _leadingHex = ColorUtility.ToHtmlStringRGBA(leadingZeroColor);
    }

    /// <summary>
    /// 按固定位数格式化数字，并将前导 0 着色。
    /// </summary>
    private string FormatNumber(int value)
    {
        if (value < 0) return "--";

        int digits = Mathf.Max(digitCount, 1);
        string padded = value.ToString("D" + digits);

        string raw = value.ToString();
        int leadingCount = padded.Length - raw.Length;

        if (leadingCount <= 0)
            return padded;

        return $"<color=#{_leadingHex}>{padded.Substring(0, leadingCount)}</color>{padded.Substring(leadingCount)}";
    }

    /// <summary>
    /// TMP 等宽包裹，避免数字跳动。
    /// </summary>
    private string WrapMono(string inner)
    {
        if (monoSpaceEm > 0f)
            return $"<mspace={monoSpaceEm}em>{inner}</mspace>";
        return inner;
    }

    private string GetSlashSeparator()
    {
        int pad = Mathf.Max(0, slashPaddingSpaces);
        string spaces = new string(' ', pad);
        return $"{spaces}/{spaces}";
    }

    private void SetPrimaryText(int mag, int reserve)
    {
        string sep = GetSlashSeparator();
        string s = (mag < 0)
            ? $"--{sep}--"
            : $"{FormatNumber(mag)}{sep}{FormatNumber(reserve)}";

        if (primaryTMP != null) primaryTMP.text = WrapMono(s);
        if (primaryText != null) primaryText.text = s;
    }

    private void SetSecondaryText(int mag, int reserve)
    {
        string sep = GetSlashSeparator();
        string s = (mag < 0)
            ? $"--{sep}--"
            : $"{FormatNumber(mag)}{sep}{FormatNumber(reserve)}";

        if (secondaryTMP != null) secondaryTMP.text = WrapMono(s);
        if (secondaryText != null) secondaryText.text = s;
    }
}
