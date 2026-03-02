using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrototypeFPC;

public sealed class PerkRerollUI : MonoBehaviour
{
    public enum TargetGunMode
    {
        GunA = 0,
        GunB = 1,
        AskOnPick = 2
    }

    [Header("引用")]
    public PerkManager perkManager;
    public Dependencies fpcDependencies;

    [Tooltip("允许进入刷新池的 Perk 预制体列表。")]
    public List<GameObject> perkPrefabs = new List<GameObject>();

    [Header("刷新配置")]
    public KeyCode rerollKey = KeyCode.O;
    [Min(1)] public int candidateCount = 3;
    public TargetGunMode targetGunMode = TargetGunMode.AskOnPick;
    public bool closeAfterPick = true;

    [Header("UI 模式")]
    [Tooltip("勾选后使用你自己在 Inspector 绑定的 UI。未勾选时使用 Builder 自动创建 UI。")]
    public bool useCustomUI = true;
    public PerkRerollUIBuilder uiBuilder;

    [Header("自定义 UI 绑定")]
    public GameObject customUIRoot;
    public RectTransform customCandidateContent;
    public Button customCandidateButtonTemplate;
    public Text customEmptyMessage;

    [Header("自定义 UI - 控件")]
    public Button customRefreshButton;
    public Button customCloseButton;

    [Header("自定义 UI - 选枪面板")]
    public GameObject customGunSelectRoot;
    public Text customGunSelectTitle;
    public Button customGunAButton;
    public Button customGunBButton;
    public Button customBackButton;

    [Header("字体")]
    [Tooltip("如果绑定，会覆盖动态文本字体。")]
    public Font customFont;

    private bool _isOpen;
    private readonly List<GameObject> _currentCandidates = new List<GameObject>();
    private readonly List<GameObject> _spawnedCandidateButtons = new List<GameObject>();

    private GameObject _uiRoot;
    private RectTransform _candidateContent;
    private Button _candidateButtonTemplate;
    private Text _emptyMessage;

    private GameObject _candidateListRoot;
    private GameObject _gunSelectRoot;
    private Text _gunSelectTitle;

    private GameObject _pendingPrefab;

    private void Awake()
    {
        bool ok = useCustomUI ? BindCustomUI() : BindBuilderUI();
        if (!ok)
        {
            Debug.LogError("[PerkRerollUI] UI 绑定失败，请检查自定义引用或 Builder 组件。");
            enabled = false;
            return;
        }

        SetOpen(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(rerollKey)) return;

        if (_isOpen)
        {
            SetOpen(false);
        }
        else
        {
            RollCandidates();
            SetOpen(true);
            ShowCandidateList();
        }
    }

    // 绑定用户自定义 UI
    private bool BindCustomUI()
    {
        if (customUIRoot == null || customCandidateContent == null || customCandidateButtonTemplate == null)
            return false;

        _uiRoot = customUIRoot;
        _candidateContent = customCandidateContent;
        _candidateButtonTemplate = customCandidateButtonTemplate;
        _emptyMessage = customEmptyMessage;

        _candidateListRoot = customCandidateContent.gameObject;
        _gunSelectRoot = customGunSelectRoot;
        _gunSelectTitle = customGunSelectTitle;

        RebindButton(customRefreshButton, OnRefreshClicked);
        RebindButton(customCloseButton, () => SetOpen(false));
        RebindButton(customGunAButton, () => OnGunSelected(0));
        RebindButton(customGunBButton, () => OnGunSelected(1));
        RebindButton(customBackButton, ShowCandidateList);

        if (_candidateButtonTemplate.gameObject.activeSelf)
            _candidateButtonTemplate.gameObject.SetActive(false);

        return true;
    }

    // 绑定自动构建 UI
    private bool BindBuilderUI()
    {
        if (uiBuilder == null) uiBuilder = GetComponent<PerkRerollUIBuilder>();
        if (uiBuilder == null) return false;

        var built = uiBuilder.Build(customFont);
        if (built == null || built.root == null || built.candidateContent == null || built.candidateButtonTemplate == null)
            return false;

        _uiRoot = built.root;
        _candidateContent = built.candidateContent;
        _candidateButtonTemplate = built.candidateButtonTemplate;
        _emptyMessage = built.emptyMessage;

        _candidateListRoot = built.candidateListRoot;
        _gunSelectRoot = built.gunSelectRoot;
        _gunSelectTitle = built.gunSelectTitle;

        RebindButton(built.refreshButton, OnRefreshClicked);
        RebindButton(built.closeButton, () => SetOpen(false));
        RebindButton(built.gunAButton, () => OnGunSelected(0));
        RebindButton(built.gunBButton, () => OnGunSelected(1));
        RebindButton(built.backButton, ShowCandidateList);

        if (_candidateButtonTemplate.gameObject.activeSelf)
            _candidateButtonTemplate.gameObject.SetActive(false);

        return true;
    }

    private static void RebindButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    private void SetOpen(bool open)
    {
        _isOpen = open;
        if (_uiRoot != null) _uiRoot.SetActive(open);

        if (fpcDependencies != null)
            fpcDependencies.isInspecting = open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    private void OnRefreshClicked()
    {
        RollCandidates();
        ShowCandidateList();
    }

    // 核心：根据前置/互斥/已拥有规则，构建可选池并随机抽取 X 个
    private void RollCandidates()
    {
        _currentCandidates.Clear();

        if (perkManager == null)
        {
            Debug.LogError("[PerkRerollUI] perkManager is null.");
            return;
        }

        perkManager.RefreshAll(force: true);

        List<GameObject> pool = new List<GameObject>();
        for (int i = 0; i < perkPrefabs.Count; i++)
        {
            var prefab = perkPrefabs[i];
            if (prefab == null) continue;

            bool selectable = targetGunMode == TargetGunMode.AskOnPick
                ? (IsSelectableForGun(prefab, 0) || IsSelectableForGun(prefab, 1))
                : IsSelectableForGun(prefab, (int)targetGunMode);

            if (selectable) pool.Add(prefab);
        }

        int pickCount = Mathf.Min(candidateCount, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int r = UnityEngine.Random.Range(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
            _currentCandidates.Add(pool[i]);
        }
    }

    private bool IsSelectableForGun(GameObject perkPrefab, int gunIndex)
    {
        if (perkPrefab == null || perkManager == null) return false;

        var logic = GetPerkLogicType(perkPrefab);
        if (logic == null) return false;

        string id = GetPerkId(perkPrefab, logic);
        if (string.IsNullOrWhiteSpace(id)) return false;

        if (perkManager.HasPerk(id, gunIndex)) return false;
        if (!perkManager.PrerequisitesMet(perkPrefab, gunIndex)) return false;
        if (perkManager.HasMutualExclusionConflict(perkPrefab, gunIndex)) return false;

        return true;
    }

    private void ShowCandidateList()
    {
        _pendingPrefab = null;

        if (_candidateListRoot != null) _candidateListRoot.SetActive(true);
        if (_gunSelectRoot != null) _gunSelectRoot.SetActive(false);

        if (_candidateContent == null || _candidateButtonTemplate == null) return;

        ClearSpawnedCandidateButtons();

        bool noCandidate = _currentCandidates.Count == 0;
        if (_emptyMessage != null)
        {
            _emptyMessage.gameObject.SetActive(noCandidate);
            if (noCandidate)
            {
                _emptyMessage.text = "No perk can be rolled with current prerequisites/exclusions.";
                ApplyFont(_emptyMessage);
            }
        }

        if (noCandidate) return;

        for (int i = 0; i < _currentCandidates.Count; i++)
        {
            var prefab = _currentCandidates[i];
            if (prefab == null) continue;

            string label = BuildPerkLabel(prefab);
            var capture = prefab;

            Button btn = CreateCandidateButton(label);
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPerkPicked(capture));
            _spawnedCandidateButtons.Add(btn.gameObject);
        }
    }

    private Button CreateCandidateButton(string label)
    {
        if (_candidateContent == null || _candidateButtonTemplate == null) return null;

        var go = Instantiate(_candidateButtonTemplate.gameObject, _candidateContent);
        go.SetActive(true);

        var btn = go.GetComponent<Button>();
        if (btn == null) return null;

        var txt = go.GetComponentInChildren<Text>(true);
        if (txt != null)
        {
            txt.text = label;
            ApplyFont(txt);
        }

        return btn;
    }

    private void ClearSpawnedCandidateButtons()
    {
        for (int i = 0; i < _spawnedCandidateButtons.Count; i++)
        {
            var go = _spawnedCandidateButtons[i];
            if (go != null) Destroy(go);
        }
        _spawnedCandidateButtons.Clear();
    }

    private void OnPerkPicked(GameObject prefab)
    {
        if (prefab == null) return;

        if (targetGunMode == TargetGunMode.AskOnPick)
        {
            _pendingPrefab = prefab;
            if (_candidateListRoot != null) _candidateListRoot.SetActive(false);
            if (_gunSelectRoot != null) _gunSelectRoot.SetActive(true);

            if (_gunSelectTitle != null)
            {
                _gunSelectTitle.text = $"Equip \"{prefab.name}\" to:";
                ApplyFont(_gunSelectTitle);
            }
            return;
        }

        TryGivePerk(prefab, (int)targetGunMode);
    }

    private void OnGunSelected(int gunIndex)
    {
        if (_pendingPrefab == null)
        {
            ShowCandidateList();
            return;
        }

        TryGivePerk(_pendingPrefab, gunIndex);
    }

    // 给玩家选中的枪发放 Perk
    private void TryGivePerk(GameObject prefab, int gunIndex)
    {
        if (perkManager == null || prefab == null)
        {
            ShowCandidateList();
            return;
        }

        perkManager.RefreshAll(force: true);

        if (!IsSelectableForGun(prefab, gunIndex))
        {
            Debug.LogWarning($"[PerkRerollUI] Perk '{prefab.name}' is no longer selectable for gun {gunIndex}.");
            RollCandidates();
            ShowCandidateList();
            return;
        }

        var gunRefs = perkManager.GetGun(gunIndex);
        if (gunRefs == null || gunRefs.root == null)
        {
            Debug.LogError($"[PerkRerollUI] GunRefs.root is null for gun {gunIndex}.");
            ShowCandidateList();
            return;
        }

        var inst = perkManager.InstantiatePerkToGun(prefab, gunIndex, gunRefs.root.transform);
        if (inst == null)
        {
            Debug.LogError($"[PerkRerollUI] InstantiatePerkToGun failed for '{prefab.name}'.");
            RollCandidates();
            ShowCandidateList();
            return;
        }

        Debug.Log($"[PerkRerollUI] Granted '{prefab.name}' to Gun{(gunIndex == 0 ? "A" : "B")}.");

        if (closeAfterPick)
        {
            SetOpen(false);
        }
        else
        {
            RollCandidates();
            ShowCandidateList();
        }
    }

    private static string BuildPerkLabel(GameObject prefab)
    {
        if (prefab == null) return "(null)";

        var meta = prefab.GetComponent<PerkMeta>();
        if (meta == null) return prefab.name;

        string id = string.IsNullOrWhiteSpace(meta.EffectiveId) ? "N/A" : meta.EffectiveId;
        string desc = string.IsNullOrWhiteSpace(meta.description) ? "" : $"\n{meta.description}";
        return $"[{id}] {prefab.name}{desc}";
    }

    private static Type GetPerkLogicType(GameObject perkPrefab)
    {
        var list = perkPrefab.GetComponents<MonoBehaviour>();
        for (int i = 0; i < list.Length; i++)
        {
            var mb = list[i];
            if (mb == null || mb is PerkMeta) continue;
            return mb.GetType();
        }
        return null;
    }

    private static string GetPerkId(GameObject perkPrefab, Type logicType)
    {
        var meta = perkPrefab.GetComponent<PerkMeta>();
        if (meta != null && !string.IsNullOrWhiteSpace(meta.EffectiveId)) return meta.EffectiveId;
        if (logicType != null) return logicType.Name;
        return perkPrefab != null ? perkPrefab.name : "";
    }

    private void ApplyFont(params Text[] texts)
    {
        if (customFont == null || texts == null) return;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null) texts[i].font = customFont;
        }
    }
}





