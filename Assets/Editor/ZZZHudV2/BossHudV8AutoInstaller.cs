using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class BossHudV8AutoInstaller
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string MarkerPath =
        "CombatView/BossStatusPanel/SourceFrame/BossHudV8";

    static BossHudV8AutoInstaller()
    {
        EditorApplication.delayCall += InstallWhenReady;
    }

    private static void InstallWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallWhenReady;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.transform.Find(MarkerPath) != null)
            return;

        BossHudV8Installer.Install();
    }
}
