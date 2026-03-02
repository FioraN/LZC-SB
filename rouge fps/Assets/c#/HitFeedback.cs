using System.Collections;
using UnityEngine;

public class HitFeedbackUI : MonoBehaviour
{
    public static HitFeedbackUI Instance { get; private set; }

    [Header("命中 UI（CanvasGroup）")]
    [Tooltip("普通部位命中时显示的 UI。")]
    public CanvasGroup normalHitUI;

    [Tooltip("爆头命中时显示的 UI。")]
    public CanvasGroup headshotHitUI;

    [Tooltip("怪物死亡时显示的 UI。")]
    public CanvasGroup killUI;

    [Header("Fade")]
    [Min(0.01f)] public float fadeDuration = 0.35f;

    private Coroutine _normalFadeCo;
    private Coroutine _headshotFadeCo;
    private Coroutine _killFadeCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        SetAlphaInstant(normalHitUI, 0f);
        SetAlphaInstant(headshotHitUI, 0f);
        SetAlphaInstant(killUI, 0f);
    }

    private void OnEnable()
    {
        CombatEventHub.OnKill += HandleKill;
    }

    private void OnDisable()
    {
        CombatEventHub.OnKill -= HandleKill;
    }

    /// <summary>
    /// 命中反馈：爆头显示 headshotHitUI，其他部位显示 normalHitUI。
    /// </summary>
    public void ShowHit(bool isHeadshot)
    {
        if (isHeadshot)
            PlayEffect(headshotHitUI, ref _headshotFadeCo);
        else
            PlayEffect(normalHitUI, ref _normalFadeCo);
    }

    /// <summary>
    /// 击杀反馈：显示 killUI。
    /// </summary>
    public void ShowKill()
    {
        PlayEffect(killUI, ref _killFadeCo);
    }

    private void HandleKill(CombatEventHub.KillEvent e)
    {
        // 只要有击杀事件就触发击杀 UI
        ShowKill();
    }

    private void PlayEffect(CanvasGroup group, ref Coroutine handle)
    {
        if (group == null) return;

        if (handle != null)
            StopCoroutine(handle);

        handle = StartCoroutine(FadeRoutine(group));
    }

    private IEnumerator FadeRoutine(CanvasGroup group)
    {
        SetAlphaInstant(group, 1f);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeDuration);
            SetAlphaInstant(group, a);
            yield return null;
        }

        SetAlphaInstant(group, 0f);
    }

    private static void SetAlphaInstant(CanvasGroup group, float a)
    {
        if (group == null) return;
        group.alpha = a;
    }
}
