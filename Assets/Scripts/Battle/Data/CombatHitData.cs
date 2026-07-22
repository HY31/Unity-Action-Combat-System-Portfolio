using UnityEngine;

public struct CombatHitData
{
    // 공격 시작 시 정적 HitPayload와 실제 공격자를 결합해 충돌 순간까지 전달한다.
    public PlayerController attacker;
    public float damageMultiplier;
    public float impactMultiplier;
    public CombatElement resolvedElement;
    public float anomalyBuildUp;
    public bool canTriggerChainSkill;
}
