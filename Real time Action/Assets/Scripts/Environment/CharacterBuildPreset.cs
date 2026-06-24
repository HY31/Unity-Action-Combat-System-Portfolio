using UnityEngine;

[CreateAssetMenu(menuName = "CharacterSettings/CharacterBuildPreset")]
public class CharacterBuildPreset : ScriptableObject
{
    [Header("Prefab")]
    public GameObject basePrefab;
    public GameObject modelPrefab;
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
    public CharacterData characterData;
    public RuntimeAnimatorController generatedAnimatorController;
}
