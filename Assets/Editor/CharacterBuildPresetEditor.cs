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
            CharacterBuildPipeline.ValidatePreset(preset);

        if (GUILayout.Button("Create Data Pack"))
            CharacterBuildPipeline.CreateDataPack(preset);

        if (GUILayout.Button("Auto Configure"))
            CharacterBuildPipeline.AutoConfigure(preset);

        if (GUILayout.Button("Create Prefab"))
            CharacterBuildPipeline.CreatePrefab(preset);
    }
}
