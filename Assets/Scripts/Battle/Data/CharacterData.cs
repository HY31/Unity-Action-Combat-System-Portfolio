using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("StatData")]
    public CharacterStatData statData;

    [Header("Identity")]
    public string characterName;
    public CombatElement element = CombatElement.None;
    public CombatElement Element => element;

    [Header("Move")]
    public float maxSpeed = 6f;
    public float dodgeSpeed = 8f;
    public float walkSpeed = 2.2f;
    public float runSpeed = 6f;
    public float runEnterDelay = 1.5f;

    [Header("Animation - Locomotion")]
    public string idleAnim;
    public string walkStartAnim;
    public string walkLoopAnim;
    public string walkEndAnim;
    public string runLoopAnim;
    public string runEndAnim;

    [Header("Animation - Combat")]
    public string hitLightFrontAnim;
    public string hitHeavyFrontAnim;
    public string dodgeFrontAnim;
    public string dodgeCounterStartAnim;
    public string dodgeCounterEndAnim;

    [Header("Combat Data")]
    public AttackData[] normalCombo;
    public SkillData normalSkillBranch;
    public SkillData enhancedSkillBranch;
    public UltimateData ultimateData;

    [Header("Support")]
    public string parrySupportStartAnim;
    public string parrySupportHeavyAnim;
    public string parrySupportLightAnim;

    [Header("Character Trait")]
    public bool hasRoamingDodge;
}
