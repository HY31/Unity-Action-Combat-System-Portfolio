using UnityEngine;

[CreateAssetMenu(menuName = "CharacterSettings/CharacterBuildPreset")]
public class CharacterBuildPreset : ScriptableObject
{
    [Header("Prefab")]
    public GameObject basePrefab;
    public GameObject modelPrefab;
    public RuntimeAnimatorController animatorController;
    public string outputFolderPath;

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
}
