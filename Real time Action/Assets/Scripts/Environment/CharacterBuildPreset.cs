using UnityEngine;

[CreateAssetMenu(menuName = "CharacterSettings/CharacterBuildPreset")]
public class CharacterBuildPreset : ScriptableObject
{
    [Header("Settings")]
    public GameObject basePrefab;
    public GameObject modelPrefab;
    public string characterName;
    public CharacterData characterData;
    public RuntimeAnimatorController animatorController;
    public string outputFolderPath;
}
