using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class BossHudV8PlayModeInstallHook
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string MarkerPath =
        "CombatView/BossStatusPanel/SourceFrame/BossHudV8";

    static BossHudV8PlayModeInstallHook()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += TryInstall;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryInstall;
    }

    private static void TryInstall()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.transform.Find(MarkerPath) != null)
            return;

        BossHudV8Installer.Install();
    }
}
