using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AssaultTimerV1Installer
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string SpriteRoot =
        "Assets/Sprites/ZZZHudV2/AssaultTimerV1";
    private const string FramePath = SpriteRoot + "/timer_frame_v1.png";
    private const string IconPath =
        "Assets/Sprites/ZZZHudV2/AssaultFairy/IconTime.png";
    private const float ProgressFullWidth = 277f;

    [MenuItem("Tools/ZZZ HUD V2/Install Functional Assault Timer V1")]
    public static void Install()
    {
        ConfigureSpriteImporter(FramePath, Vector4.zero);
        ConfigureSpriteImporter(IconPath, Vector4.zero);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform combatView = root.transform.Find("CombatView");
            if (combatView == null)
                throw new MissingReferenceException("CombatView");

            Transform existing = combatView.Find("TimerPanel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            BuildTimer(combatView);
            BindAssaultHud(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Functional assault timer V1 installed.");
    }

    private static void BuildTimer(Transform combatView)
    {
        GameObject panel = CreateUiObject("TimerPanel", combatView);
        SetTopRight(
            panel.GetComponent<RectTransform>(),
            new Vector2(405f, 123f),
            new Vector2(-62f, -86f));

        AssaultTimerV1Presenter presenter =
            panel.AddComponent<AssaultTimerV1Presenter>();

        Image frame = CreateImage(
            "FairyTimerBackground",
            panel.transform,
            LoadSprite(FramePath));
        Stretch(frame.rectTransform);

        GameObject progressTrack = CreateUiObject(
            "ProgressTrack",
            panel.transform);
        SetCentered(
            progressTrack.GetComponent<RectTransform>(),
            new Vector2(ProgressFullWidth, 20f),
            new Vector2(-38f, -6.5f));

        GameObject gaugeObject = new GameObject(
            "ProgressFill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(AssaultTimerGaugeGraphic));
        gaugeObject.transform.SetParent(progressTrack.transform, false);
        AssaultTimerGaugeGraphic gauge =
            gaugeObject.GetComponent<AssaultTimerGaugeGraphic>();
        gauge.raycastTarget = false;
        SetCentered(
            gauge.rectTransform,
            new Vector2(ProgressFullWidth, 20f),
            Vector2.zero);

        GameObject digitsObject = new GameObject(
            "TimerDigits",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(AssaultTimerDigitsGraphic));
        digitsObject.transform.SetParent(panel.transform, false);
        AssaultTimerDigitsGraphic digits =
            digitsObject.GetComponent<AssaultTimerDigitsGraphic>();
        digits.color = new Color32(232, 238, 240, 255);
        digits.raycastTarget = false;
        digits.Value = "00:03:00";
        SetCentered(
            digitsObject.GetComponent<RectTransform>(),
            new Vector2(124f, 27f),
            new Vector2(41f, -36.5f));

        Image icon = CreateImage(
            "TimeIcon",
            panel.transform,
            LoadSprite(IconPath));
        SetCentered(
            icon.rectTransform,
            new Vector2(80f, 80f),
            new Vector2(139.5f, -12.5f));
        icon.preserveAspect = true;

        presenter.Configure(digits, gauge);

        GameObject marker = CreateUiObject("AssaultTimerV4Polish", panel.transform);
        marker.SetActive(false);

    }

    private static void BindAssaultHud(GameObject root)
    {
        AssaultBattleHUD hud = root.GetComponent<AssaultBattleHUD>();
        if (hud == null)
            throw new MissingComponentException(nameof(AssaultBattleHUD));

        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("timerText").objectReferenceValue = null;
        serializedHud.FindProperty("timerProgressFill").objectReferenceValue =
            null;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSpriteImporter(string path, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
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

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
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
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
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

    private static void SetCentered(
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
internal static class AssaultTimerV1AutoInstaller
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string MarkerPath =
        "CombatView/TimerPanel/AssaultTimerV4Polish";

    static AssaultTimerV1AutoInstaller()
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

        AssaultTimerV1Installer.Install();
    }
}
