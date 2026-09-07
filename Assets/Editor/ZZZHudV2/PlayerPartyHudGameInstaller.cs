using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>게임 Canvas의 플레이어 HUD만 교체한다. 보스/콤보/액션 UI는 유지한다.</summary>
[InitializeOnLoad]
public static class PlayerPartyHudGameInstaller
{
    private const string CanvasPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_HUD_V2_DemoCanvas.prefab";
    private const string HudPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD_Assembly.prefab";
    private const string DonePath = "Library/PlayerPartyHudGameInstalled-v4-clean-pts.done";
    public const float DefaultGameHudScale = 0.55f;

    static PlayerPartyHudGameInstaller()
    {
        if (!File.Exists(DonePath)) EditorApplication.delayCall += InstallWhenReady;
    }

    private static void InstallWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InstallWhenReady;
            return;
        }
        try { Install(); }
        catch (Exception exception)
        {
            string failure = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../.codex-temp/pause-image-repair/game-installation-error.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(failure));
            File.WriteAllText(failure, DateTime.Now.ToString("O") + "\n" + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/ZZZ HUD/Use Assembled Player HUD In Game")]
    public static void Install()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if (prefab == null) throw new InvalidOperationException("Assembled player HUD missing.");
        var report = new StringBuilder();
        report.AppendLine("Game HUD replacement: " + DateTime.Now.ToString("O"));
        string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../.codex-temp/pause-image-repair/before-legacy-cleanup.prefab"));
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.Copy(CanvasPath, backup);
        var canvasRoot = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            ConfigureCanvas(canvasRoot, prefab, report, false);
            PrefabUtility.SaveAsPrefabAsset(canvasRoot, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvasRoot); }

        int sceneCanvases = 0;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (EditorUtility.IsPersistent(canvas) || !canvas.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(canvas.gameObject.scene)) continue;
            if (canvas.name != "ZZZ_HUD_V2_DemoCanvas") continue;
            var hud = ConfigureCanvas(canvas.gameObject, prefab, report, true);
            foreach (var manager in Resources.FindObjectsOfTypeAll<UIManager>())
            {
                if (EditorUtility.IsPersistent(manager) || manager.gameObject.scene != canvas.gameObject.scene) continue;
                manager.UseAssemblyPartyHud(hud);
                EditorUtility.SetDirty(manager);
                if (PrefabUtility.IsPartOfPrefabInstance(manager))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            }
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            sceneCanvases++;
        }
        report.AppendLine("Loaded game canvases updated=" + sceneCanvases + ", playMode=" + Application.isPlaying);
        PlayerPartyHudAssemblyValidation.Run();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/pause-image-repair/game-installation.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        PlayerDecibelHudAssets.RenderSavedCanvas(CanvasPath, Path.GetDirectoryName(output), report);
        File.WriteAllText(output, report.ToString());
        File.WriteAllText(DonePath, report.ToString());
        SceneView.RepaintAll();
        Debug.Log("[PlayerPartyHudGameInstaller] " + report);
    }

    private static PlayerPartyHudAssemblyPresenter ConfigureCanvas(GameObject canvas,
        GameObject prefab, StringBuilder report, bool sceneInstance)
    {
        var existing = canvas.GetComponentsInChildren<PlayerPartyHudAssemblyPresenter>(true);
        if (existing.Length > 1) throw new InvalidOperationException("Duplicate assembled player HUDs on " + canvas.name);
        PlayerPartyHudAssemblyPresenter hud;
        if (existing.Length == 0)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(canvas.transform, false);
            instance.transform.SetAsFirstSibling();
            hud = instance.GetComponent<PlayerPartyHudAssemblyPresenter>();
            hud.transform.localScale = new Vector3(DefaultGameHudScale, DefaultGameHudScale, 1f);
        }
        else hud = existing[0];
        hud.gameObject.SetActive(true);
        // Existing instances retain the user's current size and layout.
        if (PrefabUtility.IsPartOfPrefabInstance(hud.transform))
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud.transform);

        RestoreDecibelHud(canvas, hud, report);

        int removed = 0;
        foreach (var legacy in canvas.GetComponentsInChildren<PartyStatusUI>(true))
        {
            var serialized = new SerializedObject(legacy);
            var source = serialized.FindProperty("memberChainPortraits");
            if (source != null && source.isArray)
            {
                var portraits = new Sprite[source.arraySize];
                for (int i = 0; i < portraits.Length; i++)
                    portraits[i] = source.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                hud.ConfigureChainPortraits(portraits);
            }
            // PTS is now independent and full combo portraits have been copied.
            // Do not retain an inactive legacy hierarchy in the game Canvas.
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            removed++;
        }
        foreach (var manager in canvas.GetComponentsInChildren<UIManager>(true))
            manager.UseAssemblyPartyHud(hud);
        EditorUtility.SetDirty(hud);
        if (PrefabUtility.IsPartOfPrefabInstance(hud))
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);

        int legacyRemaining = canvas.GetComponentsInChildren<PartyStatusUI>(true).Length;
        if (legacyRemaining != 0 || !hud.gameObject.activeSelf)
            throw new InvalidOperationException("Player HUD visibility replacement failed.");
        var rect = hud.GetComponent<RectTransform>();
        report.AppendLine((sceneInstance ? "Live scene " + canvas.scene.name : "Saved game canvas") +
            ": new HUD=1 active, legacy HUD=" + legacyRemaining + " remaining (" + removed + " removed); " +
            "position=" + rect.anchoredPosition + ", size=" + rect.sizeDelta + ", scale=" + rect.localScale);
        report.AppendLine("Other views retained: boss=" + canvas.GetComponentsInChildren<AssaultBattleHUD>(true).Length +
            ", chain=" + canvas.GetComponentsInChildren<ChainSkillPromptUI>(true).Length +
            ", enemy=" + canvas.GetComponentsInChildren<EnemyWorldStatusUI>(true).Length);
        return hud;
    }

    private static void RestoreDecibelHud(GameObject canvas, PlayerPartyHudAssemblyPresenter hud,
        StringBuilder report)
    {
        GameObject prefab = PlayerDecibelHudAssets.EnsurePrefab();
        var existing = canvas.transform.Find("DecibelHUD");
        if (existing != null &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject) != PlayerDecibelHudAssets.PrefabPath)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
            existing = null;
        }
        if (existing == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "DecibelHUD";
            // Local-space parenting is essential; preview Canvas scale may be 0.
            instance.transform.SetParent(canvas.transform, false);
            instance.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);
            existing = instance.transform;
        }
        var view = existing.GetComponent<DecibelHudText>();
        view.gameObject.SetActive(true);
        view.enabled = true;
        var label = view.GetComponent<TMPro.TMP_Text>();
        label.enabled = true;
        label.raycastTarget = false;
        hud.ConfigureDecibelHud(view);
        label.ForceMeshUpdate();
        if (!view.gameObject.activeInHierarchy || !label.text.Contains("PTS") || label.font == null)
            throw new InvalidOperationException("Restored PTS display is not visible/configured.");
        PlayerDecibelHudAssets.ValidateView(view, report);
        var viewRect = view.GetComponent<RectTransform>();
        report.AppendLine("PTS active, Canvas sibling, bound to assembled HUD; text=" + label.text +
            "; position=" + viewRect.anchoredPosition + ", scale=" + viewRect.localScale);
        EditorUtility.SetDirty(view);
        if (PrefabUtility.IsPartOfPrefabInstance(view))
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
    }
}
