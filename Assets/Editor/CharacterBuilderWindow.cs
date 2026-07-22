using UnityEditor;
using UnityEngine;

public class CharacterBuilderWindow : EditorWindow
{
    private CharacterBuildPreset currentPreset;
    private SerializedObject presetSO;
    private Vector2 scroll;
    private Vector2 validationScroll;
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
                SetCurrentPreset(selectedPreset);
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

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Current Preset", currentPreset, typeof(CharacterBuildPreset), false);
        EditorGUI.EndDisabledGroup();

        if (currentPreset != null)
            EditorGUILayout.LabelField("Preset Path", AssetDatabase.GetAssetPath(currentPreset));

        DrawProperty("basePrefab");
        DrawProperty("modelPrefab");
        DrawProperty("animatorController");
        DrawProperty("outputFolderPath");
        DrawProperty("controllerOutputFolderPath");

        DrawProperty("animationFolderPath");
        DrawProperty("autoAssignDataFromAnimations");
        DrawProperty("autoGenerateAnimatorController");

        DrawProperty("normalSkillToken");
        DrawProperty("normalSkillEndToken");
        DrawProperty("enhancedSkillToken");
        DrawProperty("enhancedSkillEndToken");

        DrawProperty("characterName");

        DrawProperty("dataOutputFolderPath");
        DrawProperty("normalComboCount");
        DrawProperty("createNormalSkillBranch");
        DrawProperty("createEnhancedSkillBranch");
        DrawProperty("createUltimateData");

        DrawProperty("characterData");
        DrawProperty("generatedAnimatorController");

        if (presetSO.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(currentPreset);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        // 버튼 누르는 순서대로 넣음
        if (GUILayout.Button("Validate"))
            lastValidationResult = CharacterBuildPipeline.ValidatePreset(currentPreset, false);

        if (GUILayout.Button("Clear"))
        {
            lastValidationResult = null;
            validationScroll = Vector2.zero;
        }

        if (GUILayout.Button("Create Data Pack"))
            CharacterBuildPipeline.CreateDataPack(currentPreset);

        if (GUILayout.Button("Auto Configure"))
            CharacterBuildPipeline.AutoConfigure(currentPreset);

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

        SetCurrentPreset(preset);

        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
    }

    private void DrawValidationResult()
    {
        if (lastValidationResult == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Validation Result", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (lastValidationResult.HasError)
                EditorGUILayout.HelpBox($"오류 {lastValidationResult.errors.Count}개", MessageType.Error);
            else
                EditorGUILayout.HelpBox("검증 통과", MessageType.Info);

            validationScroll = EditorGUILayout.BeginScrollView(
                validationScroll,
                GUILayout.Height(220f));

            if (lastValidationResult.HasError)
            {
                foreach (string error in lastValidationResult.errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
            else
            {
                foreach (string info in lastValidationResult.infos)
                {
                    EditorGUILayout.HelpBox(info, MessageType.None);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is CharacterBuildPreset selectedPreset)
        {
            SetCurrentPreset(selectedPreset);
        }
    }

    private void SetCurrentPreset(CharacterBuildPreset preset)
    {
        currentPreset = preset;
        lastValidationResult = null;
        RefreshSerializedObject();
        Repaint();
    }
}
