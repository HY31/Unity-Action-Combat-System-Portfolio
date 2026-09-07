using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Adds clipping to the authored HUD without rebuilding or repositioning it.
[InitializeOnLoad]
public static class PlayerHudRightEdgeClipInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD_Assembly.prefab";
    private const string DonePath = "Library/PlayerHudRightEdgeClip-v1.done";

    static PlayerHudRightEdgeClipInstaller()
    {
        if (!File.Exists(DonePath)) EditorApplication.delayCall += WhenReady;
    }

    private static void WhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += WhenReady;
            return;
        }
        Install();
    }

    [MenuItem("Tools/ZZZ HUD/Crop Player Gauge Right Ends (Keep Layout)")]
    public static void Install()
    {
        var report = new StringBuilder("Right-end gauge clipping: " + DateTime.Now.ToString("O") + "\n");
        ValidateMesh(report);
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/pause-image-repair"));
        Directory.CreateDirectory(output);
        var backup = Path.Combine(output, "before-right-edge-clipping.prefab");
        if (!File.Exists(backup)) File.Copy(PrefabPath, backup);

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            Apply(stage.prefabContentsRoot, report, "Open prefab stage");
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, PrefabPath);
        }
        else
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Apply(root, report, "Source prefab");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        foreach (var hud in Resources.FindObjectsOfTypeAll<PlayerPartyHudAssemblyPresenter>())
        {
            if (EditorUtility.IsPersistent(hud) || !hud.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(hud.gameObject.scene)) continue;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud.gameObject) != PrefabPath) continue;
            Apply(hud.gameObject, report, "Live " + hud.gameObject.scene.name);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        }
        PlayerPartyHudAssemblyValidation.Run();
        report.AppendLine("PASS: existing health/energy/marker/pause wiring validation.");
        File.WriteAllText(Path.Combine(output, "right-edge-clipping.txt"), report.ToString());
        File.WriteAllText(DonePath, report.ToString());
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        Debug.Log("[PlayerHudRightEdgeClipInstaller] " + report);
    }

    private static void Apply(GameObject root, StringBuilder report, string context)
    {
        int count = 0;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            bool health = image.name == "HealthCurrent" || image.name == "HealthDamageTrail";
            if (!health && image.name != "EnergyFill") continue;
            bool active = image.transform.parent.name == "ActiveSlot";
            float inset = health ? (active ? 44f : 28f) : (active ? 22f : 18f);
            var rect = image.rectTransform;
            Vector3 position = rect.localPosition, scale = rect.localScale;
            Vector2 size = rect.sizeDelta;
            Sprite sprite = image.sprite;
            float fill = image.fillAmount;
            PlayerHudRightEdgeClip.Configure(image, inset);
            if (position != rect.localPosition || scale != rect.localScale || size != rect.sizeDelta ||
                sprite != image.sprite || fill != image.fillAmount)
                throw new InvalidOperationException("Gauge layout/data changed: " + image.name);
            var clip = image.GetComponent<PlayerHudRightEdgeClip>();
            EditorUtility.SetDirty(clip);
            if (PrefabUtility.IsPartOfPrefabInstance(clip)) PrefabUtility.RecordPrefabInstancePropertyModifications(clip);
            report.AppendLine(context + "/" + image.transform.parent.name + "/" + image.name +
                              ": right crop=" + inset + ", layout/sprite/fill unchanged");
            count++;
        }
        if (count != 9) throw new InvalidOperationException("Expected 9 gauge images, found " + count);
    }

    private static void ValidateMesh(StringBuilder report)
    {
        var go = new GameObject("Right edge clip test", typeof(RectTransform), typeof(Image), typeof(PlayerHudRightEdgeClip));
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var image = go.GetComponent<Image>();
            image.rectTransform.sizeDelta = new Vector2(100f, 20f);
            var clip = go.GetComponent<PlayerHudRightEdgeClip>();
            foreach (float inset in new[] {0f, 10f, 100f, 120f})
            foreach (float fill in new[] {0f, 0.4f, 0.9f, 1f})
            {
                clip.RightInset = inset;
                image.fillAmount = fill;
                using (var mesh = new VertexHelper())
                {
                    if (fill > 0f)
                    {
                        float end = -50f + 100f * fill;
                        mesh.AddVert(new Vector3(-50f, -10f), Color.white, new Vector2(0f, 0f));
                        mesh.AddVert(new Vector3(-50f, 10f), Color.white, new Vector2(0f, 1f));
                        mesh.AddVert(new Vector3(end, 10f), Color.white, new Vector2(fill, 1f));
                        mesh.AddVert(new Vector3(end, -10f), Color.white, new Vector2(fill, 0f));
                        mesh.AddTriangle(0, 1, 2);
                        mesh.AddTriangle(2, 3, 0);
                    }
                    clip.ModifyMesh(mesh);
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    UIVertex vertex = default;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        min = Mathf.Min(min, vertex.position.x);
                        max = Mathf.Max(max, vertex.position.x);
                        if (Mathf.Abs(vertex.uv0.x - (vertex.position.x + 50f) / 100f) > 0.0001f ||
                            Mathf.Abs(vertex.uv0.y - (vertex.position.y + 10f) / 20f) > 0.0001f)
                            throw new InvalidOperationException("Crop stretched texture UVs.");
                    }
                    if (fill > 0f && inset < 100f &&
                        (Mathf.Abs(min + 50f) > 0.001f ||
                         Mathf.Abs(max - Mathf.Min(-50f + 100f * fill, 50f - inset)) > 0.001f))
                        throw new InvalidOperationException("Crop moved left or partial-fill edge.");
                    if (image.fillAmount != fill || image.rectTransform.sizeDelta != new Vector2(100f, 20f))
                        throw new InvalidOperationException("Crop changed gauge data/size.");
                }
            }
            report.AppendLine("PASS: 16 mesh cases; fixed RIGHT clipping, left edge, partial fills, original UV mapping and size preserved.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
