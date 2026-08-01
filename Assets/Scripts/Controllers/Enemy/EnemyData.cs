using UnityEngine;

[CreateAssetMenu(menuName = "Combat(Enemy)/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;
    public int level = 1;

    [Header("Base Stats")]
    public float maxHp = 100f;
    public float attack = 10f;
    public float defense = 50f;
    public float impact = 10f;

    [Header("Stun / Groggy")]
    public float maxStun = 100f;
    [Range(0f, 1f)] public float stunResistance = 0f;
    public float groggyDuration = 3f;
    [Min(1f)] public float groggyDamageMultiplier = 1.5f;

    [Header("Hit Reaction")]
    // 강공격이 짧은 시간 안에 누적될 때만 경직되도록 감소형 숨은 게이지를 설정한다.
    [Min(0f)] public float maxHitReactionGauge = 100f;
    [Min(0f)] public float hitReactionThreshold = 80f;
    [Min(0f)] public float hitReactionResetValue = 50f;
    [Min(0f)] public float hitReactionDecayPerSecond = 45f;
    [Min(0f)] public float hitReactionDuration = 0.45f;

    [Header("Anomaly")]
    public float anomalyThreshold = 100f;
    public EnemyElementModifier[] elementModifiers;

    [Header("Attack Patterns")]
    public EnemyAttackData[] attackPatterns;

    public EnemyElementModifier GetElementModifier(CombatElement element)
    {
        // 명시된 약점·저항이 없는 속성은 피해와 이상 축적을 그대로 통과시킨다.
        if (elementModifiers == null || elementModifiers.Length == 0)
        {
            return new EnemyElementModifier
            {
                element = element,
                damageMultiplier = 1f,
                anomalyMultiplier = 1f
            };
        }

        for (int i = 0; i < elementModifiers.Length; i++)
        {
            if (elementModifiers[i].element == element)
                return elementModifiers[i];
        }

        return new EnemyElementModifier
        {
            element = element,
            damageMultiplier = 1f,
            anomalyMultiplier = 1f
        };
    }
}
