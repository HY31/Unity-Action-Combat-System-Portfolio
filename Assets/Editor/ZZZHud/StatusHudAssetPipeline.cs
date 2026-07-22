using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 기본 상태 HUD 전용 텍스처를 UI Sprite 형식으로 자동 설정한다.
internal sealed class StatusHudTexturePostprocessor : AssetPostprocessor
{
    private const string SpriteRoot = "Assets/Sprites/ZZZHud/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteRoot, StringComparison.Ordinal) ||
            assetPath.Contains("/Reference/"))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;

        string filename = Path.GetFileNameWithoutExtension(assetPath);
        if (filename == "player_panel_frame")
            importer.spriteBorder = new Vector4(32f, 24f, 32f, 24f);
        else if (filename == "player_bar_frame")
            importer.spriteBorder = new Vector4(16f, 12f, 16f, 12f);
        else if (filename == "enemy_bar_frame")
            importer.spriteBorder = new Vector4(14f, 10f, 14f, 10f);
        else
            importer.spriteBorder = Vector4.zero;
    }
}

[InitializeOnLoad]
// 단일 플레이어·적 상태 UI와 확인용 Demo Canvas를 에디터에서 생성한다.
internal static class StatusHudPrefabBuilder
{
    private const string SpriteRoot = "Assets/Sprites/ZZZHud";
    private const string PrefabRoot = "Assets/Prefabs/UI/ZZZHud";
    private const string PlayerPrefabPath = PrefabRoot + "/ZZZ_PlayerHUD.prefab";
    private const string EnemyPrefabPath = PrefabRoot + "/ZZZ_EnemyHUD.prefab";
    private const string AnomalyPrefabPath = PrefabRoot + "/ZZZ_AnomalyRing.prefab";
    private const string DemoPrefabPath = PrefabRoot + "/ZZZ_HUD_DemoCanvas.prefab";
    private const string AutoBuildSessionKey = "ZZZHudPrefabBuilder.AutoBuildAttempted";

    private readonly struct GaugeParts
    {
        public readonly GameObject Root;
        public readonly Image Fill;
        public readonly Image DelayedFill;
        public readonly AnimatedGaugeUI Gauge;

        public GaugeParts(GameObject root, Image fill, Image delayedFill, AnimatedGaugeUI gauge)
        {
            Root = root;
            Fill = fill;
            DelayedFill = delayedFill;
            Gauge = gauge;
        }
    }

    static StatusHudPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
    }

    [MenuItem("Tools/ZZZ HUD/Build or Refresh Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabRoot);
        ReimportSprites();

        GameObject playerPrefab = BuildPlayerPrefab();
        GameObject enemyPrefab = BuildEnemyPrefab();
        GameObject anomalyPrefab = BuildAnomalyPrefab();
        BuildDemoCanvas(playerPrefab, enemyPrefab, anomalyPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ZZZ HUD prefabs are ready in {PrefabRoot}");
    }

    [MenuItem("Tools/ZZZ HUD/Ping Demo Canvas")]
    private static void PingDemoCanvas()
    {
        UnityEngine.Object demo = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DemoPrefabPath);
        if (demo == null)
        {
            BuildAll();
            demo = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DemoPrefabPath);
        }

        Selection.activeObject = demo;
        EditorGUIUtility.PingObject(demo);
    }

    private static void AutoBuildOnce()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false) ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        SessionState.SetBool(AutoBuildSessionKey, true);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath) == null)
            BuildAll();
    }

    private static GameObject BuildPlayerPrefab()
    {
        Sprite panelSprite = LoadSprite("player_panel_frame");
        Sprite frameSprite = LoadSprite("player_bar_frame");
        Sprite hpSprite = LoadSprite("player_hp_fill");
        Sprite energySprite = LoadSprite("player_energy_fill");
        Sprite ringSprite = LoadSprite("anomaly_ring_frame");
        Sprite chevronSprite = LoadSprite("gauge_chevron");

        GameObject root = CreateUiObject("ZZZ_PlayerHUD", null);
        SetRect(root, new Vector2(768f, 142f), Vector2.zero);

        Image panel = root.AddComponent<Image>();
        panel.sprite = panelSprite;
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = false;

        Image portraitFrame = CreateImage("PortraitFrame", root.transform, ringSprite, Color.white);
        SetRect(portraitFrame.gameObject, new Vector2(118f, 118f), new Vector2(132f, 0f));
        portraitFrame.preserveAspect = true;

        Image chevron = CreateImage("CombatMark", root.transform, chevronSprite, new Color32(185, 255, 39, 235));
        SetRect(chevron.gameObject, new Vector2(52f, 52f), new Vector2(42f, 0f));
        chevron.preserveAspect = true;

        GaugeParts health = CreateGauge(
            "HealthGauge",
            root.transform,
            new Vector2(500f, 40f),
            new Vector2(112f, 28f),
            frameSprite,
            hpSprite,
            new Color32(183, 255, 35, 255),
            new Color32(255, 236, 226, 220),
            1f,
            true,
            new Vector4(8f, 6f, 8f, 6f));

        GaugeParts energy = CreateGauge(
            "EnergyGauge",
            root.transform,
            new Vector2(482f, 26f),
            new Vector2(121f, -21f),
            null,
            energySprite,
            new Color32(126, 117, 255, 255),
            null,
            0.42f,
            true,
            new Vector4(0f, 2f, 0f, 2f));

        PlayerStatusUI status = root.AddComponent<PlayerStatusUI>();
        status.Configure(health.Gauge, energy.Gauge);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject BuildEnemyPrefab()
    {
        Sprite frameSprite = LoadSprite("enemy_bar_frame");
        Sprite hpSprite = LoadSprite("enemy_hp_fill");
        Sprite dazeSprite = LoadSprite("enemy_daze_fill");
        Sprite anomalySprite = LoadSprite("enemy_anomaly_fill");

        GameObject root = CreateUiObject("ZZZ_EnemyHUD", null);
        SetRect(root, new Vector2(700f, 94f), Vector2.zero);

        GameObject visuals = CreateUiObject("Visuals", root.transform);
        Stretch(visuals.GetComponent<RectTransform>());

        GaugeParts hp = CreateRawGauge(
            "HealthGauge",
            visuals.transform,
            new Vector2(620f, 34f),
            new Vector2(0f, 26f),
            frameSprite,
            hpSprite,
            new Color32(255, 72, 58, 255),
            0.82f,
            new Vector4(8f, 7f, 8f, 7f));

        CreateRawGauge(
            "DazeGauge",
            visuals.transform,
            new Vector2(520f, 14f),
            new Vector2(0f, -2f),
            null,
            dazeSprite,
            new Color32(255, 202, 55, 255),
            0.64f,
            new Vector4(0f, 1f, 0f, 1f));

        GaugeParts anomaly = CreateRawGauge(
            "AnomalyGauge",
            visuals.transform,
            new Vector2(420f, 12f),
            new Vector2(0f, -24f),
            null,
            anomalySprite,
            new Color32(91, 226, 255, 255),
            0.47f,
            new Vector4(0f, 1f, 0f, 1f));

        EnemyStatusUI status = root.AddComponent<EnemyStatusUI>();
        SerializedObject serializedStatus = new SerializedObject(status);
        serializedStatus.FindProperty("hpFill").objectReferenceValue = hp.Fill;
        serializedStatus.FindProperty("anomalyFill").objectReferenceValue = anomaly.Fill;
        serializedStatus.FindProperty("visualRoot").objectReferenceValue = visuals;
        serializedStatus.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject BuildAnomalyPrefab()
    {
        Sprite fillSprite = LoadSprite("anomaly_ring_fill");
        Sprite frameSprite = LoadSprite("anomaly_ring_frame");
        Sprite chevronSprite = LoadSprite("gauge_chevron");

        GameObject root = CreateUiObject("ZZZ_AnomalyRing", null);
        SetRect(root, new Vector2(192f, 192f), Vector2.zero);

        Image fill = CreateImage("Fill", root.transform, fillSprite, new Color32(255, 93, 45, 255));
        Stretch(fill.rectTransform);
        ConfigureFill(fill, false);
        fill.fillAmount = 0.68f;

        Image frame = CreateImage("Frame", root.transform, frameSprite, Color.white);
        Stretch(frame.rectTransform);
        frame.preserveAspect = true;

        Image icon = CreateImage("CenterIcon", root.transform, chevronSprite, new Color32(255, 129, 55, 235));
        SetRect(icon.gameObject, new Vector2(72f, 72f), Vector2.zero);
        icon.preserveAspect = true;

        AnimatedGaugeUI gauge = root.AddComponent<AnimatedGaugeUI>();
        gauge.Configure(fill);
        gauge.SnapTo(0.68f);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, AnomalyPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static void BuildDemoCanvas(
        GameObject playerPrefab,
        GameObject enemyPrefab,
        GameObject anomalyPrefab)
    {
        GameObject canvasObject = new GameObject(
            "ZZZ_HUD_DemoCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform player = InstantiateUiPrefab(playerPrefab, canvasObject.transform);
        player.anchorMin = new Vector2(0f, 1f);
        player.anchorMax = new Vector2(0f, 1f);
        player.pivot = new Vector2(0f, 1f);
        player.anchoredPosition = new Vector2(52f, -42f);

        RectTransform enemy = InstantiateUiPrefab(enemyPrefab, canvasObject.transform);
        enemy.anchorMin = new Vector2(0.5f, 1f);
        enemy.anchorMax = new Vector2(0.5f, 1f);
        enemy.pivot = new Vector2(0.5f, 1f);
        enemy.anchoredPosition = new Vector2(0f, -88f);

        RectTransform anomaly = InstantiateUiPrefab(anomalyPrefab, canvasObject.transform);
        anomaly.anchorMin = new Vector2(1f, 0f);
        anomaly.anchorMax = new Vector2(1f, 0f);
        anomaly.pivot = new Vector2(1f, 0f);
        anomaly.anchoredPosition = new Vector2(-72f, 72f);

        PrefabUtility.SaveAsPrefabAsset(canvasObject, DemoPrefabPath);
        UnityEngine.Object.DestroyImmediate(canvasObject);
    }

    private static GaugeParts CreateGauge(
        string name,
        Transform parent,
        Vector2 size,
        Vector2 position,
        Sprite frameSprite,
        Sprite fillSprite,
        Color fillColor,
        Color? delayedColor,
        float initialValue,
        bool horizontal,
        Vector4 inset)
    {
        GaugeParts raw = CreateRawGauge(
            name,
            parent,
            size,
            position,
            frameSprite,
            fillSprite,
            fillColor,
            initialValue,
            inset,
            delayedColor);

        ConfigureFill(raw.Fill, horizontal);
        ConfigureFill(raw.DelayedFill, horizontal);

        AnimatedGaugeUI gauge = raw.Root.AddComponent<AnimatedGaugeUI>();
        gauge.Configure(raw.Fill, raw.DelayedFill);
        gauge.SnapTo(initialValue);
        return new GaugeParts(raw.Root, raw.Fill, raw.DelayedFill, gauge);
    }

    private static GaugeParts CreateRawGauge(
        string name,
        Transform parent,
        Vector2 size,
        Vector2 position,
        Sprite frameSprite,
        Sprite fillSprite,
        Color fillColor,
        float initialValue,
        Vector4 inset,
        Color? delayedColor = null)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root, size, position);

        if (frameSprite != null)
        {
            Image background = CreateImage("Background", root.transform, frameSprite, Color.white);
            Stretch(background.rectTransform);
            background.type = Image.Type.Sliced;
        }

        Image delayed = null;
        if (delayedColor.HasValue)
        {
            delayed = CreateImage("DelayedFill", root.transform, fillSprite, delayedColor.Value);
            Stretch(delayed.rectTransform, inset);
            ConfigureFill(delayed, true);
            delayed.fillAmount = initialValue;
        }

        Image fill = CreateImage("Fill", root.transform, fillSprite, fillColor);
        Stretch(fill.rectTransform, inset);
        ConfigureFill(fill, true);
        fill.fillAmount = initialValue;

        return new GaugeParts(root, fill, delayed, null);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        gameObject.transform.SetParent(parent, false);

        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static RectTransform InstantiateUiPrefab(GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        return (RectTransform)instance.transform;
    }

    private static void ConfigureFill(Image image, bool horizontal)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Radial360;
        image.fillOrigin = horizontal ? 0 : (int)Image.Origin360.Top;
        image.fillClockwise = true;
    }

    private static void SetRect(GameObject gameObject, Vector2 size, Vector2 position)
    {
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect, Vector4 inset = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset.x, inset.y);
        rect.offsetMax = new Vector2(-inset.z, -inset.w);
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string name)
    {
        string path = $"{SpriteRoot}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException($"Could not import ZZZ HUD sprite: {path}");

        return sprite;
    }

    private static void ReimportSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/Reference/"))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
