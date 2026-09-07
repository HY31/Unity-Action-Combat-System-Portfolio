using UnityEngine;

/// <summary>
/// 강습전의 남은 시간을 디지털 숫자와 오른쪽에 남는 주황 게이지로 표시한다.
/// 기존 HUD 갱신 뒤에 실행해 프리팹의 최종 시각 상태를 전투 원본 값과 맞춘다.
/// </summary>
[DefaultExecutionOrder(11000)]
[DisallowMultipleComponent]
public sealed class AssaultTimerV1Presenter : MonoBehaviour
{
    [SerializeField] private AssaultBattleController battleController;
    [SerializeField] private AssaultTimerDigitsGraphic timerDigits;
    [SerializeField] private AssaultTimerGaugeGraphic remainingTimeGauge;

    public void Configure(
        AssaultTimerDigitsGraphic digits,
        AssaultTimerGaugeGraphic gauge)
    {
        timerDigits = digits;
        remainingTimeGauge = gauge;
        Refresh();
    }

    private void OnEnable()
    {
        ResolveController();
        Refresh();
    }

    private void LateUpdate()
    {
        ResolveController();
        Refresh();
    }

    private void ResolveController()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();
    }

    private void Refresh()
    {
        float remaining = battleController != null
            ? Mathf.Max(0f, battleController.RemainingTime)
            : 0f;

        if (timerDigits != null)
            timerDigits.Value = FormatTime(remaining);

        float normalized = 0f;
        if (battleController != null)
        {
            float duration = Mathf.Max(0.01f, battleController.BattleDuration);
            normalized = Mathf.Clamp01(remaining / duration);
        }

        if (remainingTimeGauge != null)
            remainingTimeGauge.Value = normalized;
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int hours = Mathf.Clamp(totalSeconds / 3600, 0, 99);
        int minutes = totalSeconds / 60 % 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{remainingSeconds:00}";
    }
}
