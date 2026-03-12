using UnityEngine;

/// <summary>
/// Spawns XP orbs when this monster receives a kill event.
/// </summary>
public sealed class MonsterExperienceDrop : MonoBehaviour
{
    [Header("Drop")]
    public ExperienceOrb experienceOrbPrefab;
    [Min(1)] public int totalExperience = 1;
    [Min(1)] public int orbCount = 1;
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    [Min(0f)] public float scatterRadius = 0.6f;

    private bool _dropped;

    private void OnEnable()
    {
        CombatEventHub.OnKill += HandleKill;
    }

    private void OnDisable()
    {
        CombatEventHub.OnKill -= HandleKill;
    }

    private void HandleKill(CombatEventHub.KillEvent e)
    {
        if (_dropped || experienceOrbPrefab == null || e.target == null)
            return;

        Transform targetTransform = e.target.transform;
        bool matchesSelf = targetTransform == transform || targetTransform.IsChildOf(transform) || transform.IsChildOf(targetTransform);
        if (!matchesSelf)
            return;

        _dropped = true;
        SpawnOrbs();
    }

    private void SpawnOrbs()
    {
        int safeOrbCount = Mathf.Max(1, orbCount);
        int safeTotal = Mathf.Max(1, totalExperience);
        Vector3 origin = transform.position + spawnOffset;

        for (int i = 0; i < safeOrbCount; i++)
        {
            Vector2 circle = Random.insideUnitCircle * scatterRadius;
            Vector3 position = origin + new Vector3(circle.x, 0f, circle.y);
            ExperienceOrb orb = Instantiate(experienceOrbPrefab, position, Quaternion.identity);
            orb.experienceValue = GetOrbValue(i, safeOrbCount, safeTotal);
        }
    }

    private static int GetOrbValue(int index, int count, int total)
    {
        int baseValue = total / count;
        int remainder = total % count;
        return baseValue + (index < remainder ? 1 : 0);
    }
}
