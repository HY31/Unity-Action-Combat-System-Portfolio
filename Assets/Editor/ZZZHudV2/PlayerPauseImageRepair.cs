using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// One-time migration also updates an open prefab stage, which can otherwise save
// stale Image settings back over an externally updated prefab asset.
[InitializeOnLoad]
public static class PlayerPauseImageRepair
{
    private const string PrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD_Assembly.prefab";
    private const string SpritePath = "Assets/Sprites/ZZZHudV2/PlayerPartyV3/Extracted/player_pause_button_approved.png";
    private const string MaterialPath = "Assets/Materials/UI/PlayerHudPauseApproved.mat";
    private const string CompletionPath = "Library/PlayerPauseImageRepair-v3.done";

    static PlayerPauseImageRepair()
    {
        if (!File.Exists(CompletionPath))
            EditorApplication.delayCall += RepairWhenReady;
    }

    private static void RepairWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RepairWhenReady;
            return;
        }
        Repair();
    }

    [MenuItem("Tools/ZZZ HUD/Repair Pause and Shared Background (Keep Layout)")]
    public static void Repair()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (sprite == null || material == null || material.shader == null)
            throw new InvalidOperationException("Approved pause image/material is not imported.");

        var report = new StringBuilder();
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/pause-image-repair"));
        Directory.CreateDirectory(output);
        var backup = Path.Combine(output, "before-pause-layer-wiring.prefab");
        if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
        report.AppendLine("Pause image repair: " + DateTime.Now.ToString("O"));
        report.AppendLine("Shader: " + material.shader.name + ", supported=" + material.shader.isSupported);
        foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
            report.AppendLine("Shader message: " + message.message);

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            RepairRoot(stage.prefabContentsRoot, sprite, material, report, "Open prefab stage", true);
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, PrefabPath);
        }
        else
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RepairRoot(root, sprite, material, report, "Saved prefab", false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Only scene instances of this exact prefab; never touch unrelated buttons.
        foreach (var bridge in Resources.FindObjectsOfTypeAll<PlayerPartyPauseButton>())
        {
            if (EditorUtility.IsPersistent(bridge) || !bridge.gameObject.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(bridge.gameObject.scene))
                continue;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bridge.gameObject) != PrefabPath)
                continue;
            var image = bridge.GetComponent<Image>();
            RepairImage(image, sprite, material, report, "Scene instance", true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
        }

        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        RenderPreview(sprite, material, Path.Combine(output, "pause-button-unity-preview.png"));
        File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
        PlayerPartyHudAssemblyValidation.Run();
        File.WriteAllText(CompletionPath, report.ToString());
        Debug.Log("[PlayerPauseImageRepair] " + report);
    }

    private static void RepairRoot(GameObject root, Sprite sprite, Material material,
        StringBuilder report, string context, bool undo)
    {
        var bridge = root.GetComponentInChildren<PlayerPartyPauseButton>(true);
        if (bridge == null) throw new InvalidOperationException("Pause button not found in " + context);
        RepairImage(bridge.GetComponent<Image>(), sprite, material, report, context, undo);
        var background = InstallSharedBackground(root, undo);
        MovePauseBelowFrame(root, background, undo, report);
        report.AppendLine(context + " shared background: " + ColorUtility.ToHtmlStringRGBA(background.color) +
            "; removed HealthBackground/EnergyBackground only.");
        int removedBackgroundsRemaining = 0, trails = 0, hp = 0, energy = 0;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "HealthBackground" || image.name == "EnergyBackground") removedBackgroundsRemaining++;
            if (image.name == "HealthDamageTrail") trails++;
            if (image.name == "HealthCurrent") hp++;
            if (image.name == "EnergyFill") energy++;
        }
        report.AppendLine("Remaining individual backgrounds=" + removedBackgroundsRemaining +
            ", health fills=" + hp + ", health trails=" + trails + ", energy fills=" + energy);
    }

    private static void MovePauseBelowFrame(GameObject root, PlayerHudFrameBackground background,
        bool undo, StringBuilder report)
    {
        var pause = root.GetComponentInChildren<PlayerPartyPauseButton>(true).GetComponent<RectTransform>();
        var before = new Vector3[4];
        var after = new Vector3[4];
        pause.GetWorldCorners(before);
        if (undo) Undo.SetTransformParent(pause, background.transform.parent, "Move pause below HUD frame");
        else pause.SetParent(background.transform.parent, true);
        pause.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
        pause.GetWorldCorners(after);
        float difference = 0f;
        for (int i = 0; i < 4; i++) difference = Mathf.Max(difference, Vector3.Distance(before[i], after[i]));
        report.AppendLine("Pause layer: FrameBackground < PauseButton < FrameOverlay; corner drift=" + difference);
        if (difference > 0.01f) throw new InvalidOperationException("Pause layout changed while moving layers.");
    }

    public static PlayerHudFrameBackground InstallSharedBackground(GameObject root, bool undo = false)
    {
        Image frame = null;
        var backgroundColor = (Color)new Color32(24, 27, 29, 255);
        var images = root.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image.name == "FrameOverlay") frame = image;
            if (image.name == "EnergyBackground") backgroundColor = image.color;
        }
        if (frame == null || frame.sprite == null)
            throw new InvalidOperationException("FrameOverlay sprite is missing.");

        var background = root.GetComponentInChildren<PlayerHudFrameBackground>(true);
        if (background == null)
        {
            var go = new GameObject("FrameBackground", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PlayerHudFrameBackground));
            go.layer = frame.gameObject.layer;
            go.transform.SetParent(frame.transform.parent, false);
            if (undo) Undo.RegisterCreatedObjectUndo(go, "Add shared HUD background");
            background = go.GetComponent<PlayerHudFrameBackground>();
        }
        else
        {
            backgroundColor = background.color;
        }
        background.transform.SetAsFirstSibling();
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame.sprite.texture)));
            var pixels = texture.GetPixels32();
            var bands = new List<Vector4>();
            for (int y = 0; y < texture.height; y++)
            {
                int left = 0, right = texture.width - 1;
                while (left < texture.width && pixels[y * texture.width + left].a < 192) left++;
                while (right >= left && pixels[y * texture.width + right].a < 192) right--;
                // Stay inside the opaque black rim; it covers the background's edge.
                if (right - left > 2)
                    bands.Add(new Vector4((left + 1f) / texture.width, right / (float)texture.width,
                        y / (float)texture.height, (y + 1f) / texture.height));
            }
            background.Configure(frame.rectTransform, bands.ToArray(), backgroundColor);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        foreach (var image in images)
        {
            if (image.name != "HealthBackground" && image.name != "EnergyBackground") continue;
            if (undo) Undo.DestroyObjectImmediate(image.gameObject);
            else UnityEngine.Object.DestroyImmediate(image.gameObject);
        }
        EditorUtility.SetDirty(background);
        return background;
    }

    private static void RepairImage(Image image, Sprite sprite, Material material,
        StringBuilder report, string context, bool undo)
    {
        report.AppendLine(context + " before: sprite=" + AssetDatabase.GetAssetPath(image.sprite) +
            ", material=" + AssetDatabase.GetAssetPath(image.material) +
            ", type=" + image.type + ", fill=" + image.fillAmount);
        if (undo) Undo.RecordObject(image, "Repair approved pause image");
        image.sprite = sprite;
        image.overrideSprite = null;
        image.material = material;
        image.type = Image.Type.Simple;
        image.fillAmount = 1f;
        image.SetAllDirty();
        EditorUtility.SetDirty(image);
        report.AppendLine(context + " after: material=" + AssetDatabase.GetAssetPath(image.material) +
            ", type=" + image.type + ", fill=" + image.fillAmount + "; RectTransform unchanged.");
    }

    private static void RenderPreview(Sprite sprite, Material material, string path)
    {
        var target = RenderTexture.GetTemporary(680, 578, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var drawMaterial = new Material(material);
        Texture2D result = new Texture2D(680, 578, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0.25f, 0.4f, 0.52f, 1f));
            GL.PushMatrix();
            try
            {
                GL.LoadOrtho();
                drawMaterial.SetTexture("_MainTex", sprite.texture);
                drawMaterial.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Color(Color.white);
                GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                GL.End();
            }
            finally { GL.PopMatrix(); }
            result.ReadPixels(new Rect(0, 0, 680, 578), 0, 0);
            result.Apply();
            File.WriteAllBytes(path, result.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(result);
            UnityEngine.Object.DestroyImmediate(drawMaterial);
        }
    }
}
