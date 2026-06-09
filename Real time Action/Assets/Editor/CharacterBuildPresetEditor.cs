using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterBuildPreset))]
public class CharacterBuildPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        CharacterBuildPreset preset = (CharacterBuildPreset)target;

        if (GUILayout.Button("Validate"))
        {
            ValidatePreset(preset);
        }

        if (GUILayout.Button("Create Prefab"))
        {
            CreatePrefab(preset);
        }
    }

    private void ValidatePreset(CharacterBuildPreset preset)
    {
        bool hasError = false;

        // 1. 프리셋 필드 검사
        if (preset.basePrefab == null)
        {
            Debug.LogError("Base Prefab is missing.");
            hasError = true;
        }

        if (preset.modelPrefab == null)
        {
            Debug.LogError("Model Prefab is missing.");
            hasError = true;
        }

        if (preset.characterData == null)
        {
            Debug.LogError("CharacterData is missing.");
            hasError = true;
        }

        if (preset.animatorController == null)
        {
            Debug.LogError("Animator Controller is missing.");
            hasError = true;
        }

        if (preset.basePrefab == null)
            return;

        // 2. basePrefab 내부 공용 컴포넌트 검사
        PlayerController player = preset.basePrefab.GetComponent<PlayerController>();
        Animator animator = preset.basePrefab.GetComponent<Animator>();
        CharacterController controller = preset.basePrefab.GetComponent<CharacterController>();

        if(player == null)
        {
            Debug.LogError("Base Prefab is missing PlayerController.");
            hasError = true;
        }

        if(animator == null)
        {
            Debug.LogError("Base Prefab is missing Animator.");
            hasError = true;
        }

        if(controller == null)
        {
            Debug.LogError("Base Prefab is missing CharacterController.");
            hasError = true;
        }

        // PlayerController가 없으면 아래 참조 검사는 못 함
        if (player == null)
            return;

        // 3. PlayerController 참조 검사
        if(player.CameraFollowTarget == null)
        {
            Debug.LogError("PlayerController is missing CameraFollowTarget");
            hasError = true;
        }

        if(player.UltHitBox == null)
        {
            Debug.LogError("PlayerController is missing UltHitBox");
            hasError = true;
        }

        for(int i = 0; i < player.SkillHitBoxSlotCount; i++)
        {
            if(player.GetSkillHitBox(i) == null)
            {
                Debug.LogError($"Skill HitBox Slot {i} is missing.");
                hasError = true;
            }
        }

        if (!hasError)
        {
            Debug.Log($"'{preset.characterName}' preset validation passed.");
        }
    }

    private void CreatePrefab(CharacterBuildPreset preset)
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

        if( player == null
            || animator == null
            || modelRoot == null
            )
        {
            Debug.LogError("basePrefab's data is invalid.");
            return;
        }

        // model 교체
        foreach (Transform child in modelRoot)
            DestroyImmediate(child.gameObject);

        GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(preset.modelPrefab);
        newModel.transform.SetParent(modelRoot, false);
        newModel.transform.localPosition = Vector3.zero;
        newModel.transform.localRotation = Quaternion.identity;
        newModel.transform.localScale = Vector3.one;

        // 연결
        SerializedObject playerSO = new SerializedObject(player);
        SerializedProperty characterDataProp = playerSO.FindProperty("characterData");
        characterDataProp.objectReferenceValue = preset.characterData;
        playerSO.ApplyModifiedPropertiesWithoutUndo();

        animator.runtimeAnimatorController = preset.animatorController;

        //저장
        string folderPath = ResolveOutputFolder(preset);
        EnsureFolderExists(folderPath);
        string prefabPath = folderPath + "/" + preset.characterName + ".prefab";

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private string ResolveOutputFolder(CharacterBuildPreset preset)
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

    private void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0]; // "Assets"

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
}