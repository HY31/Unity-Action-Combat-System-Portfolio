using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class DeadEndButcherModelImporter : AssetPostprocessor
{
    internal const string ModelRoot = "Assets/ThirdParty/ZZZ_DeadEndButcher";
    internal const float ImportScale = 100f;

    private void OnPreprocessModel()
    {
        if (!IsButcherModel(assetPath))
            return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.globalScale = ImportScale;
    }

    internal static bool IsButcherModel(string path)
    {
        return path.StartsWith(ModelRoot + "/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class DeadEndButcherModelImportTools
{
    private const string ReimportMenu = "Tools/ZZZ Dead End Butcher/Reimport Models at Scale 100";

    [MenuItem(ReimportMenu)]
    private static void ReimportModels()
    {
        string[] assetGuids = AssetDatabase.FindAssets(
            "t:Model",
            new[] { DeadEndButcherModelImporter.ModelRoot });
        List<string> reimportedPaths = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string assetGuid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!DeadEndButcherModelImporter.IsButcherModel(path))
                    continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                reimportedPaths.Add(path);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();

        if (reimportedPaths.Count == 0)
        {
            Debug.LogWarning(
                $"No FBX models were found below {DeadEndButcherModelImporter.ModelRoot}.");
            return;
        }

        Debug.Log(
            $"Reimported {reimportedPaths.Count} Dead End Butcher model(s) " +
            $"at scale {DeadEndButcherModelImporter.ImportScale}:\n" +
            string.Join("\n", reimportedPaths));
    }

    [MenuItem(ReimportMenu, true)]
    private static bool ValidateReimportModels()
    {
        return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
    }
}
