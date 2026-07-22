using UnityEngine;

[CreateAssetMenu(menuName = "CharacterSettings/CharacterBuildPreset")]
public class CharacterBuildPreset : ScriptableObject
{
    [Header("Prefab")]
    public GameObject basePrefab;
    public GameObject modelPrefab;
    // AnimatorController와 OverrideController를 모두 받을 수 있도록 런타임 공통 타입을 사용한다.
    public RuntimeAnimatorController animatorController;
    public string outputFolderPath;

    [Header("Animation Source")]
    public string animationFolderPath;
    public string controllerOutputFolderPath;

    [Header("Automation")]
    public bool autoAssignDataFromAnimations = true;
    public bool autoGenerateAnimatorController = true;

    [Header("Animation Tokens")]
    public string normalSkillToken = "Attack_Branch_01";
    public string normalSkillEndToken = "Attack_Branch_01_End";
    public string enhancedSkillToken = "Attack_Branch_02";
    public string enhancedSkillEndToken = "Attack_Branch_02_End";

    [Header("Identity")]
    public string characterName;

    [Header("Data Pack")]
    public string dataOutputFolderPath;
    [Min(1)] public int normalComboCount = 3;
    public bool createNormalSkillBranch = true;
    public bool createEnhancedSkillBranch = true;
    public bool createUltimateData = true;

    [Header("Assigned Data")]
    // 자동 생성 단계가 만든 데이터와 컨트롤러를 프리팹 생성 단계까지 전달한다.
    public CharacterData characterData;
    public RuntimeAnimatorController generatedAnimatorController;
}
