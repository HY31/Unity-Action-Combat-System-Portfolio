using UnityEngine;

[System.Serializable]
public struct EnemyElementModifier
{
    public CombatElement element;
    [Tooltip("이 속성 이상이 발동했을 때 보스의 현재 공격을 끊고 경직시키는 약점인지 결정한다.")]
    public bool isWeakness;
    public float damageMultiplier;
    public float anomalyMultiplier;
}
