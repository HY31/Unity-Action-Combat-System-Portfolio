[System.Serializable]
public struct HitPayload
{
    // 공격 종류와 무관하게 에셋에 저장할 수 있는 정적 히트 설정만 모아 둔다.
    public float damageMultiplier;
    public float impactMultiplier;

    // 이 공격이 적의 숨은 경직 게이지에 누적하는 값이다. 경직을 주지 않는 공격은 0으로 둔다.
    public float hitReactionBuildUp;

    public CombatElement elementOverride;
    public float anomalyBuildUp;
    public bool canTriggerChainSkill;
}
