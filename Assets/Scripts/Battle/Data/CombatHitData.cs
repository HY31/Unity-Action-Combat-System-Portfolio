using UnityEngine;

[System.Serializable]
public struct HitFeedbackData
{
    [Min(0f)] public float hitStopDuration;
    [Min(0f)] public float cameraShakeDuration;
    [Min(0f)] public float cameraShakeStrength;
    [Min(1)] public int cameraShakeVibrato;
    [Min(0.1f)] public float vfxScale;

    public static HitFeedbackData Default => new HitFeedbackData
    {
        hitStopDuration = 0.05f,
        cameraShakeDuration = 0.12f,
        cameraShakeStrength = 0.08f,
        cameraShakeVibrato = 18,
        vfxScale = 1f
    };

    public HitFeedbackData Sanitized()
    {
        hitStopDuration = Mathf.Max(0f, hitStopDuration);
        cameraShakeDuration = Mathf.Max(0f, cameraShakeDuration);
        cameraShakeStrength = Mathf.Max(0f, cameraShakeStrength);
        cameraShakeVibrato = Mathf.Max(1, cameraShakeVibrato);
        vfxScale = Mathf.Max(0.1f, vfxScale);
        return this;
    }
}

public struct CombatHitData
{
    // 공격 시작 시 정적 HitPayload와 실제 공격자를 결합해 충돌 순간까지 전달한다.
    public PlayerController attacker;
    // 적 공격처럼 PlayerController 공격자가 없는 경우 사용하는 확정 피해량이다.
    public float rawDamage;
    public float damageMultiplier;
    public float impactMultiplier;

    // 공격 에셋에서 확정된 경직 누적치를 실제 충돌 시점까지 전달한다.
    public float hitReactionBuildUp;

    public CombatElement resolvedElement;
    public float anomalyBuildUp;
    public bool canTriggerChainSkill;
}
