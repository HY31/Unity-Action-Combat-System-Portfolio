using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public class CharacterBuildValidationResult
{
    public List<string> errors = new List<string>();
    public List<string> infos = new List<string>();
     
    public bool HasError => errors.Count > 0;
}

public class CharacterBuildPipeline : MonoBehaviour
{
    public static CharacterBuildValidationResult ValidatePreset(CharacterBuildPreset preset, bool logToConsole = true)
    {
        CharacterBuildValidationResult result = new CharacterBuildValidationResult();

        void AddError(string message)
        {
            result.errors.Add(message);
            if (logToConsole)
                Debug.LogError(message);
        }

        void AddInfo(string message)
        {
            result.infos.Add(message);
            if (logToConsole)
                Debug.Log(message);
        }

        if (preset.basePrefab == null)
            AddError("Base Prefab is missing.");

        if (preset.modelPrefab == null)
            AddError("Model Prefab is missing.");

        if (preset.characterData == null)
            AddError("CharacterData is missing.");

        if (preset.animatorController == null)
            AddError("Animator Controller is missing.");

        if (preset.basePrefab == null)
            return result;

        PlayerController player = preset.basePrefab.GetComponent<PlayerController>();
        Animator animator = preset.basePrefab.GetComponent<Animator>();
        CharacterController controller = preset.basePrefab.GetComponent<CharacterController>();

        if (player == null)
            AddError("Base Prefab is missing PlayerController.");

        if (animator == null)
            AddError("Base Prefab is missing Animator.");

        if (controller == null)
            AddError("Base Prefab is missing CharacterController.");

        if (player == null)
            return result;

        if (player.CameraFollowTarget == null)
            AddError("PlayerController is missing CameraFollowTarget.");

        if (player.UltHitBox == null)
            AddError("PlayerController is missing UltHitBox.");

        for (int i = 0; i < player.SkillHitBoxSlotCount; i++)
        {
            if (player.GetSkillHitBox(i) == null)
                AddError($"Skill HitBox Slot {i} is missing.");
        }

        if (!result.HasError)
            AddInfo($"'{preset.characterName}' preset validation passed.");

        return result;
    }

    public static void CreatePrefab(CharacterBuildPreset preset)
    {
        Debug.Log($"Create Prefab: {preset.characterName}");

        if (preset.basePrefab == null
            || preset.modelPrefab == null
            || preset.characterData == null
            || preset.animatorController == null
            || string.IsNullOrWhiteSpace(preset.characterName))
        {
            Debug.LogError("Preset is invalid.");
            return;
        }

        // basePrefab 복제
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(preset.basePrefab);
        instance.name = preset.characterName;

        PlayerController player = instance.GetComponent<PlayerController>();
        Animator animator = instance.GetComponent<Animator>();
        Transform modelRoot = instance.transform.Find("ModelRoot");

        if (player == null
            || animator == null
            || modelRoot == null
            )
        {
            Debug.LogError("basePrefab's data is invalid.");
            DestroyImmediate(instance);
            return;
        }

        // model 교체
        foreach (Transform child in modelRoot)
            DestroyImmediate(child.gameObject);

        GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(preset.modelPrefab);
        newModel.transform.SetParent(modelRoot, false);

        // 모델의 transform 초기화
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;
        newModel.transform.localScale = Vector3.one;

        // 연결
        SerializedObject playerSO = new SerializedObject(player);
        SerializedProperty characterDataProp = playerSO.FindProperty("characterData");
        characterDataProp.objectReferenceValue = preset.characterData;
        playerSO.ApplyModifiedPropertiesWithoutUndo();

        animator.runtimeAnimatorController = preset.animatorController;

        // 저장 폴더 경로 계산 및 생성
        string folderPath = ResolveOutputFolder(preset);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            DestroyImmediate(instance);
            return;
        }

        EnsureFolderExists(folderPath);


        // 최종 프리팹 저장 경로 생성 후 저장
        string prefabPath = folderPath + "/" + preset.characterName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

        // 임시 인스턴스 삭제
        DestroyImmediate(instance);

        // 애셋 데이터베이스 저장 및 새로고침
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ResolveOutputFolder(CharacterBuildPreset preset)
    {
        string folderPath = string.IsNullOrWhiteSpace(preset.outputFolderPath)
            ? "Assets/Prefabs/Players"
            : preset.outputFolderPath.Trim();

        if (!folderPath.StartsWith("Assets"))
        {
            Debug.LogError("Output folder path must start with 'Assets'.");
            return null;
        }

        return folderPath;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0]; // 시작점은 항상 "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    public static void CreateDataPack(CharacterBuildPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.characterName))
        {
            Debug.LogError("Character Name is missing.");
            return;
        }

        if (preset.characterData != null)
        {
            Debug.LogWarning("CharacterData is already assigned. Clear it first if you want to generate a new pack.");
            return;
        }

        if (preset.normalComboCount < 1)
        {
            Debug.LogError("Normal Combo Count must be at least 1.");
            return;
        }

        string folderPath = ResolveDataOutputFolder(preset);
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        EnsureFolderExists(folderPath);

        CharacterData characterData = CreateAsset<CharacterData>(folderPath, $"Character_Data_{preset.characterName}");
        CharacterStatData statData = CreateAsset<CharacterStatData>(folderPath, $"Character_Stat_{preset.characterName}");

        characterData.characterName = preset.characterName;
        characterData.statData = statData;

        AttackData[] combo = new AttackData[preset.normalComboCount];
        for (int i = 0; i < combo.Length; i++)
        {
            combo[i] = CreateAsset<AttackData>(folderPath, $"Attack_{preset.characterName}_Normal_{i + 1:00}");
            combo[i].nextComboIndex = i < combo.Length - 1 ? i + 1 : -1;
        }
        characterData.normalCombo = combo;

        if (preset.createNormalSkillBranch)
        {
            characterData.normalSkillBranch = CreateAsset<SkillData>(folderPath, $"Skill_{preset.characterName}_Normal");
        }

        if (preset.createEnhancedSkillBranch)
        {
            characterData.enhancedSkillBranch = CreateAsset<SkillData>(folderPath, $"Skill_{preset.characterName}_Enhanced");
        }

        if (preset.createUltimateData)
        {
            characterData.ultimateData = CreateAsset<UltimateData>(folderPath, $"Ultimate_{preset.characterName}");
        }

        preset.characterData = characterData;

        // 에셋 바뀌었으니 저장 대상으로 표시
        EditorUtility.SetDirty(characterData);
        EditorUtility.SetDirty(statData);
        EditorUtility.SetDirty(preset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Data Pack created for '{preset.characterName}'.");
    }

    private static string ResolveDataOutputFolder(CharacterBuildPreset preset)
    {
        string folderPath = string.IsNullOrWhiteSpace(preset.dataOutputFolderPath)
            ? $"Assets/Characters/{preset.characterName}/Data"
            : preset.dataOutputFolderPath.Trim();

        if (!folderPath.StartsWith("Assets"))
        {
            Debug.LogError("Data output folder path must start with 'Assets'.");
            return null;
        }

        return folderPath;
    }

    private static T CreateAsset<T>(string folderPath, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{fileName}.asset");
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }
}
