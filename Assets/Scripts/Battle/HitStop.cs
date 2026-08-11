using UnityEngine;
using DG.Tweening;

public class HitStop : MonoBehaviour
{
    private static Tween activeHitStop;
    private static Tween activeSlowMotion;
    private static bool isHitStopped;
    private static bool isExternallyPaused;
    private static float presentationTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        activeHitStop = null;
        activeSlowMotion = null;
        isHitStopped = false;
        isExternallyPaused = false;
        presentationTimeScale = 1f;
    }

    public static void DoHitStop(float duration = 0.05f)
    {
        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
            return;

        // 연타 적중 시 이전 복구 예약을 제거하고 마지막 적중을 기준으로 정지 시간을 다시 센다.
        activeHitStop?.Kill(false);
        isHitStopped = true;
        ApplyTimeScale();

        activeHitStop = DOVirtual.DelayedCall(duration, () =>
        {
            isHitStopped = false;
            activeHitStop = null;
            ApplyTimeScale();
        }).SetUpdate(true);
    }

    public static void DoSlowMotion(
        float timeScale,
        float holdDuration,
        float recoveryDuration)
    {
        timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        holdDuration = Mathf.Max(0f, holdDuration);
        recoveryDuration = Mathf.Max(0.01f, recoveryDuration);

        activeSlowMotion?.Kill(false);
        presentationTimeScale = timeScale;
        ApplyTimeScale();

        activeSlowMotion = DOTween.Sequence()
            .AppendInterval(holdDuration)
            .Append(DOTween.To(
                () => presentationTimeScale,
                value =>
                {
                    presentationTimeScale = value;
                    ApplyTimeScale();
                },
                1f,
                recoveryDuration).SetEase(Ease.OutCubic))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                presentationTimeScale = 1f;
                activeSlowMotion = null;
                ApplyTimeScale();
            });
    }

    public static void ClearSlowMotion()
    {
        activeSlowMotion?.Kill(false);
        activeSlowMotion = null;
        presentationTimeScale = 1f;
        ApplyTimeScale();
    }

    public static void SetExternalPause(bool paused)
    {
        isExternallyPaused = paused;
        ApplyTimeScale();
    }

    private static void ApplyTimeScale()
    {
        Time.timeScale = isExternallyPaused || isHitStopped
            ? 0f
            : presentationTimeScale;
    }
}