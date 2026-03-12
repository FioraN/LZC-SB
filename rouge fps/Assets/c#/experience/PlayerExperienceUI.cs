using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD bridge for the player XP system.
/// Keeps actual progression state separate from displayed state so UI can animate safely.
/// </summary>
public sealed class PlayerExperienceUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerExperience experience;

    [Header("XP Bar")]
    [Tooltip("Filled image for the current level progress.")]
    public Image experienceFill;

    [Tooltip("Optional TMP text for current XP progress, e.g. 3 / 10.")]
    public TMP_Text experienceText;

    [Tooltip("How fast the XP bar fill catches up to the target value.")]
    [Min(0f)] public float experienceFillLerpSpeed = 6f;

    [Tooltip("How fast the bar fills to 100% during a level-up wrap animation.")]
    [Min(0f)] public float levelUpWrapSpeed = 12f;

    [Header("Level")]
    public TMP_Text levelText;
    public string levelFormat = "Lv.{0}";

    [Header("Upgrade Points")]
    [Tooltip("Ordered icon slots. The script computes which slots light up and repositions visible ones evenly.")]
    public Image[] pointStackSlots;

    [Tooltip("Uniform horizontal gap between visible point icons.")]
    [Min(0f)] public float pointIconSpacing = 36f;

    [Tooltip("Starting scale for the point pop animation.")]
    [Min(0f)] public float pointPopStartScale = 0.6f;

    [Tooltip("How fast visible point icons scale back to normal size.")]
    [Min(0f)] public float pointPopSpeed = 10f;

    [Tooltip("When available points are greater than this value, switch to summary mode. Example: x6 + icon.")]
    [Min(1)] public int imageModeMaxPoints = 5;

    public GameObject pointStackRoot;
    public GameObject pointSummaryRoot;
    public TMP_Text pointSummaryText;
    public string pointSummaryFormat = "x{0}";

    [Header("Debug State")]
    [SerializeField] private int actualLevel;
    [SerializeField] private int displayedLevel;
    [SerializeField] private int actualUpgradePoints;
    [SerializeField] private int displayedUpgradePoints;

    private float _targetExperienceFill;
    private int _pendingLevelWraps;
    private bool _isInitialized;
    private Vector2[] _pointSlotBaseAnchors;
    private Vector3[] _pointSlotBaseScales;
    private bool[] _lastPointPattern;
    private readonly HashSet<int> _poppingPointSlots = new HashSet<int>();

    private void Awake()
    {
        CachePointSlotAnchors();
        ApplyPointMode(false);
        ClearPointSlots();
        TryAutoResolveExperience();
        RefreshAllImmediate();
    }

    private void OnEnable()
    {
        CachePointSlotAnchors();
        ApplyPointMode(false);
        ClearPointSlots();
        TryAutoResolveExperience();
        HookEvents(true);
        RefreshAllImmediate();
    }

    private void OnDisable()
    {
        HookEvents(false);
    }

    private void Update()
    {
        UpdateExperienceFillVisual();
        UpdatePointPopVisuals();
    }

    private void TryAutoResolveExperience()
    {
        if (experience != null)
            return;

        experience = FindFirstObjectByType<PlayerExperience>();
        if (experience == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                experience = player.GetComponentInParent<PlayerExperience>();
        }
    }

    private void HookEvents(bool hook)
    {
        if (experience == null)
            return;

        if (hook)
        {
            experience.OnExperienceChanged += HandleExperienceChanged;
            experience.OnLevelChanged += HandleLevelChanged;
            experience.OnUpgradePointsChanged += HandlePointsChanged;
            experience.OnLevelUp += HandleLevelUp;
        }
        else
        {
            experience.OnExperienceChanged -= HandleExperienceChanged;
            experience.OnLevelChanged -= HandleLevelChanged;
            experience.OnUpgradePointsChanged -= HandlePointsChanged;
            experience.OnLevelUp -= HandleLevelUp;
        }
    }

    private void RefreshAllImmediate()
    {
        if (experience == null)
            return;

        _pendingLevelWraps = 0;
        actualLevel = experience.CurrentLevel;
        displayedLevel = actualLevel;
        actualUpgradePoints = experience.AvailableUpgradePoints;
        displayedUpgradePoints = actualUpgradePoints;

        _targetExperienceFill = experience.ProgressNormalized;
        UpdateLevelVisual();
        UpdatePointsVisual(immediate: true);

        if (experienceText != null)
            experienceText.text = $"{Mathf.Max(0, experience.CurrentExperience)} / {Mathf.Max(1, experience.ExperienceRequiredForNextLevel)}";

        if (experienceFill != null)
            experienceFill.fillAmount = _targetExperienceFill;

        _isInitialized = true;
    }

    private void HandleExperienceChanged(int current, int required, float normalized)
    {
        _targetExperienceFill = Mathf.Clamp01(normalized);

        if (experienceText != null)
            experienceText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, required)}";

        if (experienceFill != null && !Application.isPlaying)
            experienceFill.fillAmount = _targetExperienceFill;
    }

    private void HandleLevelChanged(int level)
    {
        actualLevel = Mathf.Max(0, level);

        if (!_isInitialized)
            displayedLevel = actualLevel;
        else if (_pendingLevelWraps <= 0 && displayedLevel > actualLevel)
            displayedLevel = actualLevel;

        UpdateLevelVisual();
    }

    private void HandleLevelUp(int level)
    {
        if (!_isInitialized)
            return;

        _pendingLevelWraps++;
    }

    private void HandlePointsChanged(int points)
    {
        actualUpgradePoints = Mathf.Max(0, points);

        if (!_isInitialized)
            displayedUpgradePoints = actualUpgradePoints;
        else if (_pendingLevelWraps <= 0 && displayedUpgradePoints != actualUpgradePoints)
            displayedUpgradePoints = actualUpgradePoints;
        else if (displayedUpgradePoints > actualUpgradePoints)
            displayedUpgradePoints = actualUpgradePoints;

        UpdatePointsVisual();
    }

    private void UpdateExperienceFillVisual()
    {
        if (experienceFill == null)
            return;

        if (!Application.isPlaying)
        {
            experienceFill.fillAmount = _targetExperienceFill;
            return;
        }

        if (_pendingLevelWraps > 0)
        {
            float speed = levelUpWrapSpeed > 0f ? levelUpWrapSpeed : float.MaxValue;
            experienceFill.fillAmount = Mathf.MoveTowards(experienceFill.fillAmount, 1f, speed * Time.deltaTime);

            if (experienceFill.fillAmount >= 0.9999f)
            {
                _pendingLevelWraps--;
                experienceFill.fillAmount = 0f;
                StepDisplayedProgression();
            }

            return;
        }

        if (experienceFillLerpSpeed <= 0f)
        {
            experienceFill.fillAmount = _targetExperienceFill;
            return;
        }

        experienceFill.fillAmount = Mathf.MoveTowards(
            experienceFill.fillAmount,
            _targetExperienceFill,
            experienceFillLerpSpeed * Time.deltaTime);
    }

    private void StepDisplayedProgression()
    {
        if (displayedLevel < actualLevel)
            displayedLevel++;
        else if (displayedLevel > actualLevel)
            displayedLevel = actualLevel;

        if (displayedUpgradePoints < actualUpgradePoints)
            displayedUpgradePoints++;
        else if (displayedUpgradePoints > actualUpgradePoints)
            displayedUpgradePoints = actualUpgradePoints;

        UpdateLevelVisual();
        UpdatePointsVisual();
    }

    private void UpdateLevelVisual()
    {
        if (levelText != null)
            levelText.text = string.Format(levelFormat, Mathf.Max(0, displayedLevel));
    }

    private void UpdatePointsVisual(bool immediate = false)
    {
        int points = Mathf.Max(0, displayedUpgradePoints);
        bool useSummary = points > Mathf.Max(1, imageModeMaxPoints);
        ApplyPointMode(useSummary);

        if (useSummary)
        {
            if (pointSummaryText != null)
                pointSummaryText.text = string.Format(pointSummaryFormat, points);
            ClearPointSlots();
            RestorePointSlotPositions();
            ResetAllPointScales();
            _lastPointPattern = null;
            _poppingPointSlots.Clear();
            return;
        }

        UpdatePointSlots(points, immediate);
    }

    private void ApplyPointMode(bool useSummary)
    {
        if (pointStackRoot != null)
            pointStackRoot.SetActive(!useSummary);

        if (pointSummaryRoot != null)
            pointSummaryRoot.SetActive(useSummary);

        if (pointSummaryText != null)
            pointSummaryText.gameObject.SetActive(useSummary);
    }

    private void CachePointSlotAnchors()
    {
        if (pointStackSlots == null)
            return;

        if (_pointSlotBaseAnchors != null && _pointSlotBaseAnchors.Length == pointStackSlots.Length)
        {
            if (_pointSlotBaseScales != null && _pointSlotBaseScales.Length == pointStackSlots.Length)
                return;
        }

        _pointSlotBaseAnchors = new Vector2[pointStackSlots.Length];
        _pointSlotBaseScales = new Vector3[pointStackSlots.Length];
        for (int i = 0; i < pointStackSlots.Length; i++)
        {
            RectTransform rect = pointStackSlots[i] != null ? pointStackSlots[i].rectTransform : null;
            _pointSlotBaseAnchors[i] = rect != null ? rect.anchoredPosition : Vector2.zero;
            _pointSlotBaseScales[i] = rect != null ? rect.localScale : Vector3.one;
        }
    }

    private void RestorePointSlotPositions()
    {
        if (pointStackSlots == null || _pointSlotBaseAnchors == null)
            return;

        int count = Mathf.Min(pointStackSlots.Length, _pointSlotBaseAnchors.Length);
        for (int i = 0; i < count; i++)
        {
            if (pointStackSlots[i] != null)
                pointStackSlots[i].rectTransform.anchoredPosition = _pointSlotBaseAnchors[i];
        }
    }

    private void ClearPointSlots()
    {
        if (pointStackSlots == null)
            return;

        for (int i = 0; i < pointStackSlots.Length; i++)
        {
            if (pointStackSlots[i] != null)
                pointStackSlots[i].enabled = false;
        }
    }

    private void UpdatePointSlots(int points, bool immediate)
    {
        if (pointStackSlots == null || pointStackSlots.Length == 0)
            return;

        ClearPointSlots();

        int slotCount = pointStackSlots.Length;
        int clampedPoints = Mathf.Clamp(points, 0, slotCount);
        bool[] pattern = BuildPointPattern(slotCount, clampedPoints);
        ApplyUniformPointSpacing(pattern);
        ApplyPointPopState(pattern, immediate);

        for (int i = 0; i < slotCount; i++)
        {
            if (pointStackSlots[i] != null)
                pointStackSlots[i].enabled = pattern[i];
        }

        _lastPointPattern = (bool[])pattern.Clone();
    }

    private void ApplyUniformPointSpacing(bool[] pattern)
    {
        if (pointStackSlots == null || pattern == null)
            return;

        CachePointSlotAnchors();

        int visibleCount = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i])
                visibleCount++;
        }

        if (visibleCount <= 0)
        {
            RestorePointSlotPositions();
            return;
        }

        float centerOffset = (visibleCount - 1) * 0.5f;
        int visibleIndex = 0;

        for (int i = 0; i < pointStackSlots.Length; i++)
        {
            if (pointStackSlots[i] == null)
                continue;

            RectTransform rect = pointStackSlots[i].rectTransform;
            float y = (_pointSlotBaseAnchors != null && i < _pointSlotBaseAnchors.Length) ? _pointSlotBaseAnchors[i].y : rect.anchoredPosition.y;

            if (i < pattern.Length && pattern[i])
            {
                float x = (visibleIndex - centerOffset) * pointIconSpacing;
                rect.anchoredPosition = new Vector2(x, y);
                visibleIndex++;
            }
            else if (_pointSlotBaseAnchors != null && i < _pointSlotBaseAnchors.Length)
            {
                rect.anchoredPosition = _pointSlotBaseAnchors[i];
            }
        }
    }

    private void ApplyPointPopState(bool[] pattern, bool immediate)
    {
        if (pointStackSlots == null || pattern == null)
            return;

        for (int i = 0; i < pointStackSlots.Length; i++)
        {
            if (pointStackSlots[i] == null)
                continue;

            bool isVisible = i < pattern.Length && pattern[i];
            bool wasVisible = _lastPointPattern != null && i < _lastPointPattern.Length && _lastPointPattern[i];
            RectTransform rect = pointStackSlots[i].rectTransform;

            if (!isVisible)
            {
                rect.localScale = GetPointSlotBaseScale(i);
                _poppingPointSlots.Remove(i);
                continue;
            }

            if (immediate || !Application.isPlaying)
            {
                rect.localScale = GetPointSlotBaseScale(i);
                _poppingPointSlots.Remove(i);
                continue;
            }

            if (!wasVisible)
            {
                float startScale = pointPopStartScale > 0f ? pointPopStartScale : 0.01f;
                rect.localScale = GetPointSlotBaseScale(i) * startScale;
                _poppingPointSlots.Add(i);
            }
            else if (!_poppingPointSlots.Contains(i))
            {
                rect.localScale = GetPointSlotBaseScale(i);
            }
        }
    }

    private void UpdatePointPopVisuals()
    {
        if (pointStackSlots == null || _poppingPointSlots.Count == 0)
            return;

        if (!Application.isPlaying || pointPopSpeed <= 0f)
        {
            ResetPoppingPointScales();
            return;
        }

        List<int> completed = null;
        foreach (int index in _poppingPointSlots)
        {
            if (index < 0 || index >= pointStackSlots.Length || pointStackSlots[index] == null)
                continue;

            RectTransform rect = pointStackSlots[index].rectTransform;
            Vector3 targetScale = GetPointSlotBaseScale(index);
            Vector3 next = Vector3.MoveTowards(rect.localScale, targetScale, pointPopSpeed * Time.deltaTime);
            rect.localScale = next;

            if ((next - targetScale).sqrMagnitude <= 0.0001f)
            {
                rect.localScale = targetScale;
                if (completed == null) completed = new List<int>();
                completed.Add(index);
            }
        }

        if (completed == null)
            return;

        for (int i = 0; i < completed.Count; i++)
            _poppingPointSlots.Remove(completed[i]);
    }

    private void ResetAllPointScales()
    {
        if (pointStackSlots == null)
            return;

        for (int i = 0; i < pointStackSlots.Length; i++)
        {
            if (pointStackSlots[i] != null)
                pointStackSlots[i].rectTransform.localScale = GetPointSlotBaseScale(i);
        }
    }

    private void ResetPoppingPointScales()
    {
        foreach (int index in _poppingPointSlots)
        {
            if (index >= 0 && index < pointStackSlots.Length && pointStackSlots[index] != null)
                pointStackSlots[index].rectTransform.localScale = GetPointSlotBaseScale(index);
        }
        _poppingPointSlots.Clear();
    }

    private Vector3 GetPointSlotBaseScale(int index)
    {
        if (_pointSlotBaseScales != null && index >= 0 && index < _pointSlotBaseScales.Length)
            return _pointSlotBaseScales[index];

        return Vector3.one;
    }

    private static bool[] BuildPointPattern(int slotCount, int points)
    {
        bool[] pattern = new bool[Mathf.Max(0, slotCount)];
        if (slotCount <= 0 || points <= 0)
            return pattern;

        int clampedPoints = Mathf.Clamp(points, 0, slotCount);
        int center = slotCount / 2;

        if ((slotCount & 1) == 1)
        {
            if ((clampedPoints & 1) == 1)
                pattern[center] = true;

            int pairCount = clampedPoints / 2;
            for (int offset = 1; offset <= pairCount; offset++)
            {
                int left = center - offset;
                int right = center + offset;

                if (left >= 0)
                    pattern[left] = true;

                if (right < slotCount)
                    pattern[right] = true;
            }
        }
        else
        {
            int leftCenter = center - 1;
            int rightCenter = center;
            int remaining = clampedPoints;

            for (int offset = 0; remaining > 0; offset++)
            {
                int left = leftCenter - offset;
                int right = rightCenter + offset;

                if (left >= 0 && remaining > 0)
                {
                    pattern[left] = true;
                    remaining--;
                }

                if (right < slotCount && remaining > 0)
                {
                    pattern[right] = true;
                    remaining--;
                }
            }
        }

        return pattern;
    }
}
