using System;

public enum CombatOperationType
{
    PerfectDodge,
    DefensiveAssist,
    ChainSkill
}

public static class CombatOperationEvents
{
    public static event Action<CombatOperationType, PlayerController> Performed;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // 도메인 재로드를 생략한 플레이에서도 이전 전투의 구독자가 남지 않게 초기화한다.
        Performed = null;
    }

    public static void Report(CombatOperationType operationType, PlayerController performer)
    {
        if (performer == null)
            return;

        Performed?.Invoke(operationType, performer);
    }
}
