using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EnemyWorldHudV1Installer
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_EnemyWorldHUD.prefab";
    private const string SpriteRoot =
        "Assets/Sprites/ZZZHudV2/EnemyWorldV1";
    private const string FramePath = SpriteRoot + "/enemy_world_frame_v1.png";
    private const string HealthPath = SpriteRoot + "/enemy_world_hp_v1.png";
    private const string TrailPath = SpriteRoot + "/enemy_world_hp_trail_v1.png";
    private const string StunPath = SpriteRoot + "/enemy_world_stun_v1.png";
    private const string AnomalyBackPath =
        SpriteRoot + "/enemy_world_anomaly_back_v1.png";
    private const string AnomalyFramePath =
        SpriteRoot + "/enemy_world_anomaly_frame_v1.png";
    private const string NumberFontPath =
        "Assets/Resources/Fonts/BarlowCondensed/BarlowCondensed-Black.ttf";

    [MenuItem("Tools/ZZZ HUD V2/Install Enemy World HUD V1")]
    public static void Install()
    {
        ConfigureSprite(FramePath);
        ConfigureSprite(HealthPath);
        ConfigureSprite(TrailPath);
        ConfigureSprite(StunPath);
        ConfigureSprite(AnomalyBackPath);
        ConfigureSprite(AnomalyFramePath);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RectTransform rootRect = RequireRect(root.transform);
            rootRect.sizeDelta = new Vector2(442f, 70f);

            Transform visuals = Require(root.transform, "Visuals");
            Stretch(RequireRect(visuals));
            visuals.localScale = Vector3.one * 0.98f;

            Image frame = RequireImage(visuals, "Frame");
            ConfigureImage(frame, LoadSprite(FramePath), Image.Type.Simple);
            SetRect(
                frame.rectTransform,
                new Vector2(374f, 39.875f),
                new Vector2(0f, 15f));
            frame.transform.SetAsFirstSibling();

            DisableIfPresent(visuals, "HealthOutline");
            DisableIfPresent(visuals, "StunBackground");

            Transform previousTrail = visuals.Find("HealthDamageTrail");
            if (previousTrail != null)
                Object.DestroyImmediate(previousTrail.gameObject);

            Image healthTrail = CreateImage(
                "HealthDamageTrail",
                visuals,
                LoadSprite(TrailPath));
            ConfigureLeftRemainingFill(healthTrail, 1f);
            ConfigureSlantedFillEdge(healthTrail);
            SetRect(
                healthTrail.rectTransform,
                new Vector2(337.5625f, 20f),
                new Vector2(-7.21875f, 17.0625f));
            healthTrail.transform.SetSiblingIndex(2);

            Image health = RequireImage(visuals, "HealthFill");
            ConfigureImage(health, LoadSprite(HealthPath), Image.Type.Filled);
            ConfigureLeftRemainingFill(health, 1f);
            ConfigureSlantedFillEdge(health);
            SetRect(
                health.rectTransform,
                new Vector2(337.5625f, 20f),
                new Vector2(-7.21875f, 17.0625f));
            health.transform.SetSiblingIndex(3);

            Image stun = RequireImage(visuals, "StunFill");
            ConfigureImage(stun, LoadSprite(StunPath), Image.Type.Filled);
            ConfigureLeftRemainingFill(stun, 0f);
            ConfigureSlantedFillEdge(stun);
            SetRect(
                stun.rectTransform,
                new Vector2(299.0625f, 8.25f),
                new Vector2(4.46875f, 10.875f));
            stun.transform.SetSiblingIndex(1);

            Font numberFont = AssetDatabase.LoadAssetAtPath<Font>(NumberFontPath);
            if (numberFont == null)
                throw new MissingReferenceException(NumberFontPath);

            Text stunPercent = RequireText(visuals, "StunPercent");
            ConfigureText(stunPercent, numberFont, 15, TextAnchor.MiddleCenter);
            SetRect(
                stunPercent.rectTransform,
                new Vector2(27f, 22f),
                new Vector2(167.5f, 17f));
            BossHudTextShear shear = stunPercent.GetComponent<BossHudTextShear>();
            if (shear == null)
                shear = stunPercent.gameObject.AddComponent<BossHudTextShear>();
            shear.HorizontalShear = Mathf.Tan(20f * Mathf.Deg2Rad);

            Text damageMultiplier = RequireText(visuals, "DamageMultiplier");
            ConfigureText(
                damageMultiplier,
                numberFont,
                16,
                TextAnchor.MiddleCenter);
            damageMultiplier.supportRichText = true;
            SetRect(
                damageMultiplier.rectTransform,
                new Vector2(120f, 22f),
                new Vector2(0f, -17f));

            Transform anomaly = visuals.Find("AnomalyIcon");
            if (anomaly != null)
            {
                SetRect(
                    RequireRect(anomaly),
                    new Vector2(93.2924f, 79.75f),
                    new Vector2(220.0915f, 35.675f));

                Transform anomalyBack = anomaly.Find("AnomalyBack");
                if (anomalyBack != null)
                {
                    ConfigureImage(
                        RequireImage(anomaly, "AnomalyBack"),
                        LoadSprite(AnomalyBackPath),
                        Image.Type.Simple);
                    Stretch(RequireRect(anomalyBack));
                }

                Transform anomalyFill = anomaly.Find("AnomalyFill");
                if (anomalyFill != null)
                    SetRect(
                        RequireRect(anomalyFill),
                        new Vector2(57.17931f, 57.17931f),
                        new Vector2(7.52356f, -0.75236f));
                Transform anomalyFrame = anomaly.Find("AnomalyFrame");
                if (anomalyFrame != null)
                {
                    ConfigureImage(
                        RequireImage(anomaly, "AnomalyFrame"),
                        LoadSprite(AnomalyFramePath),
                        Image.Type.Simple);
                    Stretch(RequireRect(anomalyFrame));
                }

                Transform elementIcon = anomaly.Find("ElementIcon");
                if (elementIcon != null)
                    SetRect(
                        RequireRect(elementIcon),
                        new Vector2(33.59255f, 33.59255f),
                        new Vector2(7.52356f, -0.75236f));
            }
            EnemyWorldStatusUI hud = root.GetComponent<EnemyWorldStatusUI>();
            if (hud == null)
                throw new MissingComponentException(nameof(EnemyWorldStatusUI));
            hud.ConfigureHealthTrail(healthTrail);

            Transform previousMarker = root.transform.Find("EnemyWorldHudV1Marker");
            if (previousMarker != null)
                Object.DestroyImmediate(previousMarker.gameObject);

            GameObject marker = new GameObject(
                "EnemyWorldHudAnomalyBlackBorderV12Marker",
                typeof(RectTransform));
            marker.transform.SetParent(root.transform, false);
            marker.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Enemy world HUD V1 installed.");
    }

    private static void ConfigureLeftRemainingFill(Image image, float amount)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.fillAmount = Mathf.Clamp01(amount);
    }

    private static void ConfigureSlantedFillEdge(Image image)
    {
        SlantedFillEdgeEffect effect =
            image.GetComponent<SlantedFillEdgeEffect>();
        if (effect == null)
            effect = image.gameObject.AddComponent<SlantedFillEdgeEffect>();

        effect.HorizontalShear = 0.4663f;
    }

    private static void ConfigureText(
        Text text,
        Font font,
        int fontSize,
        TextAnchor alignment)
    {
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Italic;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color32(224, 228, 226, 255);
        text.raycastTarget = false;

        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 255);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        outline.useGraphicAlpha = true;
    }

    private static void ConfigureImage(
        Image image,
        Sprite sprite,
        Image.Type type)
    {
        image.sprite = sprite;
        image.type = type;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        ConfigureImage(image, sprite, Image.Type.Simple);
        return image;
    }

    private static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new MissingReferenceException(path);
        return sprite;
    }

    private static Transform Require(Transform parent, string path)
    {
        Transform result = parent.Find(path);
        if (result == null)
            throw new MissingReferenceException(path);
        return result;
    }

    private static RectTransform RequireRect(Transform transform)
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            throw new MissingComponentException(nameof(RectTransform));
        return rect;
    }

    private static Image RequireImage(Transform parent, string name)
    {
        Transform child = Require(parent, name);
        Image image = child.GetComponent<Image>();
        if (image == null)
            throw new MissingComponentException($"{name}/{nameof(Image)}");
        return image;
    }

    private static Text RequireText(Transform parent, string name)
    {
        Transform child = Require(parent, name);
        Text text = child.GetComponent<Text>();
        if (text == null)
            throw new MissingComponentException($"{name}/{nameof(Text)}");
        return text;
    }

    private static void DisableIfPresent(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            child.gameObject.SetActive(false);
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

[InitializeOnLoad]
internal static class EnemyWorldHudV1AutoInstaller
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_EnemyWorldHUD.prefab";
    private const string MarkerPath = "EnemyWorldHudAnomalyBlackBorderV12Marker";

    static EnemyWorldHudV1AutoInstaller()
    {
        EditorApplication.delayCall += InstallWhenReady;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
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

        EnemyWorldHudV1Installer.Install();
    }
}
