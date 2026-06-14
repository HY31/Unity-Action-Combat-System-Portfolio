using UnityEditor;
using UnityEngine;

public class CharacterBuilderWindow : EditorWindow
{
    private CharacterBuildPreset currentPreset;
    private SerializedObject presetSO;
    private Vector2 scroll;
    private CharacterBuildValidationResult lastValidationResult;

    private string defaultPresetFolder = "Assets/Characters";

    [MenuItem("Tools/Character Builder")]
    public static void Open()
    {
        GetWindow<CharacterBuilderWindow>("Character Builder");
    }

    private void OnEnable()
    {
        RefreshSerializedObject();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Character Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("New Preset"))
        {
            CreateNewPreset();
        }

        if (GUILayout.Button("Use Selected Preset"))
        {
            if (Selection.activeObject is CharacterBuildPreset selectedPreset)
            {
                currentPreset = selectedPreset;
                RefreshSerializedObject();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (currentPreset == null)
        {
            EditorGUILayout.HelpBox(
                "CharacterBuildPreset 에셋을 선택하거나 이 창에 드래그해서 넣으세요.",
                MessageType.Info);
            return;
        }

        if (presetSO == null || presetSO.targetObject != currentPreset)
            RefreshSerializedObject();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        presetSO.Update();

        DrawSectionLabel("Prefab");
        DrawProperty("basePrefab");
        DrawProperty("modelPrefab");
        DrawProperty("animatorController");
        DrawProperty("outputFolderPath");

        DrawSectionLabel("Identity");
        DrawProperty("characterName");

        DrawSectionLabel("Data Pack");
        DrawProperty("dataOutputFolderPath");
        DrawProperty("normalComboCount");
        DrawProperty("createNormalSkillBranch");
        DrawProperty("createEnhancedSkillBranch");
        DrawProperty("createUltimateData");

        DrawSectionLabel("Assigned Data");
        DrawProperty("characterData");

        if (presetSO.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(currentPreset);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Validate"))
            lastValidationResult = CharacterBuildPipeline.ValidatePreset(currentPreset, false);

        if (GUILayout.Button("Create Data Pack"))
            CharacterBuildPipeline.CreateDataPack(currentPreset);

        if (GUILayout.Button("Create Prefab"))
            CharacterBuildPipeline.CreatePrefab(currentPreset);

        EditorGUILayout.EndHorizontal();

        DrawValidationResult();
    }

    private void RefreshSerializedObject()
    {
        presetSO = currentPreset != null ? new SerializedObject(currentPreset) : null;
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = presetSO.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, true);
    }

    private static void DrawSectionLabel(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void CreateNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Character Build Preset",
            "New Character Build Preset",
            "asset",
            "새 CharacterBuildPreset 에셋을 저장할 위치를 선택하세요.",
            defaultPresetFolder);

        if (string.IsNullOrWhiteSpace(path))
            return;

        CharacterBuildPreset preset = ScriptableObject.CreateInstance<CharacterBuildPreset>();
        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        currentPreset = preset;
        RefreshSerializedObject();

        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
    }

    private void DrawValidationResult()
    {
        if (lastValidationResult == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Validation Result", EditorStyles.boldLabel);

        if (lastValidationResult.HasError)
        {
            EditorGUILayout.HelpBox(
                $"오류 {lastValidationResult.errors.Count}개",
                MessageType.Error);

            foreach (string error in lastValidationResult.errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("검증 통과", MessageType.Info);

            foreach (string info in lastValidationResult.infos)
            {
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
        }
    }
}