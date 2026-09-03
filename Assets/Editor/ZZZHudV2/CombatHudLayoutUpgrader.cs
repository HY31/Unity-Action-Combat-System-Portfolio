using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class CombatHudLayoutUpgrader
{
    private const string AssaultPrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_AssaultBattleHUD.prefab";
    private const string EnemyPrefabPath =
        "Assets/Prefabs/UI/ZZZHudV2/ZZZ_EnemyWorldHUD.prefab";
    private const string SessionKey = "ZZZHudV2.CombatLayoutUpgradeAttempted";

    static CombatHudLayoutUpgrader()
    {
        EditorApplication.delayCall += UpgradeOnce;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += UpgradeOnce;
    }

    [MenuItem("Tools/ZZZ HUD V2/Apply Combat Layout Upgrade")]
    public static void UpgradeAll()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(AssaultPrefabPath) != null)
            UpgradeAssaultHud();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) != null)
            UpgradeEnemyWorldHud();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void UpgradeOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += UpgradeOnce;
            return;
        }

        GameObject assault = AssetDatabase.LoadAssetAtPath<GameObject>(AssaultPrefabPath);
        bool needsUpgrade =
            assault != null &&
            (assault.transform.Find("CombatView/BossStatusPanel") == null ||
             assault.transform.Find("CombatView/ActionHUD") == null);

        if (SessionState.GetBool(SessionKey, false) && !needsUpgrade)
            return;

        SessionState.SetBool(SessionKey, true);
        UpgradeAll();
    }

    private static void UpgradeAssaultHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(AssaultPrefabPath);
        try
        {
            Transform combatView = root.transform.Find("CombatView");
            if (combatView == null)
                return;

            RepositionCombatPanels(combatView);
            BossView bossView = EnsureBossStatusView(combatView);
            EnsureActionHud(combatView);
            ConfigureAssaultHud(root, bossView);

            PrefabUtility.SaveAsPrefabAsset(root, AssaultPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RepositionCombatPanels(Transform combatView)
    {
        RectTransform timer = combatView.Find("TimerPanel") as RectTransform;
        if (timer != null)
        {
            timer.anchorMin = Vector2.one;
            timer.anchorMax = Vector2.one;
            timer.pivot = Vector2.one;
            timer.sizeDelta = new Vector2(270f, 48f);
            timer.anchoredPosition = new Vector2(-42f, -134f);

            Text operation = timer.Find("OperationLabel")?.GetComponent<Text>();
            if (operation != null)
            {
                operation.fontSize = 11;
                SetRect(operation.rectTransform, new Vector2(118f, 18f), new Vector2(-69f, 13f));
            }

            Text time = timer.Find("Timer")?.GetComponent<Text>();
            if (time != null)
            {
                time.fontSize = 28;
                SetRect(time.rectTransform, new Vector2(125f, 40f), new Vector2(58f, 0f));
            }

            RectTransform accent = timer.Find("Accent") as RectTransform;
            if (accent != null)
                SetRect(accent, new Vector2(252f, 4f), new Vector2(0f, -22f));
        }

        RectTransform score = combatView.Find("ScorePanel") as RectTransform;
        if (score != null)
        {
            score.anchorMin = Vector2.one;
            score.anchorMax = Vector2.one;
            score.pivot = Vector2.one;
            score.sizeDelta = new Vector2(440f, 62f);
            score.anchoredPosition = new Vector2(-42f, -190f);

            Text label = score.Find("ScoreLabel")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = "SCORE";
                label.fontSize = 15;
                SetRect(label.rectTransform, new Vector2(88f, 24f), new Vector2(-165f, 15f));
            }

            Text scoreText = score.Find("Score")?.GetComponent<Text>();
            if (scoreText != null)
            {
                scoreText.fontSize = 30;
                SetRect(scoreText.rectTransform, new Vector2(176f, 42f), new Vector2(-23f, 10f));
            }

            Text damage = score.Find("Damage")?.GetComponent<Text>();
            if (damage != null)
            {
                damage.fontSize = 12;
                SetRect(damage.rectTransform, new Vector2(190f, 22f), new Vector2(115f, 10f));
            }

            RectTransform track = score.Find("ScoreTrack") as RectTransform;
            if (track != null)
                SetRect(track, new Vector2(404f, 6f), new Vector2(0f, -25f));
        }
    }

    private static BossView EnsureBossStatusView(Transform combatView)
    {
        Transform existing = combatView.Find("BossStatusPanel");
        if (existing != null)
        {
            return new BossView
            {
                healthFill = existing.Find("Health/Fill")?.GetComponent<Image>(),
                stunFill = existing.Find("Stun/Fill")?.GetComponent<Image>(),
                healthText = existing.Find("HealthText")?.GetComponent<Text>(),
                stunText = existing.Find("StunText")?.GetComponent<Text>()
            };
        }

        Sprite healthFrame = LoadSprite("player_hp_frame");
        Sprite healthFillSprite = LoadSprite("player_hp_fill");
        Sprite stunFrame = LoadSprite("player_energy_frame");
        Sprite stunFillSprite = LoadSprite("enemy_stun_fill");
        Sprite portraitFrame = LoadElementSprite("anomaly_disc_back");

        Image panel = CreateImage(
            "BossStatusPanel",
            combatView,
            null,
            new Color32(7, 9, 10, 232));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.sizeDelta = new Vector2(670f, 90f);
        panelRect.anchoredPosition = new Vector2(-42f, -32f);

        Image accent = CreateImage(
            "Accent",
            panel.transform,
            null,
            new Color32(255, 104, 24, 255));
        SetRect(accent.rectTransform, new Vector2(7f, 76f), new Vector2(-329f, 0f));

        Text level = CreateText(
            "BossLevel",
            panel.transform,
            "08",
            30,
            TextAnchor.MiddleCenter,
            FontStyle.BoldAndItalic,
            new Color32(255, 149, 20, 255));
        SetRect(level.rectTransform, new Vector2(52f, 48f), new Vector2(-296f, 5f));

        Text name = CreateText(
            "BossName",
            panel.transform,
            "DEAD END BUTCHER",
            14,
            TextAnchor.MiddleLeft,
            FontStyle.Bold,
            Color.white);
        SetRect(name.rectTransform, new Vector2(300f, 20f), new Vector2(-120f, 33f));

        GaugeView health = CreateGauge(
            "Health",
            panel.transform,
            new Vector2(506f, 23f),
            new Vector2(-17f, 13f),
            healthFrame,
            healthFillSprite,
            new Vector4(5f, 3f, 5f, 3f));
        health.fill.color = new Color32(93, 238, 35, 255);

        GaugeView stun = CreateGauge(
            "Stun",
            panel.transform,
            new Vector2(420f, 14f),
            new Vector2(-49f, -18f),
            stunFrame,
            stunFillSprite,
            new Vector4(4f, 3f, 4f, 3f));
        stun.fill.color = new Color32(255, 205, 24, 255);

        Text healthText = CreateText(
            "HealthText",
            panel.transform,
            "-- / --",
            12,
            TextAnchor.MiddleRight,
            FontStyle.Bold,
            Color.white);
        SetRect(healthText.rectTransform, new Vector2(118f, 20f), new Vector2(183f, 33f));

        Text stunText = CreateText(
            "StunText",
            panel.transform,
            "DAZE 0%",
            13,
            TextAnchor.MiddleRight,
            FontStyle.BoldAndItalic,
            new Color32(255, 205, 24, 255));
        SetRect(stunText.rectTransform, new Vector2(86f, 20f), new Vector2(197f, -18f));

        Image portrait = CreateImage(
            "BossPortrait",
            panel.transform,
            portraitFrame,
            new Color32(232, 67, 35, 255));
        SetRect(portrait.rectTransform, new Vector2(66f, 66f), new Vector2(296f, 0f));
        portrait.preserveAspect = true;

        Text portraitLabel = CreateText(
            "Label",
            portrait.transform,
            "DEB",
            15,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Color.white);
        Stretch(portraitLabel.rectTransform);

        return new BossView
        {
            healthFill = health.fill,
            stunFill = stun.fill,
            healthText = healthText,
            stunText = stunText
        };
    }

    private static void EnsureActionHud(Transform combatView)
    {
        if (combatView.Find("ActionHUD") != null)
            return;

        Sprite disc = LoadElementSprite("anomaly_disc_back");
        Sprite ring = LoadElementSprite("anomaly_ring_frame");
        Sprite pipSprite = LoadSprite("energy_threshold_marker");

        GameObject root = CreateUiObject("ActionHUD", combatView);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.sizeDelta = new Vector2(440f, 280f);
        rootRect.anchoredPosition = new Vector2(-42f, 34f);

        CombatActionHUD.ButtonView attack = CreateActionButton(
            "Attack", root.transform, new Vector2(-142f, -82f), "ATK", "LMB", disc, ring);
        CombatActionHUD.ButtonView dodge = CreateActionButton(
            "Dodge", root.transform, new Vector2(-46f, -82f), ">>", "SHIFT", disc, ring);
        CombatActionHUD.ButtonView skill = CreateActionButton(
            "Skill", root.transform, new Vector2(50f, -82f), "EX", "E", disc, ring);
        CombatActionHUD.ButtonView support = CreateActionButton(
            "Support", root.transform, new Vector2(146f, -82f), "SW", "SPACE", disc, ring);
        CombatActionHUD.ButtonView ultimate = CreateActionButton(
            "Ultimate", root.transform, new Vector2(146f, 36f), "ULT", "Q", disc, ring);

        Image[] supportPips = new Image[6];
        for (int i = 0; i < supportPips.Length; i++)
        {
            float angle = (30f + i * 60f) * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 50f;
            Image pip = CreateImage(
                $"SupportPoint_{i + 1:00}",
                support.root.transform,
                pipSprite,
                new Color32(255, 207, 19, 255));
            SetRect(pip.rectTransform, new Vector2(13f, 13f), offset);
            supportPips[i] = pip;
        }

        CombatActionHUD actionHud = root.AddComponent<CombatActionHUD>();
        actionHud.Configure(attack, dodge, skill, support, ultimate, supportPips);
    }

    private static CombatActionHUD.ButtonView CreateActionButton(
        string name,
        Transform parent,
        Vector2 position,
        string symbol,
        string key,
        Sprite disc,
        Sprite ring)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(86f, 108f), position);
        CanvasGroup group = root.AddComponent<CanvasGroup>();

        Image background = CreateImage(
            "Background",
            root.transform,
            disc,
            new Color32(12, 15, 16, 238));
        SetRect(background.rectTransform, new Vector2(78f, 78f), new Vector2(0f, 13f));
        background.preserveAspect = true;

        Image frame = CreateImage(
            "Frame",
            root.transform,
            ring,
            new Color32(151, 157, 157, 255));
        SetRect(frame.rectTransform, new Vector2(82f, 82f), new Vector2(0f, 13f));
        frame.preserveAspect = true;

        Text symbolText = CreateText(
            "Symbol",
            root.transform,
            symbol,
            symbol.Length > 2 ? 17 : 24,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            new Color32(151, 157, 157, 255));
        SetRect(symbolText.rectTransform, new Vector2(60f, 46f), new Vector2(0f, 13f));

        Image keyBack = CreateImage(
            "KeyBack",
            root.transform,
            null,
            new Color32(8, 10, 11, 246));
        SetRect(keyBack.rectTransform, new Vector2(62f, 24f), new Vector2(0f, -40f));

        Text keyText = CreateText(
            "Key",
            root.transform,
            key,
            key.Length > 3 ? 11 : 14,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            new Color32(151, 157, 157, 255));
        Stretch(keyText.rectTransform);
        keyText.transform.SetParent(keyBack.transform, false);
        Stretch(keyText.rectTransform);

        return new CombatActionHUD.ButtonView
        {
            root = group,
            background = background,
            frame = frame,
            symbol = symbolText,
            keyLabel = keyText
        };
    }

    private static void ConfigureAssaultHud(GameObject root, BossView boss)
    {
        AssaultBattleHUD hud = root.GetComponent<AssaultBattleHUD>();
        if (hud == null)
            return;

        Transform combat = root.transform.Find("CombatView");
        Transform result = root.transform.Find("ResultView");
        Transform resultPanel = result?.Find("ResultPanel");
        Transform wipeout = root.transform.Find("WipeoutView");

        hud.ConfigureView(
            combat?.GetComponent<CanvasGroup>(),
            combat?.Find("TimerPanel/Timer")?.GetComponent<Text>(),
            combat?.Find("ScorePanel/Score")?.GetComponent<Text>(),
            combat?.Find("ScorePanel/Damage")?.GetComponent<Text>(),
            combat?.Find("ScorePanel/ScoreTrack/ScoreFill")?.GetComponent<Image>(),
            boss.healthFill,
            boss.stunFill,
            boss.healthText,
            boss.stunText,
            result?.GetComponent<CanvasGroup>(),
            resultPanel?.Find("Reason")?.GetComponent<Text>(),
            resultPanel?.Find("Rank")?.GetComponent<Text>(),
            resultPanel?.Find("Score")?.GetComponent<Text>(),
            resultPanel?.Find("Damage")?.GetComponent<Text>(),
            wipeout?.GetComponent<CanvasGroup>(),
            wipeout?.Find("YellowTint")?.GetComponent<Image>(),
            wipeout?.Find("WipeoutText")?.GetComponent<Text>(),
            resultPanel?.Find("BossImage")?.GetComponent<Image>(),
            resultPanel?.Find("ExitButton")?.GetComponent<Button>());
    }

    private static void UpgradeEnemyWorldHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            RectTransform rect = root.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(240f, 92f);

            RectTransform visuals = root.transform.Find("Visuals") as RectTransform;
            if (visuals != null)
                visuals.localScale = Vector3.one * 1.4f;

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GaugeView CreateGauge(
        string name,
        Transform parent,
        Vector2 size,
        Vector2 position,
        Sprite frameSprite,
        Sprite fillSprite,
        Vector4 inset)
    {
        GameObject root = CreateUiObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), size, position);

        Image frame = CreateImage("Frame", root.transform, frameSprite, Color.white);
        Stretch(frame.rectTransform);

        Image fill = CreateImage("Fill", root.transform, fillSprite, Color.white);
        Stretch(fill.rectTransform, inset);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillClockwise = true;
        fill.fillAmount = 1f;
        return new GaugeView { fill = fill };
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        gameObject.AddComponent<CanvasRenderer>();
        Image image = gameObject.AddComponent<Image>();
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
        FontStyle fontStyle,
        Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        gameObject.AddComponent<CanvasRenderer>();
        Text text = gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/Sprites/ZZZHudV2/{name}.png");
    }

    private static Sprite LoadElementSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(
            $"Assets/Sprites/ZZZHudV2/Elements/{name}.png");
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
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
        Stretch(rect, Vector4.zero);
    }

    private static void Stretch(RectTransform rect, Vector4 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset.x, inset.y);
        rect.offsetMax = new Vector2(-inset.z, -inset.w);
        rect.localScale = Vector3.one;
    }

    private sealed class BossView
    {
        public Image healthFill;
        public Image stunFill;
        public Text healthText;
        public Text stunText;
    }

    private sealed class GaugeView
    {
        public Image fill;
    }
}
