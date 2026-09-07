using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class CombatHudExtractedAssetAssemblerV4
{
    private const string PlayerPrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_EnemyWorldHUD.prefab";
    private const string AssaultPrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string SourceRoot = "Assets/Sprites/ZZZHudV2/SourceCombat";
    private const string FairyRoot = "Assets/Sprites/ZZZHudV2/AssaultFairy";
    private const string MarkerName = "ExtractedAssetHudV4";

    private static readonly Color32 PanelBlack = new Color32(7, 9, 11, 248);
    private static readonly Color32 TrackBlack = new Color32(22, 24, 25, 255);

    [MenuItem("Tools/ZZZ HUD V2/Assemble Extracted HUD V4")]
    public static void AssembleAll()
    {
        ConfigureImporters();
        AssemblePlayer();
        AssembleEnemy();
        AssembleAssault();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ZZZ HUD V4 assembled from extracted source pieces.");
    }

    private static void ConfigureImporters()
    {
        string[] roots = { SourceRoot, FairyRoot };
        foreach (string root in roots)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName == "FairyTime_Pro" || fileName == "FairyTime_ProLine")
                    importer.spriteBorder = new Vector4(13f, 8f, 13f, 8f);
                else if (fileName == "ProBg02")
                    importer.spriteBorder = new Vector4(15f, 10f, 15f, 10f);

                importer.SaveAndReimport();
            }
        }
    }

    private static void AssemblePlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SetSize(root.transform as RectTransform, new Vector2(720f, 84f));

            Transform active = Require(root.transform, "ActiveMember");
            SetRect(active as RectTransform, new Vector2(404f, 82f), new Vector2(-140f, 0f));
            ConfigureActive(active);

            Transform next = Require(root.transform, "ReserveNext");
            SetRect(next as RectTransform, new Vector2(158f, 56f), new Vector2(125f, 11f));
            ConfigureReserve(next);

            Transform previous = Require(root.transform, "ReservePrevious");
            SetRect(previous as RectTransform, new Vector2(158f, 56f), new Vector2(282f, 11f));
            ConfigureReserve(previous);

            Sprite pipSprite = Source("IconRoleSPPoint");
            for (int i = 0; i < 6; i++)
            {
                Transform pipTransform = root.transform.Find("SupportPoint_" + (i + 1).ToString("00"));
                Image pip = pipTransform != null ? pipTransform.GetComponent<Image>() : null;
                if (pip == null)
                    continue;
                SetSimple(pip, pipSprite, Color.white, true);
                SetRect(pip.rectTransform, new Vector2(14f, 17f), new Vector2(-310f + i * 17f, -32f));
            }

            PartyStatusUI status = root.GetComponent<PartyStatusUI>();
            if (status != null)
            {
                SerializedObject serialized = new SerializedObject(status);
                SetColor(serialized, "energyNormalColor", Color.white);
                SetColor(serialized, "energyReadyColor", Color.white);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureActive(Transform slot)
    {
        Image frame = RequireImage(slot, "Frame");
        SetSimple(frame, Source("RoleInfoBG01"), PanelBlack);
        SetRect(frame.rectTransform, new Vector2(84f, 78f), new Vector2(-157f, 0f));
        frame.transform.SetAsFirstSibling();

        ConfigurePortrait(
            slot,
            Source("RoleIconBG01"),
            Source("RoleIconBGLine"),
            new Vector2(70f, 62f),
            new Vector2(-157f, 0f),
            new Vector2(112f, 64f));

        Image emblem = RequireImage(slot, "PartyEmblem");
        SetRect(emblem.rectTransform, new Vector2(52f, 54f), new Vector2(-201f, 0f));
        emblem.preserveAspect = true;

        ConfigureGauge(slot, "Health", new Vector2(302f, 36f), new Vector2(50f, 18f),
            Source("RoleHPBg01"), Source("RoleHPFill01"));
        ConfigureGauge(slot, "Energy", new Vector2(144f, 14f), new Vector2(114f, -12f),
            Source("RoleSPBg01"), Source("RoleSPFill01"));

        Image marker = FindImage(slot, "ReadyThreshold");
        if (marker != null)
        {
            SetSimple(marker, Source("RoleSPFillPoint"), Color.white, true);
            marker.rectTransform.sizeDelta = new Vector2(24f, 14f);
            marker.transform.SetAsLastSibling();
        }

        Text healthText = FindDeep(slot, "HealthText")?.GetComponent<Text>();
        if (healthText != null)
        {
            healthText.fontSize = 13;
            healthText.alignment = TextAnchor.MiddleRight;
            SetRect(healthText.rectTransform, new Vector2(92f, 16f), new Vector2(-48f, -25f));
        }
    }

    private static void ConfigureReserve(Transform slot)
    {
        Image frame = RequireImage(slot, "Frame");
        SetSimple(frame, Source("RoleInfoBG02"), PanelBlack);
        SetRect(frame.rectTransform, new Vector2(60f, 52f), new Vector2(-50f, 0f));
        frame.transform.SetAsFirstSibling();

        ConfigurePortrait(
            slot,
            Source("RoleInfoBG02"),
            Source("RoleIconBGOutline02"),
            new Vector2(60f, 52f),
            new Vector2(-50f, 0f),
            new Vector2(82f, 52f));

        ConfigureGauge(slot, "Health", new Vector2(100f, 14f), new Vector2(30f, 10f),
            Source("RoleHPBg02"), Source("RoleHPFill02"));
        ConfigureGauge(slot, "Energy", new Vector2(100f, 14f), new Vector2(30f, -10f),
            Source("RoleSPBg02"), Source("RoleSPFill02"));

        Image marker = FindImage(slot, "ReadyThreshold");
        if (marker != null)
        {
            SetSimple(marker, Source("RoleSPFillPoint"), Color.white, true);
            marker.rectTransform.sizeDelta = new Vector2(16f, 14f);
            marker.transform.SetAsLastSibling();
        }

        Image swap = FindImage(slot, "SwapReadyIcon");
        if (swap != null)
            SetRect(swap.rectTransform, new Vector2(24f, 16f), new Vector2(-4f, -15f));
    }

    private static void ConfigurePortrait(
        Transform slot,
        Sprite maskSprite,
        Sprite frameSprite,
        Vector2 maskSize,
        Vector2 maskPosition,
        Vector2 portraitSize)
    {
        Image clipImage = EnsureImage(slot, "PortraitClipV4");
        SetSimple(clipImage, maskSprite, Color.white);
        SetRect(clipImage.rectTransform, maskSize, maskPosition);
        Mask mask = clipImage.GetComponent<Mask>();
        if (mask == null)
            mask = clipImage.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        Transform portrait = FindDeep(slot, "Portrait");
        if (portrait == null)
            throw new InvalidOperationException("Portrait was not found in " + slot.name);
        portrait.SetParent(clipImage.transform, false);
        SetRect(portrait as RectTransform, portraitSize, Vector2.zero);
        Image portraitImage = portrait.GetComponent<Image>();
        if (portraitImage != null)
            portraitImage.preserveAspect = false;

        Image edge = FindImage(slot, "PortraitFrame");
        if (edge != null)
        {
            edge.transform.SetParent(slot, false);
            SetSimple(edge, frameSprite, Color.white, true);
            SetRect(edge.rectTransform, maskSize, maskPosition);
            edge.transform.SetAsLastSibling();
        }
    }

    private static void ConfigureGauge(
        Transform slot,
        string gaugeName,
        Vector2 size,
        Vector2 position,
        Sprite backgroundSprite,
        Sprite fillSprite)
    {
        Transform gauge = Require(slot, gaugeName);
        SetRect(gauge as RectTransform, size, position);

        Transform oldTrack = gauge.Find("SourceTrack");
        if (oldTrack != null)
            UnityEngine.Object.DestroyImmediate(oldTrack.gameObject);

        Image background = RequireImage(gauge, "Frame");
        SetSimple(background, backgroundSprite, TrackBlack);
        Stretch(background.rectTransform);
        background.transform.SetAsFirstSibling();

        Image fill = RequireImage(gauge, "Fill");
        SetFill(fill, fillSprite, Color.white);
        Stretch(fill.rectTransform);
        fill.transform.SetSiblingIndex(1);
    }

    private static void AssembleEnemy()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            SetSize(root.transform as RectTransform, new Vector2(174f, 54f));
            Transform visuals = Require(root.transform, "Visuals");

            Image frame = RequireImage(visuals, "Frame");
            SetSimple(frame, Source("MonsteHPBg"), PanelBlack);
            SetRect(frame.rectTransform, new Vector2(130f, 22f), new Vector2(-5f, 9f));
            frame.transform.SetAsFirstSibling();

            Transform obsoleteOutline = visuals.Find("HealthOutline");
            if (obsoleteOutline != null)
                obsoleteOutline.gameObject.SetActive(false);

            Image health = RequireImage(visuals, "HealthFill");
            SetFill(health, Source("RoleHPFill02"), Color.white);
            SetRect(health.rectTransform, new Vector2(100f, 14f), new Vector2(-17f, 10f));

            Image stunBackground = EnsureImage(visuals, "StunBackground");
            SetSimple(stunBackground, Source("MonsterStunBg22"), TrackBlack);
            SetRect(stunBackground.rectTransform, new Vector2(120f, 8f), new Vector2(-5f, -3f));

            Image stun = RequireImage(visuals, "StunFill");
            SetFill(stun, Source("MonsterStunFill"), Color.white);
            SetRect(stun.rectTransform, new Vector2(86f, 8f), new Vector2(-22f, -3f));

            Text percent = FindDeep(visuals, "StunPercent")?.GetComponent<Text>();
            if (percent != null)
                SetRect(percent.rectTransform, new Vector2(34f, 18f), new Vector2(50f, 8f));

            Transform anomaly = FindDeep(visuals, "AnomalyIcon");
            if (anomaly != null)
                SetRect(anomaly as RectTransform, new Vector2(34f, 34f), new Vector2(72f, 9f));

            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssembleAssault()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(AssaultPrefabPath);
        try
        {
            Transform combat = Require(root.transform, "CombatView");
            AssembleBoss(combat);
            AssembleTimer(combat);
            AssembleScore(combat);
            EnsureMarker(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, AssaultPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssembleBoss(Transform combat)
    {
        Transform panel = Require(combat, "BossStatusPanel");
        SetTopRight(panel as RectTransform, new Vector2(620f, 78f), new Vector2(-28f, -18f));
        Transform sourceFrame = Require(panel, "SourceFrame");

        Image outer = RequireImage(sourceFrame, "BossOuterMask");
        SetSimple(outer, Source("BossHPFill01"), PanelBlack);
        SetRect(outer.rectTransform, new Vector2(511f, 63f), new Vector2(-20f, 0f));

        Image healthBackground = RequireImage(sourceFrame, "HealthBackground");
        SetSimple(healthBackground, Source("MonsteBossHPBg"), new Color32(40, 43, 44, 255));
        SetRect(healthBackground.rectTransform, new Vector2(406f, 39f), new Vector2(-66f, 10f));
        healthBackground.rectTransform.localScale = Vector3.one;

        Transform healthTrack = Require(sourceFrame, "Health");
        SetRect(healthTrack as RectTransform, new Vector2(392f, 14f), new Vector2(-68f, 11f));
        Image healthFill = RequireImage(healthTrack, "Fill");
        SetFill(healthFill, Source("BossHPFill02"), Color.white);
        Stretch(healthFill.rectTransform);

        Image stunBackground = RequireImage(sourceFrame, "StunBackground");
        SetSimple(stunBackground, Source("MonsterBossStunFill"), TrackBlack);
        SetRect(stunBackground.rectTransform, new Vector2(376f, 16f), new Vector2(-74f, -14f));

        Transform stunTrack = Require(sourceFrame, "Stun");
        SetRect(stunTrack as RectTransform, new Vector2(376f, 8f), new Vector2(-74f, -14f));
        Image stunFill = RequireImage(stunTrack, "Fill");
        SetFill(stunFill, Source("MonsterBossStunFill"), new Color32(255, 205, 24, 255));
        Stretch(stunFill.rectTransform);

        Text stunText = FindDeep(sourceFrame, "StunText")?.GetComponent<Text>();
        if (stunText != null)
            SetRect(stunText.rectTransform, new Vector2(52f, 42f), new Vector2(-292f, 8f));

        Image portraitMask = RequireImage(sourceFrame, "PortraitMask");
        SetSimple(portraitMask, Source("BossInfoBG"), Color.white);
        SetRect(portraitMask.rectTransform, new Vector2(88f, 78f), new Vector2(251f, 0f));
        Mask mask = portraitMask.GetComponent<Mask>();
        if (mask == null)
            mask = portraitMask.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        Image plate = EnsureImage(sourceFrame, "PortraitPlateV4");
        SetSimple(plate, Source("BossInfoBG"), PanelBlack);
        SetRect(plate.rectTransform, new Vector2(88f, 78f), new Vector2(251f, 0f));
        plate.transform.SetSiblingIndex(Mathf.Max(0, portraitMask.transform.GetSiblingIndex()));

        Transform portrait = FindDeep(portraitMask.transform, "BossPortrait");
        if (portrait != null)
        {
            SetRect(portrait as RectTransform, new Vector2(110f, 152f), new Vector2(0f, -31f));
            Image portraitImage = portrait.GetComponent<Image>();
            if (portraitImage != null)
                portraitImage.preserveAspect = true;
        }
        portraitMask.transform.SetAsLastSibling();
    }

    private static void AssembleTimer(Transform combat)
    {
        Transform panel = Require(combat, "TimerPanel");
        SetTopRight(panel as RectTransform, new Vector2(336f, 48f), new Vector2(-36f, -94f));

        Image background = RequireImage(panel, "FairyTimerBackground");
        SetSliced(background, Fairy("FairyTime_Pro"), new Color32(17, 19, 21, 250));
        SetRect(background.rectTransform, new Vector2(300f, 34f), new Vector2(-9f, 0f));

        Transform track = Require(panel, "ProgressTrack");
        SetRect(track as RectTransform, new Vector2(246f, 16f), new Vector2(-28f, 6f));
        Image trackBackground = RequireImage(track, "Background");
        SetSliced(trackBackground, Fairy("FairyTime_Pro"), new Color32(70, 48, 45, 255));
        Stretch(trackBackground.rectTransform);

        Image fill = RequireImage(track, "ProgressFill");
        SetFill(fill, Fairy("FairyTime_Pro"), new Color32(238, 76, 40, 255));
        Stretch(fill.rectTransform, new Vector4(3f, 3f, 3f, 3f));

        Image line = RequireImage(track, "Line");
        SetSliced(line, Fairy("FairyTime_ProLine"), new Color32(38, 41, 42, 255));
        Stretch(line.rectTransform);

        RectTransform timer = Require(panel, "Timer") as RectTransform;
        SetRect(timer, new Vector2(112f, 24f), new Vector2(58f, -8f));
        Image icon = RequireImage(panel, "TimeIcon");
        SetSimple(icon, Fairy("IconTime"), Color.white, true);
        SetRect(icon.rectTransform, new Vector2(44f, 44f), new Vector2(143f, 0f));
    }

    private static void AssembleScore(Transform combat)
    {
        Transform panel = Require(combat, "ScorePanel");
        SetTopRight(panel as RectTransform, new Vector2(396f, 46f), new Vector2(-36f, -142f));

        Image background = RequireImage(panel, "FairyScoreBackground");
        SetSliced(background, Fairy("ProBg02"), new Color32(15, 17, 19, 250));
        SetRect(background.rectTransform, new Vector2(338f, 34f), new Vector2(-10f, 0f));

        Image left = RequireImage(panel, "LeftCap");
        SetSimple(left, Fairy("ProBg01"), new Color32(29, 32, 34, 255), true);
        SetRect(left.rectTransform, new Vector2(44f, 24f), new Vector2(-174f, 0f));

        Image right = RequireImage(panel, "RightCap");
        SetSimple(right, Fairy("ProBg04"), new Color32(15, 17, 19, 255), true);
        SetRect(right.rectTransform, new Vector2(28f, 32f), new Vector2(159f, 0f));

        SetRect(Require(panel, "RankMedals") as RectTransform, new Vector2(76f, 26f), new Vector2(-128f, 0f));
        SetRect(Require(panel, "Score") as RectTransform, new Vector2(216f, 28f), new Vector2(20f, 0f));

        Image fairyBackground = RequireImage(panel, "FairyIconBackground");
        SetSimple(fairyBackground, Fairy("FairyIconBG02"), Color.white, true);
        SetRect(fairyBackground.rectTransform, new Vector2(48f, 45f), new Vector2(170f, 0f));

        Image fairyIcon = RequireImage(panel, "FairyIcon");
        SetRect(fairyIcon.rectTransform, new Vector2(36f, 36f), new Vector2(169f, 0f));
        fairyIcon.preserveAspect = true;
    }

    private static Sprite Source(string name)
    {
        return LoadSprite(SourceRoot + "/" + name + ".png");
    }

    private static Sprite Fairy(string name)
    {
        return LoadSprite(FairyRoot + "/" + name + ".png");
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        if (sprite == null)
            throw new InvalidOperationException("Extracted HUD sprite was not found: " + path);
        return sprite;
    }

    private static Transform Require(Transform root, string name)
    {
        Transform result = root.Find(name);
        if (result == null)
            throw new InvalidOperationException("HUD hierarchy path was not found: " + root.name + "/" + name);
        return result;
    }

    private static Image RequireImage(Transform root, string name)
    {
        Transform target = Require(root, name);
        Image image = target.GetComponent<Image>();
        if (image == null)
            throw new InvalidOperationException("Image was not found: " + root.name + "/" + name);
        return image;
    }

    private static Image FindImage(Transform root, string name)
    {
        return FindDeep(root, name)?.GetComponent<Image>();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    private static Image EnsureImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            if (found != null)
                return found;
        }
        GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        created.transform.SetParent(parent, false);
        return created.GetComponent<Image>();
    }

    private static void SetSimple(Image image, Sprite sprite, Color color, bool preserveAspect = false)
    {
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
    }

    private static void SetSliced(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static void SetFill(Image image, Sprite sprite, Color color)
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

    private static void SetColor(SerializedObject serialized, string propertyName, Color color)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.colorValue = color;
    }

    private static void EnsureMarker(Transform root)
    {
        if (root.Find(MarkerName) != null)
            return;
        GameObject marker = new GameObject(MarkerName, typeof(RectTransform));
        marker.transform.SetParent(root, false);
        marker.hideFlags = HideFlags.HideInHierarchy;
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

    private static void SetTopRight(RectTransform rect, Vector2 size, Vector2 position)
    {
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
