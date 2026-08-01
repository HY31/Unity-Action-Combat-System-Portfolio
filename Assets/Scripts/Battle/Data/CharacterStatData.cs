using UnityEngine;

[System.Serializable]
public struct CharacterLevelStat
{
    public int level;
    public float attack;
    public float defense;
    public float hp;
    public float impact;
    public float critRate;
    public float critDamage;
    public float anomalyProficiency;
    public float anomalyMastery;
    public float penRatio;
    public float energyRegen;
}

[CreateAssetMenu(menuName = "Combat/CharacterStatData")]
public class CharacterStatData : ScriptableObject
{
    public CharacterLevelStat[] levelStats;

    public CharacterLevelStat GetStatByLevel(int level)
    {
        if (levelStats == null || levelStats.Length == 0)
            return default;

        // 정확한 레벨이 없으면 요청 레벨을 넘지 않는 가장 가까운 구간의 스탯을 사용한다.
        CharacterLevelStat fallback = levelStats[0];

        for (int i = 0; i < levelStats.Length; i++)
        {
            if (levelStats[i].level == level)
                return levelStats[i];

            if (levelStats[i].level < level)
                fallback = levelStats[i];
        }

        return fallback;
    }
}
