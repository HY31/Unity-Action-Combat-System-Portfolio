using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class CombatHudSourceBackedRefiner
{
    private const string PlayerPrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD.prefab";
    private const string EnemyPrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_EnemyWorldHUD.prefab";
    private const string AssaultPrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string SourceAssetRoot =
        "Assets/Sprites/ZZZHudV2/SourceCombat";
    private const string MarkerName = "SourceBackedHudV3";

    [MenuItem("Tools/ZZZ HUD V2/Apply Source-Backed V3 Layout")]
    public static void RefineAll()
    {
        ConfigureSpriteImporters();
        RefinePlayerPartyHud();
        RefineEnemyWorldHud();
        RefineAssaultInfoHud();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ZZZ HUD V3 source-backed layout applied: party, enemy, timer and score panels.");
    }

    private static void ConfigureSpriteImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceAssetRoot });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    private static void RefinePlayerPartyHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SetSize(root.transform as RectTransform, new Vector2(980f, 92f));

            Transform active = Require(root.transform, "ActiveMember");
            SetRect(active as RectTransform, new Vector2(540f, 82f), new Vector2(-214f, 0f));
            ConfigureActiveMember(active);

            Transform reserveNext = Require(root.transform, "ReserveNext");
            SetRect(reserveNext as RectTransform, new Vector2(190f, 54f), new Vector2(165f, 11f));
            ConfigureReserveMember(reserveNext);

            Transform reservePrevious = Require(root.transform, "ReservePrevious");
            SetRect(reservePrevious as RectTransform, new Vector2(190f, 54f), new Vector2(362f, 11f));
            ConfigureReserveMember(reservePrevious);

            ConfigureSupportPoints(root.transform);
            ConfigurePartyRuntimeColors(root);
            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureActiveMember(Transform active)
    {
        Image frame = RequireImage(active, "Frame");
        ConfigureSimpleImage(frame, LoadSourceSprite("RoleInfoBG01"), new Color32(7, 9, 11, 250));
        SetRect(frame.rectTransform, new Vector2(112f, 80f), new Vector2(-200f, 0f));
        frame.transform.SetAsFirstSibling();

        Image emblem = RequireImage(active, "PartyEmblem");
        SetRect(emblem.rectTransform, new Vector2(56f, 54f), new Vector2(-244f, 0f));

        Image portrait = RequireImage(active, "Portrait");
        SetRect(portrait.rectTransform, new Vector2(150f, 60f), new Vector2(-175f, 2f));
        portrait.preserveAspect = false;

        Image portraitFrame = RequireImage(active, "PortraitFrame");
        SetRect(portraitFrame.rectTransform, new Vector2(158f, 66f), new Vector2(-175f, 2f));

        ConfigureGauge(
            Require(active, "Health"),
            new Vector2(370f, 44f),
            new Vector2(78f, 16f),
            LoadSourceSprite("RoleHPBg01"),
            LoadSourceSprite("RoleHPFill01"),
            new Vector4(5f, 5f, 5f, 5f));

        ConfigureGauge(
            Require(active, "Energy"),
            new Vector2(258f, 18f),
            new Vector2(133f, -20f),
            LoadSourceSprite("RoleSPBg01"),
            LoadSourceSprite("RoleSPFill01"),
            new Vector4(3f, 3f, 3f, 3f));

        Image threshold = active.Find("Energy/ReadyThreshold")?.GetComponent<Image>();
        if (threshold != null)
        {
            threshold.sprite = LoadSourceSprite("RoleSPFillPoint");
            threshold.rectTransform.sizeDelta = new Vector2(22f, 20f);
            threshold.transform.SetAsLastSibling();
        }

        Text healthText = active.Find("HealthText")?.GetComponent<Text>();
        if (healthText != null)
        {
            healthText.fontSize = 15;
            healthText.alignment = TextAnchor.MiddleLeft;
            SetRect(healthText.rectTransform, new Vector2(130f, 20f), new Vector2(-55f, -22f));
        }
    }

    private static void ConfigureReserveMember(Transform reserve)
    {
        Image frame = RequireImage(reserve, "Frame");
        ConfigureSimpleImage(frame, LoadSourceSprite("RoleInfoBG02"), new Color32(8, 10, 12, 248));
        SetRect(frame.rectTransform, new Vector2(86f, 54f), new Vector2(-50f, 1f));
        frame.transform.SetAsFirstSibling();

        Image portrait = RequireImage(reserve, "Portrait");
        SetRect(portrait.rectTransform, new Vector2(86f, 42f), new Vector2(-49f, 2f));
        portrait.preserveAspect = false;

        Image portraitFrame = RequireImage(reserve, "PortraitFrame");
        SetRect(portraitFrame.rectTransform, new Vector2(92f, 47f), new Vector2(-49f, 2f));

        ConfigureGauge(
            Require(reserve, "Health"),
            new Vector2(112f, 16f),
            new Vector2(40f, 10f),
            LoadSourceSprite("RoleHPBg02"),
            LoadSourceSprite("RoleHPFill02"),
            new Vector4(2f, 2f, 2f, 2f));

        ConfigureGauge(
            Require(reserve, "Energy"),
            new Vector2(86f, 14f),
            new Vector2(51f, -10f),
            LoadSourceSprite("RoleSPBg02"),
            LoadSourceSprite("RoleSPFill02"),
            new Vector4(2f, 2f, 2f, 2f));

        Image threshold = reserve.Find("Energy/ReadyThreshold")?.GetComponent<Image>();
        if (threshold != null)
        {
            threshold.sprite = LoadSourceSprite("RoleSPFillPoint");
            threshold.rectTransform.sizeDelta = new Vector2(16f, 16f);
            threshold.transform.SetAsLastSibling();
        }

        Image swap = reserve.Find("SwapReadyIcon")?.GetComponent<Image>();
        if (swap != null)
            SetRect(swap.rectTransform, new Vector2(28f, 16f), new Vector2(13f, -11f));
    }

    private static void ConfigureSupportPoints(Transform playerRoot)
    {
        Sprite supportSprite = LoadSourceSprite("IconRoleSPPoint");
        for (int index = 0; index < 6; index++)
        {
            Transform pipTransform = playerRoot.Find($"SupportPoint_{index + 1:00}");
            Image pip = pipTransform != null ? pipTransform.GetComponent<Image>() : null;
            if (pip == null)
                continue;

            pip.sprite = supportSprite;
            pip.preserveAspect = true;
            SetRect(pip.rectTransform, new Vector2(14f, 17f), new Vector2(-402f + index * 18f, -36f));
        }
    }

    private static void ConfigurePartyRuntimeColors(GameObject playerRoot)
    {
        PartyStatusUI partyStatus = playerRoot.GetComponent<PartyStatusUI>();
        if (partyStatus == null)
            return;

        SerializedObject serialized = new SerializedObject(partyStatus);
        SetColorProperty(serialized, "energyNormalColor", Color.white);
        SetColorProperty(serialized, "energyReadyColor", Color.white);
        SetColorProperty(serialized, "energyMarkerNormalColor", new Color32(92, 96, 94, 255));
        SetColorProperty(serialized, "energyMarkerReadyColor", new Color32(255, 198, 26, 255));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RefineEnemyWorldHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            SetSize(root.transform as RectTransform, new Vector2(194f, 66f));
            Transform visuals = Require(root.transform, "Visuals");

            Image frame = RequireImage(visuals, "Frame");
            ConfigureSimpleImage(frame, LoadSourceSprite("MonsteHPBg"), new Color32(6, 8, 9, 248));
            SetRect(frame.rectTransform, new Vector2(154f, 28f), new Vector2(-10f, 9f));
            frame.transform.SetAsFirstSibling();

            Image healthOutline = EnsureImage(visuals, "HealthOutline");
            ConfigureSimpleImage(healthOutline, LoadSourceSprite("RoleHPBg02"), new Color32(235, 239, 236, 255));
            SetRect(healthOutline.rectTransform, new Vector2(126f, 15f), new Vector2(-21f, 14f));
            healthOutline.transform.SetSiblingIndex(1);

            Image healthFill = RequireImage(visuals, "HealthFill");
            ConfigureFillImage(healthFill, LoadSourceSprite("RoleHPFill02"), Color.white);
            SetRect(healthFill.rectTransform, new Vector2(120f, 10f), new Vector2(-22f, 14f));
            healthFill.transform.SetSiblingIndex(2);

            Image stunBackground = EnsureImage(visuals, "StunBackground");
            ConfigureSimpleImage(stunBackground, LoadSourceSprite("MonsterStunBg22"), new Color32(8, 10, 11, 250));
            SetRect(stunBackground.rectTransform, new Vector2(116f, 8f), new Vector2(-18f, 3f));
            stunBackground.transform.SetSiblingIndex(3);

            Image stunFill = RequireImage(visuals, "StunFill");
            ConfigureFillImage(stunFill, LoadSourceSprite("MonsterStunFill"), new Color32(255, 205, 24, 255));
            SetRect(stunFill.rectTransform, new Vector2(110f, 6f), new Vector2(-20f, 3f));
            stunFill.transform.SetSiblingIndex(4);

            Text stunPercent = visuals.Find("StunPercent")?.GetComponent<Text>();
            if (stunPercent != null)
            {
                stunPercent.fontSize = 14;
                SetRect(stunPercent.rectTransform, new Vector2(36f, 20f), new Vector2(41f, 8f));
            }

            Text damageMultiplier = visuals.Find("DamageMultiplier")?.GetComponent<Text>();
            if (damageMultiplier != null)
            {
                damageMultiplier.fontSize = 15;
                SetRect(damageMultiplier.rectTransform, new Vector2(118f, 22f), new Vector2(0f, -19f));
            }

            Transform anomaly = visuals.Find("AnomalyIcon");
            if (anomaly != null)
                SetRect(anomaly as RectTransform, new Vector2(36f, 36f), new Vector2(74f, 11f));

            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RefineAssaultInfoHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(AssaultPrefabPath);
        try
        {
            Transform combat = Require(root.transform, "CombatView");
            Transform timer = combat.Find("TimerPanel");
            if (timer != null)
            {
                SetAnchoredTopRight(timer as RectTransform, new Vector2(332f, 52f), new Vector2(-42f, -122f));
                SetRect(timer.Find("FairyTimerBackground") as RectTransform, new Vector2(304f, 38f), new Vector2(-9f, 0f));
                SetRect(timer.Find("ProgressTrack") as RectTransform, new Vector2(238f, 18f), new Vector2(-29f, 9f));
                SetRect(timer.Find("Timer") as RectTransform, new Vector2(120f, 26f), new Vector2(57f, -10f));
                SetRect(timer.Find("TimeIcon") as RectTransform, new Vector2(46f, 46f), new Vector2(137f, 0f));
            }

            Transform score = combat.Find("ScorePanel");
            if (score != null)
            {
                SetAnchoredTopRight(score as RectTransform, new Vector2(372f, 52f), new Vector2(-42f, -180f));
                SetRect(score.Find("FairyScoreBackground") as RectTransform, new Vector2(344f, 38f), new Vector2(-9f, 0f));
                SetRect(score.Find("LeftCap") as RectTransform, new Vector2(54f, 36f), new Vector2(-164f, 0f));
                SetRect(score.Find("RightCap") as RectTransform, new Vector2(28f, 34f), new Vector2(151f, 0f));
                SetRect(score.Find("RankMedals") as RectTransform, new Vector2(72f, 26f), new Vector2(-119f, 0f));
                SetRect(score.Find("Score") as RectTransform, new Vector2(218f, 30f), new Vector2(23f, 0f));
                SetRect(score.Find("FairyIconBackground") as RectTransform, new Vector2(50f, 48f), new Vector2(164f, 0f));
                SetRect(score.Find("FairyIcon") as RectTransform, new Vector2(38f, 38f), new Vector2(163f, 0f));
            }

            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, AssaultPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureGauge(
        Transform gauge,
        Vector2 size,
        Vector2 position,
        Sprite backgroundSprite,
        Sprite fillSprite,
        Vector4 fillInset)
    {
        SetRect(gauge as RectTransform, size, position);

        Image outline = RequireImage(gauge, "Frame");
        ConfigureSimpleImage(outline, backgroundSprite, new Color32(226, 232, 228, 255));
        Stretch(outline.rectTransform);
        outline.transform.SetAsFirstSibling();

        Image track = EnsureImage(gauge, "SourceTrack");
        ConfigureSimpleImage(track, backgroundSprite, new Color32(7, 9, 10, 255));
        Stretch(track.rectTransform, new Vector4(2f, 2f, 2f, 2f));
        track.transform.SetSiblingIndex(1);

        Image fill = RequireImage(gauge, "Fill");
        ConfigureFillImage(fill, fillSprite, Color.white);
        Stretch(fill.rectTransform, fillInset);
        fill.transform.SetSiblingIndex(2);
    }

    private static void ConfigureSimpleImage(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static void ConfigureFillImage(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static Sprite LoadSourceSprite(string name)
    {
        string path = SourceAssetRoot + "/" + name + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        if (sprite == null)
            throw new InvalidOperationException($"ZZZ HUD V3 sprite could not be loaded: {path}");
        return sprite;
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root.Find(path);
        if (result == null)
            throw new InvalidOperationException($"ZZZ HUD V3 hierarchy path was not found: {root.name}/{path}");
        return result;
    }

    private static Image RequireImage(Transform root, string path)
    {
        Transform target = Require(root, path);
        Image image = target.GetComponent<Image>();
        if (image == null)
            throw new InvalidOperationException($"ZZZ HUD V3 Image was not found: {root.name}/{path}");
        return image;
    }

    private static Image EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image existingImage = existing.GetComponent<Image>();
            if (existingImage != null)
                return existingImage;
        }

        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<Image>();
    }

    private static void EnsureMarker(Transform root)
    {
        if (root.Find(MarkerName) != null)
            return;

        GameObject marker = new GameObject(MarkerName, typeof(RectTransform));
        marker.transform.SetParent(root, false);
        marker.hideFlags = HideFlags.HideInHierarchy;
    }

    private static void SetColorProperty(SerializedObject serialized, string propertyName, Color color)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.colorValue = color;
    }

    private static void SetSize(RectTransform rect, Vector2 size)
    {
        if (rect == null)
            return;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetAnchoredTopRight(RectTransform rect, Vector2 size, Vector2 position)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
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
}
