using System;
using UnityEditor;

internal sealed class JaneDoeModelImporter : AssetPostprocessor
{
    internal const string ModelPath =
        "Assets/ImportedCharacters/Jane_Doe/Model/Avatar_Female_Size03_JaneDoe.fbx";
    internal const float ImportScale = 100f;

    private void OnPreprocessModel()
    {
        if (!assetPath.Equals(ModelPath, StringComparison.OrdinalIgnoreCase))
            return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.globalScale = ImportScale;
    }

    [MenuItem("Tools/ZZZ Jane Doe/Reimport Model at Scale 100")]
    private static void ReimportModel()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/ZZZ Jane Doe/Reimport Model at Scale 100", true)]
    private static bool ValidateReimportModel()
    {
        return !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating &&
               AssetDatabase.LoadMainAssetAtPath(ModelPath) != null;
    }
}
