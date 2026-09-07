using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class BossHudV8Installer
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string SpriteRoot =
        "Assets/Sprites/ZZZHudV2/BossStatusV8";
    private const string FontPath =
        "Assets/Resources/Fonts/BarlowCondensed/BarlowCondensed-Black.ttf";

    [MenuItem("Tools/ZZZ HUD V2/Install Functional Boss HUD V8")]
    public static void Install()
    {
        ConfigureSpriteImporters();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform combatView = root.transform.Find("CombatView");
            if (combatView == null)
                throw new MissingReferenceException("CombatView");

            Transform existing = combatView.Find("BossStatusPanel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            BossHudView view = BuildBossHud(combatView);
            BindAssaultHud(root, view);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Functional boss HUD V8 installed.");
    }

    private static BossHudView BuildBossHud(Transform combatView)
    {
        GameObject panel = CreateUiObject("BossStatusPanel", combatView);
        SetTopRight(
            panel.GetComponent<RectTransform>(),
            new Vector2(790f, 86f),
            new Vector2(-62f, -31f));

        BossHudV8Presenter presenter = panel.AddComponent<BossHudV8Presenter>();

        GameObject sourceFrame = CreateUiObject("SourceFrame", panel.transform);
        Stretch(sourceFrame.GetComponent<RectTransform>());

        Image frame = CreateImage(
            "BossOuterMask",
            sourceFrame.transform,
            LoadSprite("boss_frame_v8"));
        Stretch(frame.rectTransform);

        GameObject stunTrack = CreateUiObject("Stun", sourceFrame.transform);
        SetCentered(
            stunTrack.GetComponent<RectTransform>(),
            new Vector2(517f, 14f),
            new Vector2(-77.5f, -1f));
        Image stunBackground = CreateImage(
            "Background",
            stunTrack.transform,
            LoadSprite("boss_stun_fill_v8"));
        Stretch(stunBackground.rectTransform);
        stunBackground.color = new Color32(71, 76, 78, 255);
        Image stunFill = CreateFillImage(
            "Fill",
            stunTrack.transform,
            LoadSprite("boss_stun_fill_v8"));
        Stretch(stunFill.rectTransform);

        GameObject healthTrack = CreateUiObject("Health", sourceFrame.transform);
        SetCentered(
            healthTrack.GetComponent<RectTransform>(),
            new Vector2(577f, 31f),
            new Vector2(-40.5f, 8f));
        Image healthTrail = CreateFillImage(
            "Trail",
            healthTrack.transform,
            LoadSprite("boss_health_trail_v8"));
        Stretch(healthTrail.rectTransform);
        Image healthFill = CreateFillImage(
            "Fill",
            healthTrack.transform,
            LoadSprite("boss_health_current_v8"));
        Stretch(healthFill.rectTransform);

        GameObject portraitMask = CreateUiObject(
            "PortraitMask",
            sourceFrame.transform);
        SetCentered(
            portraitMask.GetComponent<RectTransform>(),
            new Vector2(116f, 53f),
            new Vector2(313f, -1.5f));
        Image portrait = CreateImage(
            "BossPortrait",
            portraitMask.transform,
            LoadSprite("boss_portrait_v8"));
        Stretch(portrait.rectTransform);

        Text stunText = CreateStunText(sourceFrame.transform);
        presenter.Configure(healthTrail, healthFill, stunFill, stunText);

        GameObject marker = CreateUiObject("BossHudV8", sourceFrame.transform);
        marker.SetActive(false);

        return new BossHudView(healthFill, stunFill, stunText);
    }

    private readonly struct BossHudView
    {
        public readonly Image HealthFill;
        public readonly Image StunFill;
        public readonly Text StunText;

        public BossHudView(Image healthFill, Image stunFill, Text stunText)
        {
            HealthFill = healthFill;
            StunFill = stunFill;
            StunText = stunText;
        }
    }
}
