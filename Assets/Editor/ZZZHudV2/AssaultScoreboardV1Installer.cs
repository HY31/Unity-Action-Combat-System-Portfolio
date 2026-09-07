using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AssaultScoreboardV1Installer
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string SpriteRoot =
        "Assets/Sprites/ZZZHudV2/AssaultScoreboardV1";
    private const string FramePath = SpriteRoot + "/score_frame_v1.png";
    private const string MedalActivePath =
        SpriteRoot + "/rank_medal_active_v1.png";
    private const string MedalInactivePath =
        SpriteRoot + "/rank_medal_inactive_v1.png";
    private const string FairyRoot =
        "Assets/Sprites/ZZZHudV2/AssaultFairy";
    private const string FairyPath = FairyRoot + "/Fairy.png";
    private const string FairyBackgroundPath =
        FairyRoot + "/FairyIconBG02.png";
    private const string FairyOutlinePath = FairyRoot + "/Outline64_8.png";
    private const string NumberFontPath =
        "Assets/Resources/Fonts/BarlowCondensed/BarlowCondensed-Black.ttf";

    private static readonly Vector2 PanelSize = new Vector2(526f, 104f);
    private static readonly Vector2 DesignSize = new Vector2(390f, 72f);

    [MenuItem("Tools/ZZZ HUD V2/Install Functional Assault Scoreboard V1")]
    public static void Install()
    {
        ConfigureSpriteImporter(FramePath);
        ConfigureSpriteImporter(MedalActivePath);
        ConfigureSpriteImporter(MedalInactivePath);
        ConfigureSpriteImporter(FairyPath);
        ConfigureSpriteImporter(FairyBackgroundPath);
        ConfigureSpriteImporter(FairyOutlinePath);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform combatView = root.transform.Find("CombatView");
            if (combatView == null)
                throw new MissingReferenceException("CombatView");

            Transform existing = combatView.Find("ScorePanel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            ScoreboardView view = BuildScoreboard(combatView);
            BindAssaultHud(root);
            view.Presenter.Configure(
                view.BubbleRoot,
                view.FairyIconRoot,
                view.CurrentScoreText,
                view.OperationScoreText,
                view.TargetScoreText,
                view.RankMedals,
                LoadSprite(MedalActivePath),
                LoadSprite(MedalInactivePath));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Functional assault scoreboard V1 installed.");
    }

    private static ScoreboardView BuildScoreboard(Transform combatView)
    {
        GameObject panel = CreateUiObject("ScorePanel", combatView);
        SetTopRight(
            panel.GetComponent<RectTransform>(),
            PanelSize,
            new Vector2(-36f, -198f));

        AssaultScoreboardV1Presenter presenter =
            panel.AddComponent<AssaultScoreboardV1Presenter>();

        GameObject bubble = CreateUiObject("BubbleRoot", panel.transform);
        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.anchorMin = Vector2.zero;
        bubbleRect.anchorMax = Vector2.one;
        bubbleRect.pivot = new Vector2(349f / DesignSize.x, 0.5f);
        bubbleRect.offsetMin = Vector2.zero;
        bubbleRect.offsetMax = Vector2.zero;
        bubbleRect.localScale = Vector3.one;

        Image frame = CreateImage(
            "ScoreFrame",
            bubble.transform,
            LoadSprite(FramePath));
        Stretch(frame.rectTransform);

        Image[] medals = new Image[3];
        float[] medalCenters = { 39f, 64f, 89f };
        for (int index = 0; index < medals.Length; index++)
        {
            medals[index] = CreateImage(
                $"Medal{index + 1}",
                bubble.transform,
                LoadSprite(MedalInactivePath));
            SetDesignRect(
                medals[index].rectTransform,
                new Vector2(medalCenters[index], 36f),
                new Vector2(22f, 22f));
            medals[index].preserveAspect = true;
        }

        Font numberFont = AssetDatabase.LoadAssetAtPath<Font>(NumberFontPath);
        if (numberFont == null)
            throw new MissingReferenceException(NumberFontPath);

        Text currentScore = CreateNumberText(
            "ScoreValueLine",
            bubble.transform,
            numberFont,
            "15000  (5000)   / 20000",
            Color.white,
            TextAnchor.MiddleCenter);
        SetDesignRect(
            currentScore.rectTransform,
            new Vector2(236f, 34.5f),
            new Vector2(196f, 29f));

        Text operationScore = null;
        Text targetScore = null;

        GameObject fairyRoot = CreateUiObject("FairyIconRoot", panel.transform);
        RectTransform fairyRootRect = fairyRoot.GetComponent<RectTransform>();
        SetDesignRect(
            fairyRootRect,
            new Vector2(349f, 36f),
            new Vector2(64f, 64f));

        Image fairyBackground = CreateImage(
            "FairyBackground",
            fairyRoot.transform,
            LoadSprite(FairyBackgroundPath));
        SetCentered(
            fairyBackground.rectTransform,
            new Vector2(64f, 64f));
        fairyBackground.rectTransform.anchoredPosition =
            new Vector2(1.5f, -3f);
        fairyBackground.preserveAspect = true;

        Image fairyOutline = CreateImage(
            "FairyOutline",
            fairyRoot.transform,
            LoadSprite(FairyOutlinePath));
        SetCentered(
            fairyOutline.rectTransform,
            new Vector2(56f, 56f));
        fairyOutline.color = new Color32(38, 145, 255, 255);
        fairyOutline.preserveAspect = true;

        Image fairy = CreateImage(
            "Fairy",
            fairyRoot.transform,
            LoadSprite(FairyPath));
        SetCentered(fairy.rectTransform, new Vector2(48f, 48f));
        fairy.preserveAspect = true;

        GameObject marker = CreateUiObject(
            "AssaultScoreboardV5Aligned",
            panel.transform);
        marker.SetActive(false);

        return new ScoreboardView(
            presenter,
            bubbleRect,
            fairyRootRect,
            currentScore,
            operationScore,
            targetScore,
            medals);
    }

    private static void BindAssaultHud(GameObject root)
    {
        AssaultBattleHUD hud = root.GetComponent<AssaultBattleHUD>();
        if (hud == null)
            throw new MissingComponentException(nameof(AssaultBattleHUD));

        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("scoreText").objectReferenceValue = null;
        serializedHud.FindProperty("scoreFill").objectReferenceValue = null;
        SerializedProperty ranks = serializedHud.FindProperty("rankIndicators");
        ranks.arraySize = 0;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Text CreateNumberText(
        string name,
        Transform parent,
        Font font,
        string value,
        Color color,
        TextAnchor alignment)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        gameObject.AddComponent<CanvasRenderer>();
        Text text = gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = 25;
        text.fontStyle = FontStyle.Italic;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 22;
        text.resizeTextMaxSize = 25;
        text.supportRichText = true;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;

        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        return text;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        gameObject.AddComponent<CanvasRenderer>();
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetTopRight(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetDesignRect(
        RectTransform rect,
        Vector2 center,
        Vector2 designSize)
    {
        Vector2 anchor = new Vector2(
            center.x / DesignSize.x,
            1f - center.y / DesignSize.y);
        float scale = PanelSize.x / DesignSize.x;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = designSize * scale;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
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

    private static void ConfigureSpriteImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = Vector4.zero;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
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

    private readonly struct ScoreboardView
    {
        public readonly AssaultScoreboardV1Presenter Presenter;
        public readonly RectTransform BubbleRoot;
        public readonly RectTransform FairyIconRoot;
        public readonly Text CurrentScoreText;
        public readonly Text OperationScoreText;
        public readonly Text TargetScoreText;
        public readonly Image[] RankMedals;

        public ScoreboardView(
            AssaultScoreboardV1Presenter presenter,
            RectTransform bubbleRoot,
            RectTransform fairyIconRoot,
            Text currentScoreText,
            Text operationScoreText,
            Text targetScoreText,
            Image[] rankMedals)
        {
            Presenter = presenter;
            BubbleRoot = bubbleRoot;
            FairyIconRoot = fairyIconRoot;
            CurrentScoreText = currentScoreText;
            OperationScoreText = operationScoreText;
            TargetScoreText = targetScoreText;
            RankMedals = rankMedals;
        }
    }
}

[InitializeOnLoad]
internal static class AssaultScoreboardV1AutoInstaller
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string MarkerPath =
        "CombatView/ScorePanel/AssaultScoreboardV5Aligned";

    static AssaultScoreboardV1AutoInstaller()
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

        AssaultScoreboardV1Installer.Install();
    }
}
