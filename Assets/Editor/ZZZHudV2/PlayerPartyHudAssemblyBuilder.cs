using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayerPartyHudAssemblyBuilder
{
    private const string MenuPath =
        "Tools/ZZZ HUD V2/Rebuild Player Party HUD Assembly (Overwrite Layout)";
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD_Assembly.prefab";
    private const string MaterialPath =
        "Assets/Materials/UI/PlayerHudAlphaTint.mat";
    private const string ShaderName = "UI/Player HUD Alpha Tint";
    private const string PauseMaterialPath =
        "Assets/Materials/UI/PlayerHudPauseApproved.mat";
    private const string ExtractedRoot =
        "Assets/Sprites/ZZZHudV2/PlayerPartyV3/Extracted/";
    private const string PortraitRoot = "Assets/Sprites/ZZZHudV2/";

    private static readonly Vector2 RootSize = new Vector2(1600f, 122f);
    private static int remainingAutoAttempts = 20;

    static PlayerPartyHudAssemblyBuilder()
    {
        EditorApplication.delayCall += AutoBuildIfMissing;
    }

    [MenuItem(MenuPath)]
    public static void BuildReset()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog(
                "Player Party HUD 재생성",
                "프리팹에서 직접 조정한 배치를 초기값으로 덮어씁니다. 계속할까요?",
                "덮어쓰기",
                "취소"))
        {
            return;
        }

        if (!BuildPrefab())
            Debug.LogError("[PlayerPartyHudAssemblyBuilder] 조립용 HUD 프리팹 생성에 실패했습니다.");
    }

    private static void AutoBuildIfMissing()
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            QueueRetry();
            return;
        }

        if (!BuildPrefab())
            QueueRetry();
    }

    private static void QueueRetry()
    {
        remainingAutoAttempts--;
        if (remainingAutoAttempts > 0)
            EditorApplication.delayCall += AutoBuildIfMissing;
    }

    private static bool BuildPrefab()
    {
        Sprite frame = LoadSprite(ExtractedRoot + "player_party_frame_extracted.png");
        Sprite pause = LoadSprite(ExtractedRoot + "player_pause_button_approved.png");
        Sprite activeHealthBackground = LoadSprite(
            ExtractedRoot + "player_hp_bg_active_gray.png");
        Sprite reserveHealthBackground = LoadSprite(
            ExtractedRoot + "player_hp_bg_reserve_gray.png");
        Sprite activeHealthFill = LoadSprite(ExtractedRoot + "player_hp_fill_active.png");
        Sprite reserveHealthFill = LoadSprite(ExtractedRoot + "player_hp_fill_reserve.png");
        Sprite activeEnergyFill = LoadSprite(ExtractedRoot + "player_energy_fill_active.png");
        Sprite reserveEnergyFill = LoadSprite(ExtractedRoot + "player_energy_fill_reserve.png");
        Sprite threshold = LoadSprite(ExtractedRoot + "player_energy_threshold.png");
        Sprite ultimate = LoadSprite(ExtractedRoot + "player_ultimate_ready.png");
        Sprite ellen = LoadSprite(PortraitRoot + "ellen_health_portrait.png");
        Sprite jane = LoadSprite(PortraitRoot + "jane_health_portrait.png");
        Sprite corin = LoadSprite(PortraitRoot + "corin_health_portrait.png");

        Sprite[] requiredSprites =
        {
            frame,
            pause,
            activeHealthBackground,
            reserveHealthBackground,
            activeHealthFill,
            reserveHealthFill,
            activeEnergyFill,
            reserveEnergyFill,
            threshold,
            ultimate,
            ellen,
            jane,
            corin
        };

        foreach (Sprite sprite in requiredSprites)
        {
            if (sprite == null)
            {
                Debug.LogWarning(
                    "[PlayerPartyHudAssemblyBuilder] 아직 임포트되지 않은 HUD 스프라이트가 있습니다.");
                return false;
            }
        }

        Material alphaTintMaterial = GetOrCreateAlphaTintMaterial();
        Material pauseMaterial = AssetDatabase.LoadAssetAtPath<Material>(PauseMaterialPath);
        if (alphaTintMaterial == null || pauseMaterial == null)
            return false;

        EnsureAssetFolder("Assets/Prefabs/UI/ZZZHudV2");

        GameObject root = null;
        try
        {
            root = new GameObject(
                "ZZZ_PlayerPartyHUD_Assembly",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(PlayerPartyHudAssemblyPresenter));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(64f, -48f);
            rootRect.sizeDelta = RootSize;
            rootRect.localScale = Vector3.one;

            RectTransform backContent = CreateStretchRect("BackContent", rootRect);
            RectTransform frontContent = CreateStretchRect("FrontContent", rootRect);

            PlayerPartyHudAssemblyPresenter.SlotView activeSlot = BuildSlot(
                "ActiveSlot",
                backContent,
                frontContent,
                ellen,
                activeHealthBackground,
                activeHealthFill,
                activeEnergyFill,
                threshold,
                ultimate,
                alphaTintMaterial,
                new Vector2(-547f, 0f),
                new Vector2(229f, 94f),
                new Vector2(-150f, 24f),
                new Vector2(575f, 54f),
                new Vector2(-45f, -18f),
                new Vector2(350f, 24f),
                new Vector2(-355f, -47f),
                new Vector2(-394f, -18f),
                new Vector2(58f, 27f),
                true);

            PlayerPartyHudAssemblyPresenter.SlotView reserveNext = BuildSlot(
                "ReserveNext",
                backContent,
                frontContent,
                jane,
                reserveHealthBackground,
                reserveHealthFill,
                reserveEnergyFill,
                threshold,
                ultimate,
                alphaTintMaterial,
                new Vector2(230f, 0f),
                new Vector2(170f, 62f),
                new Vector2(410f, 25f),
                new Vector2(180f, 22f),
                new Vector2(440f, -18f),
                new Vector2(105f, 20f),
                Vector2.zero,
                new Vector2(345f, -18f),
                new Vector2(54f, 22f),
                false);

            PlayerPartyHudAssemblyPresenter.SlotView reservePrevious = BuildSlot(
                "ReservePrevious",
                backContent,
                frontContent,
                corin,
                reserveHealthBackground,
                reserveHealthFill,
                reserveEnergyFill,
                threshold,
                ultimate,
                alphaTintMaterial,
                new Vector2(535f, 0f),
                new Vector2(170f, 62f),
                new Vector2(715f, 25f),
                new Vector2(180f, 22f),
                new Vector2(745f, -18f),
                new Vector2(105f, 20f),
                Vector2.zero,
                new Vector2(650f, -18f),
                new Vector2(54f, 22f),
                false);

            Image frameOverlay = CreateImage(
                "FrameOverlay",
                backContent,
                frame,
                Color.white,
                Vector2.zero,
                RootSize,
                null,
                false);
            frameOverlay.transform.SetAsLastSibling();
            PlayerPauseImageRepair.InstallSharedBackground(root);

            Button pauseButton = CreatePauseButton(
                backContent,
                pause,
                new Vector2(-731f, 0f),
                new Vector2(115f, 105f),
                pauseMaterial);
            pauseButton.transform.SetSiblingIndex(1);
            PlayerPartyPauseButton pauseBridge =
                pauseButton.gameObject.AddComponent<PlayerPartyPauseButton>();
            pauseBridge.Configure(pauseButton);

            PlayerPartyHudAssemblyPresenter presenter =
                root.GetComponent<PlayerPartyHudAssemblyPresenter>();
            presenter.Configure(
                activeSlot,
                new[] { reserveNext, reservePrevious },
                new[] { ellen, jane, corin });

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[PlayerPartyHudAssemblyBuilder] 기능 연결용 HUD 프리팹을 생성했습니다: " +
                PrefabPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static PlayerPartyHudAssemblyPresenter.SlotView BuildSlot(
        string slotName,
        RectTransform backContent,
        RectTransform frontContent,
        Sprite portraitSprite,
        Sprite healthBackgroundSprite,
        Sprite healthFillSprite,
        Sprite energyFillSprite,
        Sprite thresholdSprite,
        Sprite ultimateSprite,
        Material alphaTintMaterial,
        Vector2 portraitPosition,
        Vector2 portraitSize,
        Vector2 healthPosition,
        Vector2 healthSize,
        Vector2 energyPosition,
        Vector2 energySize,
        Vector2 healthTextPosition,
        Vector2 ultimatePosition,
        Vector2 ultimateSize,
        bool showHealthText)
    {
        RectTransform slotRoot = CreateStretchRect(slotName, backContent);
        CanvasGroup canvasGroup = slotRoot.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image portrait = CreateImage(
            "Portrait",
            slotRoot,
            portraitSprite,
            Color.white,
            portraitPosition,
            portraitSize,
            null,
            false);

        Image healthTrail = CreateImage(
            "HealthDamageTrail",
            slotRoot,
            healthBackgroundSprite,
            new Color32(224, 38, 32, 255),
            healthPosition,
            healthSize,
            alphaTintMaterial,
            true);
        healthTrail.fillAmount = 0.88f;

        Image healthFill = CreateImage(
            "HealthCurrent",
            slotRoot,
            healthFillSprite,
            Color.white,
            healthPosition,
            healthSize,
            null,
            true);
        healthFill.fillAmount = 0.78f;

        Image energyFill = CreateImage(
            "EnergyFill",
            slotRoot,
            energyFillSprite,
            new Color32(72, 75, 76, 255),
            energyPosition,
            energySize,
            alphaTintMaterial,
            true);
        energyFill.fillAmount = 0.65f;

        PlayerHudRightEdgeClip.Configure(healthFill, showHealthText ? 44f : 28f);
        PlayerHudRightEdgeClip.Configure(healthTrail, showHealthText ? 44f : 28f);
        PlayerHudRightEdgeClip.Configure(energyFill, showHealthText ? 22f : 18f);

        RectTransform markerTrack = CreateRect(
            slotName + "EnergyMarkerTrack",
            frontContent,
            energyPosition,
            energySize);
        Image marker = CreateImage(
            "EnhancedThreshold",
            markerTrack,
            thresholdSprite,
            Color.white,
            Vector2.zero,
            showHealthText ? new Vector2(17f, 31f) : new Vector2(13f, 25f),
            null,
            false);
        marker.preserveAspect = true;
        RectTransform markerRect = marker.rectTransform;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);

        Text healthText = null;
        if (showHealthText)
        {
            healthText = CreateHealthText(
                slotName + "HealthText",
                frontContent,
                healthTextPosition,
                new Vector2(230f, 30f));
        }

        Image ultimate = CreateImage(
            slotName + "UltimateReady",
            frontContent,
            ultimateSprite,
            Color.white,
            ultimatePosition,
            ultimateSize,
            null,
            false);
        ultimate.preserveAspect = true;

        return new PlayerPartyHudAssemblyPresenter.SlotView
        {
            root = canvasGroup,
            portrait = portrait,
            healthTrail = healthTrail,
            healthFill = healthFill,
            energyFill = energyFill,
            energyThresholdMarker = markerRect,
            healthText = healthText,
            ultimateReadyIndicator = ultimate
        };
    }

    private static Button CreatePauseButton(
        Transform parent,
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        Material pauseMaterial)
    {
        GameObject buttonObject = new GameObject(
            "PauseButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.material = pauseMaterial;
        image.preserveAspect = true;
        image.raycastTarget = true;
        // 승인된 버튼 전체를 표시한다. 재질은 밝은 체크무늬 배경만 투명 처리한다.
        image.type = Image.Type.Simple;
        image.fillAmount = 1f;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        return button;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 position,
        Vector2 size,
        Material material,
        bool filled)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetRect(imageObject.GetComponent<RectTransform>(), position, size);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.material = material;
        image.raycastTarget = false;
        image.preserveAspect = false;

        if (filled)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            image.fillAmount = 1f;
        }

        return image;
    }

    private static Text CreateHealthText(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline));
        textObject.transform.SetParent(parent, false);
        SetRect(textObject.GetComponent<RectTransform>(), position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "11025 / 11025";
        text.fontSize = 25;
        text.fontStyle = FontStyle.BoldAndItalic;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 230);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private static RectTransform CreateStretchRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        SetRect(rect, position, size);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static Material GetOrCreateAlphaTintMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning(
                "[PlayerPartyHudAssemblyBuilder] Alpha Tint 셰이더 임포트를 기다리는 중입니다.");
            return null;
        }

        if (material != null)
        {
            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        EnsureAssetFolder("Assets/Materials/UI");
        material = new Material(shader)
        {
            name = "PlayerHudAlphaTint"
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
