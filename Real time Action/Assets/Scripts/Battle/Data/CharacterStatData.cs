using UnityEngine;

[CreateAssetMenu(menuName = "Combat/CharacterStatData")]
public class CharacterStatData : ScriptableObject
{
    public CharacterLevelStat[] levelStats;
}

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