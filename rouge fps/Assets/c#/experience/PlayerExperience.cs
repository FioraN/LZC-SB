using System;
using UnityEngine;

/// <summary>
/// Tracks player level, current XP progress and available upgrade points.
/// </summary>
public sealed class PlayerExperience : MonoBehaviour
{
    [Header("Level")]
    [Min(1)] public int startLevel = 1;
    [Min(0)] public int startExperience = 0;
    [Min(0)] public int startUpgradePoints = 0;

    [Header("XP Curve")]
    [Min(1)] public int baseExperienceRequired = 5;
    [Min(0)] public int experienceGrowthPerLevel = 3;
    [Min(1)] public int pointsPerLevel = 1;

    [Header("Debug")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentExperience;
    [SerializeField] private int availableUpgradePoints;

    public int CurrentLevel => currentLevel;
    public int CurrentExperience => currentExperience;
    public int AvailableUpgradePoints => availableUpgradePoints;
    public int ExperienceRequiredForNextLevel => GetRequiredExperienceForLevel(currentLevel);
    public float ProgressNormalized => ExperienceRequiredForNextLevel > 0
        ? Mathf.Clamp01((float)currentExperience / ExperienceRequiredForNextLevel)
        : 0f;

    public event Action<int, int, float> OnExperienceChanged;
    public event Action<int> OnLevelChanged;
    public event Action<int> OnUpgradePointsChanged;
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        currentLevel = Mathf.Max(1, startLevel);
        currentExperience = Mathf.Max(0, startExperience);
        availableUpgradePoints = Mathf.Max(0, startUpgradePoints);

        ResolvePendingLevelUps();
        BroadcastAll();
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
            return;

        currentExperience += amount;
        ResolvePendingLevelUps();
        BroadcastExperience();
    }

    public bool TrySpendUpgradePoint(int amount = 1)
    {
        if (amount <= 0 || availableUpgradePoints < amount)
            return false;

        availableUpgradePoints -= amount;
        OnUpgradePointsChanged?.Invoke(availableUpgradePoints);
        return true;
    }

    public void AddUpgradePoints(int amount)
    {
        if (amount <= 0)
            return;

        availableUpgradePoints += amount;
        OnUpgradePointsChanged?.Invoke(availableUpgradePoints);
    }

    public int GetRequiredExperienceForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return Mathf.Max(1, baseExperienceRequired + (safeLevel - 1) * experienceGrowthPerLevel);
    }

    private void ResolvePendingLevelUps()
    {
        while (currentExperience >= ExperienceRequiredForNextLevel)
        {
            currentExperience -= ExperienceRequiredForNextLevel;
            currentLevel++;
            availableUpgradePoints += Mathf.Max(1, pointsPerLevel);

            OnLevelChanged?.Invoke(currentLevel);
            OnUpgradePointsChanged?.Invoke(availableUpgradePoints);
            OnLevelUp?.Invoke(currentLevel);
        }
    }

    private void BroadcastAll()
    {
        OnLevelChanged?.Invoke(currentLevel);
        OnUpgradePointsChanged?.Invoke(availableUpgradePoints);
        BroadcastExperience();
    }

    private void BroadcastExperience()
    {
        OnExperienceChanged?.Invoke(currentExperience, ExperienceRequiredForNextLevel, ProgressNormalized);
    }
}

