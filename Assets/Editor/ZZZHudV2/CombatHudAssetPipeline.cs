using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 파티 전투 HUD 전용 텍스처를 UI Sprite 형식으로 자동 설정한다.
internal sealed class CombatHudTexturePostprocessor : AssetPostprocessor
{
    private const string SpriteRoot = "Assets/Sprites/ZZZHudV2/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteRoot, StringComparison.Ordinal) || assetPath.Contains("/Reference/"))
            return;

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
        importer.maxTextureSize = 2048;
        importer.spriteBorder = Vector4.zero;
    }
}

[InitializeOnLoad]
// 파티 상태·적 월드 상태·체인 스킬 UI와 확인용 Demo Canvas를 생성한다.
internal static class CombatHudPrefabBuilder
{
    private const string SpriteRoot = "Assets/Sprites/ZZZHudV2";
    private const string PrefabRoot = "Assets/Prefabs/UI/ZZZHudV2";
    private const string PlayerPrefabPath = PrefabRoot + "/ZZZ_PlayerPartyHUD.prefab";
    private const string EnemyPrefabPath = PrefabRoot + "/ZZZ_EnemyWorldHUD.prefab";
    private const string ChainPrefabPath = PrefabRoot + "/ZZZ_ChainSkillPrompt.prefab";
    private const string DemoPrefabPath = PrefabRoot + "/ZZZ_HUD_V2_DemoCanvas.prefab";
    private const string SessionKey = "ZZZHudV2PrefabBuilder.AutoBuildAttempted";

    private sealed class GaugeParts
    {
        public GameObject root;
        public Image fill;
        public RectTransform marker;
    }

    static CombatHudPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
    }

    [MenuItem("Tools/ZZZ HUD V2/Build or Refresh Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabRoot);
        ReimportSprites();

        GameObject player = BuildPlayerPartyPrefab();
        GameObject enemy = BuildEnemyWorldPrefab();
        GameObject chain = BuildChainPromptPrefab();
        BuildDemoCanvas(player, enemy, chain);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ZZZ HUD V2 prefabs are ready in {PrefabRoot}");
    }

    [MenuItem("Tools/ZZZ HUD V2/Ping Demo Canvas")]
    private static void PingDemo()
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
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        SessionState.SetBool(SessionKey, true);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath) == null)
            BuildAll();
    }

    private static GameObject BuildPlayerPartyPrefab()
    {
        Sprite activeFrame = LoadSprite("player_active_frame");
        Sprite reserveFrame = LoadSprite("player_reserve_frame");
        Sprite portraitFrame = LoadSprite("player_portrait_frame");
        Sprite hpFrame = LoadSprite("player_hp_frame");
        Sprite hpFill = LoadSprite("player_hp_fill");
        Sprite energyFrame = LoadSprite("player_energy_frame");
        Sprite energyFill = LoadSprite("player_energy_fill");
        Sprite marker = LoadSprite("energy_threshold_marker");
        Sprite reserveSwapIcon = LoadSprite("reserve_swap_icon");
        Sprite emblem = LoadSprite("party_emblem");
        Sprite portraitA = LoadSprite("portrait_placeholder_a");
        Sprite portraitB = LoadSprite("portrait_placeholder_b");
        Sprite portraitC = LoadSprite("portrait_placeholder_c");

        GameObject root = CreateUiObject("ZZZ_PlayerPartyHUD", null);
        SetRect(root, new Vector2(878f, 60f), Vector2.zero);

        PartyStatusUI.SlotView active = BuildActiveSlot(
            root.transform,
            new Vector2(-179f, 0f),
            activeFrame,
            portraitFrame,
            portraitA,
            hpFrame,
            hpFill,
            energyFrame,
            energyFill,
            marker,
            emblem);

        PartyStatusUI.SlotView reserveOne = BuildReserveSlot(
            "ReserveNext",
            root.transform,
            new Vector2(160f, 11f),
            reserveFrame,
            portraitFrame,
            portraitB,
            hpFrame,
            hpFill,
            energyFrame,
            energyFill,
            marker,
            reserveSwapIcon);

        PartyStatusUI.SlotView reserveTwo = BuildReserveSlot(
            "ReservePrevious",
            root.transform,
            new Vector2(344f, 11f),
            reserveFrame,
            portraitFrame,
            portraitC,
            hpFrame,
            hpFill,
            energyFrame,
            energyFill,
            marker,
            reserveSwapIcon);

        PartyStatusUI hud = root.AddComponent<PartyStatusUI>();
        hud.Configure(
            active,
            new[] { reserveOne, reserveTwo },
            new[] { portraitA, portraitB, portraitC });

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static PartyStatusUI.SlotView BuildActiveSlot(
        Transform parent,
        Vector2 position,
        Sprite frameSprite,
        Sprite portraitFrameSprite,
        Sprite portraitSprite,
        Sprite hpFrame,
        Sprite hpFill,
        Sprite energyFrame,
        Sprite energyFill,
        Sprite marker,
        Sprite emblem)
    {
        GameObject root = CreateUiObject("ActiveMember", parent);
        SetRect(root, new Vector2(520f, 60f), position);
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Image background = CreateImage("Frame", root.transform, frameSprite, Color.white);
        Stretch(background.rectTransform);

        Image emblemImage = CreateImage("PartyEmblem", root.transform, emblem, Color.white);
        SetRect(emblemImage.gameObject, new Vector2(66f, 56f), new Vector2(-227f, 0f));

        Image portrait = CreateImage("Portrait", root.transform, portraitSprite, Color.white);
        SetRect(portrait.gameObject, new Vector2(140f, 50f), new Vector2(-137f, 2f));
        portrait.preserveAspect = true;

        Image portraitEdge = CreateImage("PortraitFrame", root.transform, portraitFrameSprite, Color.white);
        SetRect(portraitEdge.gameObject, new Vector2(148f, 56f), new Vector2(-137f, 2f));

        GaugeParts health = CreateGauge(
            "Health",
            root.transform,
            new Vector2(320f, 19f),
            new Vector2(85f, 18f),
            hpFrame,
            hpFill,
            new Vector4(5f, 3f, 4f, 3f),
            false,
            null);
        health.fill.color = Color.white;
        health.fill.fillAmount = 0.82f;

        GaugeParts energy = CreateGauge(
            "Energy",
            root.transform,
            new Vector2(198f, 18f),
            new Vector2(135f, 2f),
            energyFrame,
            energyFill,
            new Vector4(4f, 3f, 4f, 3f),
            true,
            marker);
        energy.fill.color = new Color32(84, 88, 86, 255);
        energy.fill.fillAmount = 0.38f;

        Text healthText = CreateText(
            "HealthText",
            root.transform,
            "5152 / 5152",
            16,
            TextAnchor.MiddleLeft,
            FontStyle.BoldAndItalic,
            Color.white);
        SetRect(healthText.gameObject, new Vector2(125f, 20f), new Vector2(-24f, -18f));
        AddTextOutline(healthText, new Vector2(1.5f, -1.5f));

        return new PartyStatusUI.SlotView
        {
            root = group,
            portrait = portrait,
            healthFill = health.fill,
            energyFill = energy.fill,
            energyThresholdMarker = energy.marker,
            healthText = healthText
        };
    }

    private static PartyStatusUI.SlotView BuildReserveSlot(
        string name,
        Transform parent,
        Vector2 position,
        Sprite frameSprite,
        Sprite portraitFrameSprite,
        Sprite portraitSprite,
        Sprite hpFrame,
        Sprite hpFill,
        Sprite energyFrame,
        Sprite energyFill,
        Sprite marker,
        Sprite swapIcon)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root, new Vector2(190f, 38f), position);
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Image background = CreateImage("Frame", root.transform, frameSprite, Color.white);
        Stretch(background.rectTransform);

        Image portrait = CreateImage("Portrait", root.transform, portraitSprite, Color.white);
        SetRect(portrait.gameObject, new Vector2(78f, 34f), new Vector2(-52f, 1f));
        portrait.preserveAspect = true;

        Image portraitEdge = CreateImage("PortraitFrame", root.transform, portraitFrameSprite, Color.white);
        SetRect(portraitEdge.gameObject, new Vector2(82f, 38f), new Vector2(-52f, 1f));

        GaugeParts health = CreateGauge(
            "Health",
            root.transform,
            new Vector2(100f, 14f),
            new Vector2(40f, 11f),
            hpFrame,
            hpFill,
            new Vector4(3f, 2f, 2f, 2f),
            false,
            null);
        health.fill.fillAmount = 0.91f;

        Image swapReady = CreateImage("SwapReadyIcon", root.transform, swapIcon, Color.white);
        SetRect(swapReady.gameObject, new Vector2(36f, 16f), new Vector2(0f, -10f));

        GaugeParts energy = CreateGauge(
            "Energy",
            root.transform,
            new Vector2(70f, 13f),
            new Vector2(50f, -9f),
            energyFrame,
            energyFill,
            new Vector4(3f, 2f, 3f, 2f),
            true,
            marker);
        energy.fill.color = new Color32(84, 88, 86, 255);
        energy.fill.fillAmount = 0.72f;

        return new PartyStatusUI.SlotView
        {
            root = group,
            portrait = portrait,
            healthFill = health.fill,
            energyFill = energy.fill,
            energyThresholdMarker = energy.marker,
            healthText = null
        };
    }

    private static GameObject BuildEnemyWorldPrefab()
    {
        Sprite frameSprite = LoadSprite("enemy_compact_frame");
        Sprite hpSprite = LoadSprite("enemy_hp_fill");
        Sprite stunSprite = LoadSprite("enemy_stun_fill");
        Sprite anomalySprite = LoadSprite("anomaly_icon_frame");

        GameObject root = CreateUiObject("ZZZ_EnemyWorldHUD", null);
        SetRect(root, new Vector2(166f, 64f), Vector2.zero);

        GameObject visuals = CreateUiObject("Visuals", root.transform);
        Stretch(visuals.GetComponent<RectTransform>());

        Image frame = CreateImage("Frame", visuals.transform, frameSprite, Color.white);
        SetRect(frame.gameObject, new Vector2(132f, 28f), new Vector2(-17f, 8f));

        Image health = CreateImage("HealthFill", visuals.transform, hpSprite, Color.white);
        SetRect(health.gameObject, new Vector2(96f, 10f), new Vector2(-27f, 12f));
        ConfigureHorizontalFill(health, 0.78f);

        Image stun = CreateImage("StunFill", visuals.transform, stunSprite, new Color32(255, 205, 24, 255));
        SetRect(stun.gameObject, new Vector2(75f, 6f), new Vector2(-22f, 4f));
        ConfigureHorizontalFill(stun, 0.64f);

        Text stunPercent = CreateText(
            "StunPercent",
            visuals.transform,
            "64",
            11,
            TextAnchor.MiddleRight,
            FontStyle.BoldAndItalic,
            new Color32(255, 205, 24, 255));
        SetRect(stunPercent.gameObject, new Vector2(28f, 16f), new Vector2(16f, 8f));

        Text damageMultiplier = CreateText(
            "DamageMultiplier",
            visuals.transform,
            "DMG 150%",
            15,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            new Color32(255, 205, 24, 255));
        SetRect(damageMultiplier.gameObject, new Vector2(110f, 22f), new Vector2(-4f, -18f));
        AddTextOutline(damageMultiplier, new Vector2(1.5f, -1.5f));

        GameObject anomalyRoot = CreateUiObject("AnomalyIcon", visuals.transform);
        SetRect(anomalyRoot, new Vector2(44f, 44f), new Vector2(59f, 15f));
        Image anomaly = anomalyRoot.AddComponent<Image>();
        anomaly.sprite = anomalySprite;
        anomaly.raycastTarget = false;

        EnemyWorldStatusUI hud = root.AddComponent<EnemyWorldStatusUI>();
        hud.Configure(health, stun, stunPercent, damageMultiplier, visuals, anomalyRoot);
        hud.ConfigureAutoTarget("Enemy", new Vector3(0f, 1.7f, 0f));

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject BuildChainPromptPrefab()
    {
        Sprite vignetteSprite = LoadSprite("chain_vignette");
        Sprite portraitFrameSprite = LoadSprite("chain_portrait_frame");
        Sprite trackFrameSprite = LoadSprite("chain_track_frame");
        Sprite segmentSprite = LoadSprite("chain_segment_fill");
        Sprite leftSprite = LoadSprite("portrait_placeholder_b");
        Sprite rightSprite = LoadSprite("portrait_placeholder_c");

        GameObject root = CreateUiObject("ZZZ_ChainSkillPrompt", null);
        SetRect(root, new Vector2(1920f, 1080f), Vector2.zero);
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Image vignette = CreateImage("OrangeVignette", root.transform, vignetteSprite, Color.white);
        Stretch(vignette.rectTransform);

        Image track = CreateImage("ChainTrackFrame", root.transform, trackFrameSprite, Color.white);
        SetRect(track.gameObject, new Vector2(820f, 38f), new Vector2(0f, -250f));

        Image timeFill = CreateImage("TimeFill", root.transform, segmentSprite, Color.white);
        SetRect(timeFill.gameObject, new Vector2(560f, 22f), new Vector2(0f, -250f));
        ConfigureHorizontalFill(timeFill, 1f);

        Text chainLabel = CreateText(
            "ChainLabel",
            root.transform,
            "C   H   A   I   N      A   T   T   A   C   K",
            14,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Color.white);
        SetRect(chainLabel.gameObject, new Vector2(560f, 24f), new Vector2(0f, -250f));

        Image leftPortrait = CreatePortraitChoice(
            "LeftChoice",
            root.transform,
            new Vector2(-505f, -215f),
            leftSprite,
            portraitFrameSprite,
            "Q");
        Image rightPortrait = CreatePortraitChoice(
            "RightChoice",
            root.transform,
            new Vector2(505f, -215f),
            rightSprite,
            portraitFrameSprite,
            "E");

        Text timer = CreateText(
            "Timer",
            root.transform,
            "00:01:34",
            64,
            TextAnchor.MiddleCenter,
            FontStyle.BoldAndItalic,
            new Color32(240, 239, 233, 255));
        timer.material = null;
        SetRect(timer.gameObject, new Vector2(430f, 82f), new Vector2(0f, -325f));

        Text cancel = CreateText(
            "CancelHint",
            root.transform,
            "ESC  CANCEL",
            22,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            new Color32(180, 186, 180, 255));
        SetRect(cancel.gameObject, new Vector2(300f, 40f), new Vector2(0f, -382f));

        ChainSkillPromptUI prompt = root.AddComponent<ChainSkillPromptUI>();
        prompt.Configure(group, leftPortrait, rightPortrait, timeFill, timer);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ChainPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    private static Image CreatePortraitChoice(
        string name,
        Transform parent,
        Vector2 position,
        Sprite portraitSprite,
        Sprite frameSprite,
        string key)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root, new Vector2(156f, 190f), position);

        Image portrait = CreateImage("Portrait", root.transform, portraitSprite, Color.white);
        SetRect(portrait.gameObject, new Vector2(126f, 126f), new Vector2(0f, 17f));
        portrait.preserveAspect = true;

        Image frame = CreateImage("Frame", root.transform, frameSprite, Color.white);
        SetRect(frame.gameObject, new Vector2(156f, 156f), new Vector2(0f, 17f));

        Image keyBack = CreateImage("KeyBack", root.transform, frameSprite, new Color32(10, 11, 11, 255));
        SetRect(keyBack.gameObject, new Vector2(38f, 38f), new Vector2(0f, -77f));

        Text keyText = CreateText(
            "Key",
            root.transform,
            key,
            20,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            new Color32(225, 242, 33, 255));
        SetRect(keyText.gameObject, new Vector2(32f, 32f), new Vector2(0f, -77f));
        return portrait;
    }

    private static void BuildDemoCanvas(GameObject playerPrefab, GameObject enemyPrefab, GameObject chainPrefab)
    {
        GameObject canvasObject = new GameObject(
            "ZZZ_HUD_V2_DemoCanvas",
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
        player.anchoredPosition = new Vector2(64f, -48f);

        RectTransform enemy = InstantiateUiPrefab(enemyPrefab, canvasObject.transform);
        enemy.anchorMin = new Vector2(0.5f, 0.5f);
        enemy.anchorMax = new Vector2(0.5f, 0.5f);
        enemy.anchoredPosition = new Vector2(240f, 90f);

        RectTransform chain = InstantiateUiPrefab(chainPrefab, canvasObject.transform);
        Stretch(chain);

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
        Vector4 inset,
        bool addMarker,
        Sprite markerSprite)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root, size, position);

        Image frame = CreateImage("Frame", root.transform, frameSprite, Color.white);
        Stretch(frame.rectTransform);

        Image fill = CreateImage("Fill", root.transform, fillSprite, Color.white);
        Stretch(fill.rectTransform, inset);
        ConfigureHorizontalFill(fill, 1f);

        RectTransform marker = null;
        if (addMarker && markerSprite != null)
        {
            Image markerImage = CreateImage("ReadyThreshold", root.transform, markerSprite, Color.white);
            marker = markerImage.rectTransform;
            marker.anchorMin = new Vector2(0.5f, 0.5f);
            marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(Mathf.Max(14f, size.y * 0.85f), size.y + 6f);
            marker.anchoredPosition = Vector2.zero;
        }

        return new GaugeParts { root = root, fill = fill, marker = marker };
    }

    private static void ConfigureHorizontalFill(Image image, float amount)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.fillAmount = Mathf.Clamp01(amount);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment,
        FontStyle style,
        Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void AddTextOutline(Text text, Vector2 distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
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
            throw new InvalidOperationException($"Could not import ZZZ HUD V2 sprite: {path}");
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
