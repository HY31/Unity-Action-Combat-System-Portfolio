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
    private const string ActionButtonSpriteRoot =
        "Assets/Sprites/ZZZHudV2/ActionButtons";
    private const string AssaultFairySpriteRoot =
        "Assets/Sprites/ZZZHudV2/AssaultFairy";
    private const string BossStatusSpriteRoot =
        "Assets/Sprites/ZZZHudV2/BossStatus";
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
        Transform readySheenMask = assault != null
            ? assault.transform.Find("CombatView/ActionHUD/Skill/ReadySheenMask")
            : null;
        bool needsUpgrade =
            assault != null &&
            (assault.transform.Find("CombatView/BossStatusPanel") == null ||
             assault.transform.Find("CombatView/BossStatusPanel/SourceFrame/BossOuterMask") == null ||
             assault.transform.Find("CombatView/BossStatusPanel/SourceFrame/Health/Fill") == null ||
             assault.transform.Find("CombatView/BossStatusPanel/SourceFrame/Stun/Fill") == null ||
             assault.transform.Find("CombatView/BossStatusPanel/SourceFrame/PortraitMask/BossPortrait") == null ||
             assault.transform.Find("CombatView/ActionHUD") == null ||
             assault.transform.Find("CombatView/ActionHUD/Attack/Icon") == null ||
             assault.transform.Find("CombatView/TimerPanel/FairyTimerBackground") == null ||
             assault.transform.Find("CombatView/ScorePanel/FairyIcon") == null ||
             readySheenMask == null ||
             readySheenMask.GetComponent<Mask>() == null);

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

            AssaultInfoView assaultInfo = RebuildFairyAssaultInfo(combatView);
            BossView bossView = RebuildBossStatusView(combatView);
            EnsureActionHud(combatView);
            ConfigureAssaultHud(root, bossView, assaultInfo);

            PrefabUtility.SaveAsPrefabAsset(root, AssaultPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AssaultInfoView RebuildFairyAssaultInfo(Transform combatView)
    {
        Transform oldTimer = combatView.Find("TimerPanel");
        if (oldTimer != null)
            UnityEngine.Object.DestroyImmediate(oldTimer.gameObject);

        Transform oldScore = combatView.Find("ScorePanel");
        if (oldScore != null)
            UnityEngine.Object.DestroyImmediate(oldScore.gameObject);

        Sprite panelSprite = LoadAssaultFairySprite("ProBg03");
        Sprite timerSprite = LoadAssaultFairySprite("FairyTime_Pro");
        Sprite timerLineSprite = LoadAssaultFairySprite("FairyTime_ProLine");
        Sprite timerIconSprite = LoadAssaultFairySprite("IconTime");
        Sprite circleSprite = LoadAssaultFairySprite("Circle64");
        Sprite fairyBackgroundSprite = LoadAssaultFairySprite("FairyIconBG02");
        Sprite fairySprite = LoadAssaultFairySprite("Fairy");
        Sprite leftCapSprite = LoadAssaultFairySprite("ProBg02");
        Sprite rightCapSprite = LoadAssaultFairySprite("ProBg04");

        GameObject timerObject = CreateUiObject("TimerPanel", combatView);
        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        timerRect.anchorMin = Vector2.one;
        timerRect.anchorMax = Vector2.one;
        timerRect.pivot = Vector2.one;
        timerRect.sizeDelta = new Vector2(360f, 58f);
        timerRect.anchoredPosition = new Vector2(-42f, -126f);

        Image timerBackground = CreateImage(
            "FairyTimerBackground",
            timerObject.transform,
            panelSprite,
            new Color32(19, 21, 23, 248));
        SetRect(timerBackground.rectTransform, new Vector2(326f, 42f), new Vector2(-11f, 0f));
        timerBackground.type = Image.Type.Sliced;

        GameObject progressTrackObject = CreateUiObject("ProgressTrack", timerObject.transform);
        SetRect(
            progressTrackObject.GetComponent<RectTransform>(),
            new Vector2(262f, 20f),
            new Vector2(-31f, 10f));

        Image progressBack = CreateImage(
            "Background",
            progressTrackObject.transform,
            timerSprite,
            new Color32(75, 47, 43, 255));
        Stretch(progressBack.rectTransform);
        progressBack.type = Image.Type.Sliced;

        Image progressFill = CreateImage(
            "ProgressFill",
            progressTrackObject.transform,
            timerSprite,
            new Color32(235, 74, 39, 255));
        Stretch(progressFill.rectTransform, new Vector4(3f, 3f, 3f, 3f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillClockwise = true;
        progressFill.fillAmount = 0f;

        Image progressLine = CreateImage(
            "Line",
            progressTrackObject.transform,
            timerLineSprite,
            new Color32(37, 39, 41, 255));
        Stretch(progressLine.rectTransform);
        progressLine.type = Image.Type.Sliced;

        Text timerText = CreateText(
            "Timer",
            timerObject.transform,
            "03:00",
            22,
            TextAnchor.MiddleRight,
            FontStyle.Bold,
            new Color32(225, 231, 234, 255));
        SetRect(timerText.rectTransform, new Vector2(142f, 28f), new Vector2(62f, -10f));

        Image timerIcon = CreateImage(
            "TimeIcon",
            timerObject.transform,
            timerIconSprite,
            Color.white);
        SetRect(timerIcon.rectTransform, new Vector2(52f, 52f), new Vector2(151f, 0f));
        timerIcon.preserveAspect = true;

        GameObject scoreObject = CreateUiObject("ScorePanel", combatView);
        RectTransform scoreRect = scoreObject.GetComponent<RectTransform>();
        scoreRect.anchorMin = Vector2.one;
        scoreRect.anchorMax = Vector2.one;
        scoreRect.pivot = Vector2.one;
        scoreRect.sizeDelta = new Vector2(420f, 58f);
        scoreRect.anchoredPosition = new Vector2(-42f, -188f);

        Image scoreBackground = CreateImage(
            "FairyScoreBackground",
            scoreObject.transform,
            panelSprite,
            new Color32(16, 18, 20, 250));
        SetRect(scoreBackground.rectTransform, new Vector2(382f, 42f), new Vector2(-12f, 0f));
        scoreBackground.type = Image.Type.Sliced;

        Image leftCap = CreateImage(
            "LeftCap",
            scoreObject.transform,
            leftCapSprite,
            new Color32(25, 28, 30, 255));
        SetRect(leftCap.rectTransform, new Vector2(62f, 40f), new Vector2(-179f, 0f));
        leftCap.preserveAspect = true;

        Image rightCap = CreateImage(
            "RightCap",
            scoreObject.transform,
            rightCapSprite,
            new Color32(16, 18, 20, 255));
        SetRect(rightCap.rectTransform, new Vector2(30f, 38f), new Vector2(165f, 0f));
        rightCap.preserveAspect = true;

        GameObject rankRoot = CreateUiObject("RankMedals", scoreObject.transform);
        SetRect(
            rankRoot.GetComponent<RectTransform>(),
            new Vector2(78f, 28f),
            new Vector2(-134f, 0f));

        string[] rankNames = { "RankB", "RankA", "RankS" };
        string[] rankLabels = { "B", "A", "S" };
        Image[] rankIndicators = new Image[rankNames.Length];
        for (int i = 0; i < rankNames.Length; i++)
        {
            float x = (i - 1) * 25f;
            Image rank = CreateImage(
                rankNames[i],
                rankRoot.transform,
                circleSprite,
                new Color32(83, 86, 87, 255));
            SetRect(rank.rectTransform, new Vector2(22f, 22f), new Vector2(x, 0f));
            rank.preserveAspect = true;

            Text label = CreateText(
                "Label",
                rank.transform,
                rankLabels[i],
                10,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color32(16, 18, 20, 255));
            Stretch(label.rectTransform);
            rankIndicators[i] = rank;
        }

        Text scoreText = CreateText(
            "Score",
            scoreObject.transform,
            "점수: <color=#FFC51C>0</color> <color=#FFC51C>(0)</color>/6000",
            18,
            TextAnchor.MiddleLeft,
            FontStyle.Bold,
            new Color32(235, 239, 240, 255));
        SetRect(scoreText.rectTransform, new Vector2(248f, 34f), new Vector2(35f, 0f));
        scoreText.supportRichText = true;

        Image fairyBackground = CreateImage(
            "FairyIconBackground",
            scoreObject.transform,
            fairyBackgroundSprite,
            Color.white);
        SetRect(fairyBackground.rectTransform, new Vector2(58f, 54f), new Vector2(180f, 0f));
        fairyBackground.preserveAspect = true;

        Image fairyIcon = CreateImage(
            "FairyIcon",
            scoreObject.transform,
            fairySprite,
            Color.white);
        SetRect(fairyIcon.rectTransform, new Vector2(45f, 45f), new Vector2(178f, 0f));
        fairyIcon.preserveAspect = true;

        return new AssaultInfoView
        {
            timerProgressFill = progressFill,
            rankIndicators = rankIndicators
        };
    }

    /// <summary>
    /// 추출한 원본 조각으로 보스 체력·그로기·초상을 하나의 프레임에 조립한다.
    /// 외곽과 게이지 바탕은 검게 사용하고 실제 수치만 색이 있는 게이지로 표시한다.
    /// </summary>
    private static BossView RebuildBossStatusView(Transform combatView)
    {
        Transform existing = combatView.Find("BossStatusPanel");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Sprite outerMaskSprite = LoadBossStatusSprite("boss_outer_mask");
        Sprite healthBackgroundSprite = LoadBossStatusSprite("boss_health_background");
        Sprite healthFillSprite = LoadBossStatusSprite("boss_health_fill");
        Sprite stunFillSprite = LoadBossStatusSprite("boss_stun_fill");
        Sprite portraitMaskSprite = LoadBossStatusSprite("boss_portrait_mask");
        Sprite portraitSprite = LoadBossStatusSprite("boss_portrait_full");

        GameObject panel = CreateUiObject("BossStatusPanel", combatView);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.sizeDelta = new Vector2(650f, 70f);
        panelRect.anchoredPosition = new Vector2(-42f, -22f);

        GameObject sourceFrame = CreateUiObject("SourceFrame", panel.transform);
        Stretch(sourceFrame.GetComponent<RectTransform>());

        Image outerMask = CreateImage(
            "BossOuterMask",
            sourceFrame.transform,
            outerMaskSprite,
            new Color32(2, 3, 4, 255));
        SetRect(outerMask.rectTransform, new Vector2(584f, 66f), new Vector2(4f, 0f));

        Image healthBackground = CreateImage(
            "HealthBackground",
            sourceFrame.transform,
            healthBackgroundSprite,
            new Color32(2, 3, 4, 255));
        SetRect(
            healthBackground.rectTransform,
            new Vector2(464f, 40f),
            new Vector2(-46f, 7f));
        healthBackground.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        GameObject healthTrack = CreateUiObject("Health", sourceFrame.transform);
        SetRect(
            healthTrack.GetComponent<RectTransform>(),
            new Vector2(454f, 12f),
            new Vector2(-51f, 10f));
        Image healthFill = CreateImage(
            "Fill",
            healthTrack.transform,
            healthFillSprite,
            Color.white);
        Stretch(healthFill.rectTransform);
        ConfigureHorizontalFill(healthFill);

        Image stunBackground = CreateImage(
            "StunBackground",
            sourceFrame.transform,
            stunFillSprite,
            new Color32(2, 3, 4, 255));
        SetRect(
            stunBackground.rectTransform,
            new Vector2(440f, 10f),
            new Vector2(-58f, -15f));

        GameObject stunTrack = CreateUiObject("Stun", sourceFrame.transform);
        SetRect(
            stunTrack.GetComponent<RectTransform>(),
            new Vector2(432f, 7f),
            new Vector2(-62f, -15f));
        Image stunFill = CreateImage(
            "Fill",
            stunTrack.transform,
            stunFillSprite,
            new Color32(255, 205, 24, 255));
        Stretch(stunFill.rectTransform);
        ConfigureHorizontalFill(stunFill);

        Text stunText = CreateText(
            "StunText",
            sourceFrame.transform,
            "00",
            34,
            TextAnchor.MiddleCenter,
            FontStyle.BoldAndItalic,
            new Color32(255, 149, 20, 255));
        SetRect(stunText.rectTransform, new Vector2(58f, 46f), new Vector2(-307f, 8f));

        Image portraitMask = CreateImage(
            "PortraitMask",
            sourceFrame.transform,
            portraitMaskSprite,
            Color.white);
        SetRect(
            portraitMask.rectTransform,
            new Vector2(96f, 56f),
            new Vector2(234f, 1f));
        Mask portraitClip = portraitMask.gameObject.AddComponent<Mask>();
        portraitClip.showMaskGraphic = false;

        Image portrait = CreateImage(
            "BossPortrait",
            portraitMask.transform,
            portraitSprite,
            Color.white);
        SetRect(portrait.rectTransform, new Vector2(112f, 155f), new Vector2(5f, -20f));
        portrait.preserveAspect = true;

        return new BossView
        {
            healthFill = healthFill,
            stunFill = stunFill,
            healthText = null,
            stunText = stunText
        };
    }

    private static void ConfigureHorizontalFill(Image image)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillClockwise = true;
        image.fillAmount = 1f;
    }

    private static void EnsureActionHud(Transform combatView)
    {
        Transform existing = combatView.Find("ActionHUD");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Sprite disc = LoadElementSprite("anomaly_disc_back");
        Sprite supportSegmentSprite = LoadElementSprite("anomaly_ring_fill");
        Sprite attackIcon = LoadActionButtonSprite("action_attack");
        Sprite attackOutline = LoadActionButtonSprite("action_attack_outline");
        Sprite dodgeIcon = LoadActionButtonSprite("action_dodge");
        Sprite skillNormalIcon = LoadActionButtonSprite("action_skill_normal");
        Sprite skillEnhancedIcon = LoadActionButtonSprite("action_skill_enhanced");
        Sprite supportIcon = LoadActionButtonSprite("action_support");
        Sprite supportOutline = LoadActionButtonSprite("action_support_outline");
        Sprite ultimateIcon = LoadActionButtonSprite("action_ultimate");
        Sprite readySheen = LoadActionButtonSprite("action_ready_sheen");

        GameObject root = CreateUiObject("ActionHUD", combatView);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.sizeDelta = new Vector2(440f, 280f);
        rootRect.anchoredPosition = new Vector2(-42f, 34f);

        CombatActionHUD.ButtonView attack = CreateActionButton(
            "Attack",
            root.transform,
            new Vector2(-142f, -82f),
            "LMB",
            disc,
            attackIcon,
            null,
            attackOutline,
            readySheen,
            new Vector2(78f, 78f),
            false,
            false);
        CombatActionHUD.ButtonView dodge = CreateActionButton(
            "Dodge",
            root.transform,
            new Vector2(-46f, -82f),
            "SHIFT",
            disc,
            dodgeIcon,
            null,
            null,
            readySheen,
            new Vector2(82f, 82f),
            false,
            false);
        CombatActionHUD.ButtonView skill = CreateActionButton(
            "Skill",
            root.transform,
            new Vector2(50f, -82f),
            "E",
            disc,
            skillNormalIcon,
            skillEnhancedIcon,
            null,
            readySheen,
            new Vector2(82f, 82f),
            true,
            false);
        CombatActionHUD.ButtonView support = CreateActionButton(
            "Support",
            root.transform,
            new Vector2(146f, -82f),
            "SPACE",
            disc,
            supportIcon,
            null,
            supportOutline,
            readySheen,
            new Vector2(58f, 56f),
            false,
            false);
        CombatActionHUD.ButtonView ultimate = CreateActionButton(
            "Ultimate",
            root.transform,
            new Vector2(146f, 36f),
            "Q",
            disc,
            ultimateIcon,
            ultimateIcon,
            null,
            readySheen,
            new Vector2(82f, 82f),
            true,
            true);

        Image[] supportSegments = new Image[6];
        for (int i = 0; i < supportSegments.Length; i++)
        {
            Image segment = CreateImage(
                $"SupportPoint_{i + 1:00}",
                support.root.transform,
                supportSegmentSprite,
                new Color32(255, 207, 19, 255));
            SetRect(segment.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 13f));
            segment.type = Image.Type.Filled;
            segment.fillMethod = Image.FillMethod.Radial360;
            segment.fillOrigin = (int)Image.Origin360.Top;
            segment.fillClockwise = true;
            segment.fillAmount = 0.15f;
            segment.rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * 60f);
            supportSegments[i] = segment;
        }

        CombatActionHUD actionHud = root.AddComponent<CombatActionHUD>();
        actionHud.Configure(attack, dodge, skill, support, ultimate, supportSegments);
    }

    private static CombatActionHUD.ButtonView CreateActionButton(
        string name,
        Transform parent,
        Vector2 position,
        string key,
        Sprite disc,
        Sprite normalIcon,
        Sprite readyIcon,
        Sprite frameSprite,
        Sprite sheenSprite,
        Vector2 iconSize,
        bool useReadySheen,
        bool dimUntilReady)
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

        Image icon = CreateImage(
            "Icon",
            root.transform,
            normalIcon,
            Color.white);
        SetRect(icon.rectTransform, iconSize, new Vector2(0f, 13f));
        icon.preserveAspect = true;

        Image frame = null;
        if (frameSprite != null)
        {
            frame = CreateImage("Frame", root.transform, frameSprite, Color.white);
            SetRect(frame.rectTransform, new Vector2(88f, 88f), new Vector2(0f, 13f));
            frame.preserveAspect = true;
        }

        Image sheen = null;
        if (useReadySheen && sheenSprite != null)
        {
            Image sheenViewport = CreateImage(
                "ReadySheenMask",
                root.transform,
                disc,
                Color.white);
            SetRect(
                sheenViewport.rectTransform,
                new Vector2(82f, 82f),
                new Vector2(0f, 13f));
            sheenViewport.preserveAspect = true;
            Mask circularMask = sheenViewport.gameObject.AddComponent<Mask>();
            circularMask.showMaskGraphic = false;

            sheen = CreateImage(
                "ReadySheen",
                sheenViewport.transform,
                sheenSprite,
                Color.white);
            SetRect(
                sheen.rectTransform,
                new Vector2(116f, 66f),
                new Vector2(-104f, 104f));
            sheen.preserveAspect = true;
            sheen.gameObject.SetActive(false);
        }

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
            icon = icon,
            keyLabel = keyText,
            normalSprite = normalIcon,
            readySprite = readyIcon,
            readySheen = sheen,
            dimUntilReady = dimUntilReady
        };
    }

    private static void ConfigureAssaultHud(
        GameObject root,
        BossView boss,
        AssaultInfoView assaultInfo)
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

        hud.ConfigureAssaultInfoView(
            assaultInfo.timerProgressFill,
            assaultInfo.rankIndicators);
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
                visuals.localScale = Vector3.one * 0.98f;

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

    private static Sprite LoadActionButtonSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(
            $"{ActionButtonSpriteRoot}/{name}.png");
    }

    private static Sprite LoadAssaultFairySprite(string name)
    {
        string path = $"{AssaultFairySpriteRoot}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
            return sprite;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"Fairy 강습전 HUD 스프라이트를 가져오지 못했습니다: {path}");
        return sprite;
    }

    /// <summary>
    /// 추출한 보스 HUD 이미지를 UI용 단일 Sprite 설정으로 통일해 불러온다.
    /// 얇은 게이지의 가장자리가 뭉개지지 않도록 밉맵과 압축은 사용하지 않는다.
    /// </summary>
    private static Sprite LoadBossStatusSprite(string name)
    {
        string path = $"{BossStatusSpriteRoot}/{name}.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Sprite ||
             importer.spriteImportMode != SpriteImportMode.Single ||
             importer.mipmapEnabled ||
             importer.textureCompression != TextureImporterCompression.Uncompressed ||
             importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"보스 상태 HUD 원본 스프라이트를 가져오지 못했습니다: {path}");
        return sprite;
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

    private sealed class AssaultInfoView
    {
        public Image timerProgressFill;
        public Image[] rankIndicators;
    }

    private sealed class GaugeView
    {
        public Image fill;
    }
}
