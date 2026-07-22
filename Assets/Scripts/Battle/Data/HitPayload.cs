[System.Serializable]
public struct HitPayload
{
    // 공격 종류와 무관하게 에셋에 저장할 수 있는 정적 히트 설정만 모아 둔다.
    public float damageMultiplier;
    public float impactMultiplier;
    public CombatElement elementOverride;
    public float anomalyBuildUp;
    public bool canTriggerChainSkill;
}
