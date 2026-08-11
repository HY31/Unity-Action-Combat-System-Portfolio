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

    [Header("Dodge Timing")]
    [Tooltip("회피 시작 후 평타로 연결할 수 있는 실제 시간(초)이다.")]
    [Min(0f)] public float dodgeAttackCancelTime = 0.12f;

    [Tooltip("회피 시작 후 특수 스킬로 연결할 수 있는 실제 시간(초)이다.")]
    [Min(0f)] public float dodgeSkillCancelTime = 0.12f;

    [Header("Combat Data")]
    public AttackData[] normalCombo;
    public SkillData normalSkillBranch;
    public SkillData enhancedSkillBranch;
    public UltimateData ultimateData;

    [Header("Support")]
    public string parrySupportStartAnim;
    public string parrySupportHeavyAnim;
    public string parrySupportLightAnim;

    [Header("Support Timing")]
    [Tooltip("교대 캐릭터가 나타난 뒤 실제 패링 충돌이 발생하기까지의 시간(초)이다.")]
    [Min(0f)] public float parryWindUpDuration = 0.16f;

    [Tooltip("패링 충돌 뒤 코드로 캐릭터를 밀어내는 시간(초)이다.")]
    [Min(0.01f)] public float parryRecoilDuration = 1f;

    [Tooltip("루트 이동을 제거한 패링 애니메이션 대신 코드로 적용할 총 밀려남 거리다.")]
    [Min(0f)] public float parryRecoilDistance = 1.75f;

    [Tooltip("패링 준비/충돌 애니메이션의 재생 배율이다.")]
    [Min(0.01f)] public float parryPlaybackSpeed = 0.88f;

    [Tooltip("패링 충돌 후 지원 돌격 입력을 받기 시작하는 시간(초)이다.")]
    [Min(0f)] public float parryCounterWindowOpenTime = 0.12f;

    [Tooltip("패링 충돌 후 지원 돌격 입력을 받는 마지막 시간(초)이다.")]
    [Min(0f)] public float parryCounterWindowCloseTime = 0.88f;

    [Tooltip("패링 모션 정규화 시간 기준으로 회피가 다시 허용되는 시점이다.")]
    [Range(0f, 1f)] public float parryDodgeUnlockTime = 0.9f;

    [Header("Character Trait")]
    public bool hasRoamingDodge;
}
