using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class PlayerDecibelHudAssets
{
    public const string PrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_DecibelHUD.prefab";
    private const string LegacyPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD.prefab";
    private const string FontPath = "Assets/Resources/Fonts/BarlowCondensed/DecibelHudSDF.asset";

    public static GameObject EnsurePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        TMP_FontAsset font = EnsureFont();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPath)
            .GetComponentInChildren<DecibelHudText>(true);
        if (source == null) throw new InvalidOperationException("Approved PTS source missing.");
        var staging = new GameObject("Inactive PTS extraction");
        staging.SetActive(false);
        try
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject, staging.transform, false);
            clone.name = "ZZZ_DecibelHUD";
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = source.GetComponent<RectTransform>().sizeDelta;
            rect.localScale = source.transform.localScale;
            rect.localRotation = Quaternion.identity;
            // Original Canvas offset (64,-48) plus authored text (154,-136.6).
            // Never use world coordinates: unloaded overlay canvases have scale 0.
            rect.anchoredPosition3D = new Vector3(218f, -184.6f, 0f);
            clone.GetComponent<DecibelHudText>().ConfigureFont(font);
            var label = clone.GetComponent<TMP_Text>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(staging); }
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        ValidateView(prefab.GetComponent<DecibelHudText>(), null);
        return prefab;
    }

    private static TMP_FontAsset EnsureFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) return existing;
        var source = Resources.Load<Font>("Fonts/BarlowCondensed/BarlowCondensed-Black");
        if (source == null) throw new InvalidOperationException("Barlow Condensed font missing.");
        var font = TMP_FontAsset.CreateFontAsset(source, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024);
        font.name = "Decibel HUD Barlow Condensed Black SDF";
        if (!font.TryAddCharacters("0123456789PTS"))
            throw new InvalidOperationException("Could not bake all PTS glyphs.");
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.material.DisableKeyword("OUTLINE_ON");
        font.material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        font.material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        AssetDatabase.CreateAsset(font, FontPath);
        foreach (var texture in font.atlasTextures)
        {
            texture.name = "Decibel HUD Atlas";
            texture.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(texture, font);
        }
        font.material.name = "Decibel HUD SDF Material";
        font.material.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }

    public static void ValidateView(DecibelHudText view, StringBuilder report)
    {
        var rect = view.GetComponent<RectTransform>();
        var label = view.GetComponent<TMP_Text>();
        var serialized = new SerializedObject(view);
        var font = serialized.FindProperty("displayFont").objectReferenceValue as TMP_FontAsset;
        if (Mathf.Abs(rect.localScale.x) < 0.01f || Mathf.Abs(rect.localScale.y) < 0.01f ||
            rect.rect.width < 1f || rect.rect.height < 1f)
            throw new InvalidOperationException("PTS has zero/invalid local dimensions.");
        if (font == null || !EditorUtility.IsPersistent(font) || label.font != font ||
            label.fontSharedMaterial == null || !EditorUtility.IsPersistent(label.fontSharedMaterial))
            throw new InvalidOperationException("PTS font/material is not saved as a persistent asset.");
        report?.AppendLine("PTS layout/font PASS: position=" + rect.anchoredPosition +
            ", scale=" + rect.localScale + ", size=" + rect.sizeDelta + ", persistent font/material.");
    }

    // Render the SAVED game-canvas copy, with unrelated views inactive. This tests
    // actual text/silhouette pixels, not just activeSelf or a populated string.
    public static void RenderSavedCanvas(string canvasPath, string output, StringBuilder report)
    {
        var staging = new GameObject("Inactive saved HUD render");
        staging.SetActive(false);
        var canvasCopy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(canvasPath), staging.transform, false);
        var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var texture = new Texture2D(600, 240, TextureFormat.RGB24, false);
        try
        {
            var canvas = canvasCopy.GetComponent<Canvas>();
            var scaler = canvasCopy.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.scaleFactor = 1f;
            var root = canvas.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(1920f, 1080f);
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;
            root.anchoredPosition3D = Vector3.zero;
            var cameraObject = new GameObject("PTS validation camera", typeof(Camera));
            cameraObject.transform.SetParent(staging.transform, false);
            cameraObject.transform.localPosition = new Vector3(960f, 540f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            camera.aspect = 1920f / 1080f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.20f, 0.30f, 0.38f, 1f);
            camera.cullingMask = 1 << 31;
            camera.targetTexture = target;
            canvas.worldCamera = camera;
            foreach (Transform child in root) child.gameObject.SetActive(child.name == "DecibelHUD");
            foreach (var behaviour in canvasCopy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is CanvasScaler) && !(behaviour is Graphic) && !(behaviour is DecibelHudText))
                    behaviour.enabled = false;
            var view = root.Find("DecibelHUD").GetComponent<DecibelHudText>();
            if (canvasCopy.GetComponentsInChildren<PartyStatusUI>(true).Length != 0)
                throw new InvalidOperationException("Legacy party HUD remains in saved canvas.");
            var presenter = canvasCopy.GetComponentInChildren<PlayerPartyHudAssemblyPresenter>(true);
            if (new SerializedObject(presenter).FindProperty("decibelHudText").objectReferenceValue != view)
                throw new InvalidOperationException("Saved PTS data binding is missing.");
            staging.SetActive(true);
            ValidateView(view, report);
            foreach (int value in new[] {0, 3000})
            {
                view.SetValue(value);
                view.GetComponent<TMP_Text>().ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                foreach (var child in canvasCopy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var label = view.GetComponent<TMP_Text>();
                report.AppendLine("Render input: glyphs=" + label.textInfo.characterCount +
                    ", vertices=" + label.mesh.vertexCount + ", position=" + view.transform.position +
                    ", world scale=" + view.transform.lossyScale + ", canvas=" + root.rect);
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 810, 600, 240), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                Color32 background = pixels[0];
                int drawn = 0;
                foreach (var p in pixels)
                    if (Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) > 20) drawn++;
                File.WriteAllBytes(Path.Combine(output, "decibel-" + value + "-preview.png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(output, "decibel-render-diagnostics.txt"), report.ToString());
                if (drawn < 500) throw new InvalidOperationException("PTS render empty/small: " + drawn + " pixels.");
                report.AppendLine("RENDER PASS " + value + " PTS: " + drawn + " non-background pixels in expected screen region.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(staging);
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
