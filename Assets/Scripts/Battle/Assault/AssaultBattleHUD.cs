using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 강습전 진행 정보와 WIPEOUT·결과 화면을 표시하고 전투 관리자의 이벤트를 화면 값으로 변환한다.
/// 연결된 뷰가 없으면 런타임 기본 뷰를 생성해 테스트 가능한 폴백을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssaultBattleHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AssaultBattleController battleController;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private Text timerText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text damageText;
    [SerializeField] private Image scoreFill;

    [Header("Boss Status")]
    [SerializeField] private Image bossHealthFill;
    [SerializeField] private Image bossStunFill;
    [SerializeField] private Text bossHealthText;
    [SerializeField] private Text bossStunText;
    [SerializeField] private Color bossHealthColor = new Color32(93, 238, 35, 255);
    [SerializeField] private Color bossStunColor = new Color32(255, 205, 24, 255);
    [SerializeField] private Color bossExhaustedStunColor = new Color32(112, 118, 126, 255);

    [Header("Score Display")]
    [Tooltip("실제 점수가 변했을 때 HUD 숫자가 목표 점수를 따라잡는 기준 시간이다.")]
    [SerializeField, Min(0.01f)] private float scoreCountDuration = 0.18f;
    [Tooltip("작은 점수 변화도 느려 보이지 않게 보장하는 초당 최소 증가량이다.")]
    [SerializeField, Min(1f)] private float minimumScoreCountSpeed = 120f;

    [Header("Wipeout")]
    [SerializeField] private CanvasGroup wipeoutGroup;
    [SerializeField] private Image wipeoutTint;
    [SerializeField] private Text wipeoutText;
    [SerializeField] private RawImage wipeoutFreezeFrame;
    [SerializeField] private Image wipeoutFlash;
    [SerializeField] private Image wipeoutBand;
    [SerializeField] private Image wipeoutTopLine;
    [SerializeField] private Image wipeoutBottomLine;
    [SerializeField] private Text wipeoutCyanEcho;
    [SerializeField] private Text wipeoutRedEcho;
    [Tooltip("전투가 멈춘 뒤 WIPEOUT 문구를 보여주는 실제 시간이다.")]
    [SerializeField, Min(0.1f)] private float wipeoutDuration = 5f;
    [SerializeField, Min(0.05f)] private float wipeoutIntroDuration = 0.24f;
    [SerializeField, Min(0.05f)] private float wipeoutOutroDuration = 0.24f;
    [SerializeField] private Color wipeoutYellow = new Color32(244, 232, 0, 255);
    [SerializeField] private Color wipeoutCyan = new Color32(36, 229, 241, 210);
    [SerializeField] private Color wipeoutRed = new Color32(255, 39, 91, 210);

    [Header("Result")]
    [SerializeField] private CanvasGroup resultGroup;
    [SerializeField] private Text resultReasonText;
    [SerializeField] private Text resultRankText;
    [SerializeField] private Text resultScoreText;
    [SerializeField] private Text resultDamageText;
    [SerializeField] private Text resultGoalsText;
    [SerializeField] private Image bossImage;
    [Tooltip("결과 화면에 표시할 보스 초상입니다. 비어 있으면 보스 정보 대체 패널을 표시합니다.")]
    [SerializeField] private Sprite bossPortrait;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button retryButton;

    [Header("Rank Score")]
    [Tooltip("B 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int bRankScore = 8000;
    [Tooltip("A 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int aRankScore = 16000;
    private ZZZWipeoutLayeredDirector wipeoutPresenter;
    [Tooltip("S 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int sRankScore = 25000;

    private bool subscribed;
    private bool ownsBattlePause;
    private bool ownsResultCursor;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private Coroutine finishSequence;
    private float displayedScore;
    private int targetScore;
    private bool scoreDisplayInitialized;
    private bool HasView =>
        timerText != null ||
        scoreText != null ||
        wipeoutGroup != null ||
        resultGroup != null;

    private bool CanBuildRuntimeView =>
        transform is RectTransform &&
        GetComponentInParent<Canvas>() != null;

    private void Awake()
    {
        ResolveBattleController();
        EnsurePresentationView();
        RefreshAll();
    }

    private void OnEnable()
    {
        ResolveBattleController();
        EnsurePresentationView();
        BindExitButton();
        BindRetryButton();
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        RefreshAll();
    }

    private void LateUpdate()
    {
        if (battleController == null)
        {
            ResolveBattleController();
            Subscribe();
        }

        if (battleController == null ||
            battleController.State != AssaultBattleState.Fighting)
        {
            return;
        }

        // 이벤트 연결 여부와 관계없이 화면은 현재 전투 원본 값을 최종적으로 따라간다.
        UpdateTimer(battleController.RemainingTime);
        UpdateScore(battleController.CurrentScore);
        UpdateDisplayedScore();
        UpdateBossStatus();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnbindExitButton();
        UnbindRetryButton();

        if (finishSequence != null)
        {
            StopCoroutine(finishSequence);
            finishSequence = null;
        }

        RestoreBattleTime();
        ReleaseResultCursor();
        scoreDisplayInitialized = false;
    }

    public void Configure(AssaultBattleController controller)
    {
        Unsubscribe();
        battleController = controller;

        if (isActiveAndEnabled)
            Subscribe();

        RefreshAll();
    }

    public void ConfigureView(
        CanvasGroup battleHudGroup,
        Text battleTimerText,
        Text battleScoreText,
        Text battleDamageText,
        Image battleScoreFill,
        Image battleBossHealthFill,
        Image battleBossStunFill,
        Text battleBossHealthText,
        Text battleBossStunText,
        CanvasGroup battleResultGroup,
        Text battleResultReasonText,
        Text battleResultRankText,
        Text battleResultScoreText,
        Text battleResultDamageText,
        CanvasGroup battleWipeoutGroup,
        Image battleWipeoutTint,
        Text battleWipeoutText,
        Image battleBossImage,
        Button battleExitButton)
    {
        hudGroup = battleHudGroup;
        timerText = battleTimerText;
        scoreText = battleScoreText;
        damageText = battleDamageText;
        scoreFill = battleScoreFill;
        bossHealthFill = battleBossHealthFill;
        bossStunFill = battleBossStunFill;
        bossHealthText = battleBossHealthText;
        bossStunText = battleBossStunText;
        resultGroup = battleResultGroup;
        resultReasonText = battleResultReasonText;
        resultRankText = battleResultRankText;
        resultScoreText = battleResultScoreText;
        resultDamageText = battleResultDamageText;
        wipeoutGroup = battleWipeoutGroup;
        wipeoutTint = battleWipeoutTint;
        wipeoutText = battleWipeoutText;
        bossImage = battleBossImage;
        exitButton = battleExitButton;
        EnsurePresentationView();
        BindExitButton();
        BindRetryButton();
        RefreshAll();
    }

    public void SetBossPortrait(Sprite portrait)
    {
        bossPortrait = portrait;
        RefreshBossPortrait();
    }

    public void QuitGame()
    {
        ReleaseResultCursor();
        RestoreBattleTime();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RetryBattle()
    {
        ReleaseResultCursor();
        RestoreBattleTime();
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private void ResolveBattleController()
    {
        if (battleController == null)
            battleController = GetComponent<AssaultBattleController>();

        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();
    }

    private void Subscribe()
    {
        if (!Application.isPlaying ||
            battleController == null ||
            !HasView ||
            subscribed)
        {
            return;
        }

        battleController.BattleStarted += OnBattleStarted;
        battleController.RemainingTimeChanged += UpdateTimer;
        battleController.ScoreChanged += UpdateScore;
        battleController.BattleFinished += OnBattleFinished;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (battleController == null || !subscribed)
            return;

        battleController.BattleStarted -= OnBattleStarted;
        battleController.RemainingTimeChanged -= UpdateTimer;
        battleController.ScoreChanged -= UpdateScore;
        battleController.BattleFinished -= OnBattleFinished;
        subscribed = false;
    }

    private void RefreshAll()
    {
        if (battleController == null)
        {
            SetGroupVisible(hudGroup, false);
            SetGroupVisible(wipeoutGroup, false);
            SetGroupVisible(resultGroup, false);
            return;
        }

        UpdateTimer(battleController.RemainingTime);
        UpdateScore(battleController.CurrentScore);
        RefreshBossPortrait();
        UpdateBossStatus();

        bool fighting = battleController.State == AssaultBattleState.Fighting;
        bool finished = battleController.State == AssaultBattleState.Finished;
        SetGroupVisible(hudGroup, fighting);
        SetGroupVisible(wipeoutGroup, false);
        SetGroupVisible(resultGroup, finished, finished);

        if (finished)
        {
            AcquireResultCursor();
            AssaultBattleEndReason? reason = battleController.HasFinalResult
                ? battleController.FinalEndReason
                : null;
            UpdateResult(reason);
        }
        else
        {
            ReleaseResultCursor();
        }
    }

    private void OnBattleStarted()
    {
        ReleaseResultCursor();
        RestoreBattleTime();
        SetGroupVisible(resultGroup, false);
        SetGroupVisible(wipeoutGroup, false);
        SetGroupVisible(hudGroup, true);
        UpdateTimer(battleController.RemainingTime);
        SnapDisplayedScore(battleController.CurrentScore);
        RefreshScoreBreakdown();
        UpdateBossStatus();
    }

    private void OnBattleFinished(AssaultBattleEndReason reason)
    {
        if (finishSequence != null)
            StopCoroutine(finishSequence);

        finishSequence = StartCoroutine(PlayFinishSequence(reason));
    }

    private IEnumerator PlayFinishSequence(AssaultBattleEndReason reason)
    {
        PauseBattleTime();
        SetGroupVisible(hudGroup, false);
        SetGroupVisible(resultGroup, false);
        UpdateResult(reason);

        bool partyDefeated = reason == AssaultBattleEndReason.PartyDefeated;

        if (wipeoutPresenter == null)
            wipeoutPresenter = GetComponent<ZZZWipeoutLayeredDirector>();
        if (wipeoutPresenter == null)
            wipeoutPresenter = gameObject.AddComponent<ZZZWipeoutLayeredDirector>();


        wipeoutPresenter.Configure(
            wipeoutDuration,
            wipeoutIntroDuration,
            wipeoutOutroDuration,
            wipeoutYellow,
            wipeoutCyan,
            wipeoutRed);
        yield return wipeoutPresenter.Play(partyDefeated);
        AcquireResultCursor();
        SetGroupVisible(resultGroup, true, true);
        finishSequence = null;
        yield break;
    }

    private void UpdateTimer(float remainingTime)
    {
        if (timerText == null)
            return;

        timerText.text = FormatTime(remainingTime, true);
    }

    private void UpdateScore(int score)
    {
        targetScore = Mathf.Max(0, score);

        if (!scoreDisplayInitialized)
            SnapDisplayedScore(targetScore);

        RefreshScoreBreakdown();
    }

    private void UpdateDisplayedScore()
    {
        if (!scoreDisplayInitialized)
            return;

        float difference = Mathf.Abs(targetScore - displayedScore);
        if (difference <= 0.001f)
        {
            displayedScore = targetScore;
            ApplyDisplayedScore(targetScore);
            return;
        }

        // 히트 스톱 중에도 점수 숫자는 멈추지 않고 실제 누적 점수를 따라간다.
        float duration = Mathf.Max(0.01f, scoreCountDuration);
        float countSpeed = Mathf.Max(minimumScoreCountSpeed, difference / duration);
        displayedScore = Mathf.MoveTowards(
            displayedScore,
            targetScore,
            countSpeed * Time.unscaledDeltaTime);
        ApplyDisplayedScore(Mathf.RoundToInt(displayedScore));
    }

    private void SnapDisplayedScore(int score)
    {
        targetScore = Mathf.Max(0, score);
        displayedScore = targetScore;
        scoreDisplayInitialized = true;
        ApplyDisplayedScore(targetScore);
    }

    private void ApplyDisplayedScore(int score)
    {
        int safeScore = Mathf.Max(0, score);

        if (scoreText != null)
            scoreText.text = $"{safeScore:00000}";

        if (scoreFill != null && battleController != null)
        {
            float maximumScore = Mathf.Max(1, battleController.MaximumTotalScore);
            scoreFill.fillAmount = Mathf.Clamp01(safeScore / maximumScore);
        }
    }

    private void RefreshScoreBreakdown()
    {
        if (damageText != null && battleController != null)
        {
            damageText.text =
                $"DMG {battleController.DamageScore:00000}  " +
                $"OP {battleController.OperationScore:0000}";
        }
    }

    private void UpdateBossStatus()
    {
        EnemyController boss = battleController != null ? battleController.Boss : null;
        float healthNormalized = boss != null ? boss.CurrentHpNormalized : 0f;
        float stunNormalized = boss != null ? boss.CurrentStunNormalized : 0f;

        if (bossHealthFill != null)
        {
            bossHealthFill.fillAmount = healthNormalized;
            bossHealthFill.color = bossHealthColor;
        }

        if (bossStunFill != null)
        {
            bossStunFill.fillAmount = stunNormalized;
            bool isGroggy = boss != null && boss.IsGroggy;
            bool isChainExhausted = isGroggy && boss.IsChainSkillSequenceComplete;

            bossStunFill.color = isChainExhausted
                ? bossExhaustedStunColor
                : isGroggy
                    ? Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 1.6f, 1f), 0.8f, 1f)
                    : bossStunColor;
        }

        if (bossHealthText != null)
        {
            bossHealthText.text = boss != null
                ? Mathf.RoundToInt(boss.CurrentHp).ToString("N0") + " / " +
                  Mathf.RoundToInt(boss.MaxHp).ToString("N0")
                : "-- / --";
        }

        if (bossStunText != null)
        {
            int percent = Mathf.RoundToInt(stunNormalized * 100f);
            bossStunText.text = boss != null
                ? (boss.IsGroggy ? "STUN " : "DAZE ") + percent + "%"
                : "DAZE --";
        }
    }
    private void UpdateResult(AssaultBattleEndReason? reason)
    {
        if (battleController == null)
            return;

        bool hasFinalResult = battleController.HasFinalResult;
        int score = Mathf.Max(
            0,
            hasFinalResult
                ? battleController.FinalScore
                : battleController.CurrentScore);
        int damageScore = Mathf.Max(
            0,
            hasFinalResult
                ? battleController.FinalDamageScore
                : battleController.DamageScore);
        int operationScore = Mathf.Max(
            0,
            hasFinalResult
                ? battleController.FinalOperationScore
                : battleController.OperationScore);
        float elapsedTime = hasFinalResult
            ? battleController.FinalElapsedTime
            : battleController.ElapsedTime;
        AssaultBattleEndReason? resolvedReason = reason;
        if (!resolvedReason.HasValue && hasFinalResult)
            resolvedReason = battleController.FinalEndReason;

        bool missionFailed = resolvedReason == AssaultBattleEndReason.PartyDefeated;
        if (resultReasonText != null)
        {
            resultReasonText.text =
                $"{(missionFailed ? "작전 실패" : "작전 완료")}\n" +
                $"총 소요 시간    {FormatTime(elapsedTime, false)}";
        }

        string rank = EvaluateRank(score);
        if (resultRankText != null)
        {
            resultRankText.text = rank;
            resultRankText.color = rank == "S"
                ? new Color32(255, 178, 25, 255)
                : rank == "A"
                    ? new Color32(55, 219, 255, 255)
                    : rank == "B"
                        ? new Color32(126, 227, 93, 255)
                        : new Color32(176, 181, 184, 255);
        }

        if (resultScoreText != null)
            resultScoreText.text = $"총 점수\n{score:N0}";

        if (resultDamageText != null)
        {
            resultDamageText.text =
                $"피해 점수    {damageScore:N0}\n" +
                $"조작 점수    {operationScore:N0}";
        }

        if (resultGoalsText != null)
        {
            resultGoalsText.text =
                $"{BuildGoalLine(score, sRankScore, "S 랭크")}\n" +
                $"{BuildGoalLine(score, aRankScore, "A 랭크")}\n" +
                $"{BuildGoalLine(score, bRankScore, "B 랭크")}";
        }
    }

    private static string BuildGoalLine(int score, int targetScore, string label)
    {
        bool achieved = score >= targetScore;
        string color = achieved ? "55E56B" : "777C7C";
        string marker = achieved ? "✓" : "○";
        string progress = achieved ? "1/1" : "0/1";
        return $"<color=#{color}>{marker}  {label} · {targetScore:N0}점 이상      {progress}</color>";
    }

    private string EvaluateRank(int score)
    {
        if (score >= sRankScore)
            return "S";
        if (score >= aRankScore)
            return "A";
        if (score >= bRankScore)
            return "B";
        return "C";
    }

    private static string FormatTime(float time, bool roundUp)
    {
        int totalSeconds = roundUp
            ? Mathf.Max(0, Mathf.CeilToInt(time))
            : Mathf.Max(0, Mathf.FloorToInt(time));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void PauseBattleTime()
    {
        if (ownsBattlePause)
            return;

        HitStop.SetExternalPause(true);
        ownsBattlePause = true;
    }

    private void RestoreBattleTime()
    {
        if (!ownsBattlePause)
            return;

        HitStop.SetExternalPause(false);
        ownsBattlePause = false;
    }

    private void AcquireResultCursor()
    {
        if (!ownsResultCursor)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsResultCursor = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReleaseResultCursor()
    {
        if (!ownsResultCursor)
            return;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        ownsResultCursor = false;
    }

    private void EnsurePresentationView()
    {
        if (!CanBuildRuntimeView)
            return;

        EnsureWipeoutView();
        EnsureResultExtras();
        RefreshBossPortrait();
    }

    private void EnsureWipeoutView()
    {
        if (wipeoutGroup != null)
            return;

        GameObject root = new GameObject(
            "WipeoutView",
            typeof(RectTransform),
            typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();
        wipeoutGroup = root.GetComponent<CanvasGroup>();

        GameObject tintObject = new GameObject(
            "YellowTint",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        tintObject.transform.SetParent(root.transform, false);
        Stretch(tintObject.GetComponent<RectTransform>());
        wipeoutTint = tintObject.GetComponent<Image>();
        wipeoutTint.color = new Color(1f, 0.72f, 0.05f, 0.82f);
        wipeoutTint.raycastTarget = false;

        GameObject textObject = new GameObject(
            "WipeoutText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline));
        textObject.transform.SetParent(root.transform, false);
        SetRect(textObject.GetComponent<RectTransform>(), new Vector2(900f, 180f), Vector2.zero);
        wipeoutText = textObject.GetComponent<Text>();
        wipeoutText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        wipeoutText.text = "WIPEOUT";
        wipeoutText.fontSize = 112;
        wipeoutText.fontStyle = FontStyle.BoldAndItalic;
        wipeoutText.alignment = TextAnchor.MiddleCenter;
        wipeoutText.color = Color.white;
        wipeoutText.raycastTarget = false;
        wipeoutText.horizontalOverflow = HorizontalWrapMode.Overflow;
        wipeoutText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.06f, 0f, 1f);
        outline.effectDistance = new Vector2(6f, -6f);
        SetGroupVisible(wipeoutGroup, false);
    }

    private void EnsureResultExtras()
    {
        if (resultGroup == null)
            return;

        Transform panel = resultGroup.transform.Find("ResultPanel");
        if (panel == null)
            panel = resultGroup.transform;

        if (panel is RectTransform panelRect)
            SetRect(panelRect, new Vector2(1660f, 720f), Vector2.zero);

        StyleResultRoot(resultGroup.transform, panel);
        EnsureResultBackdrop(panel, "LeftSummaryPanel", new Vector2(500f, 430f),
            new Vector2(-565f, -35f));
        EnsureResultBackdrop(panel, "BossSummaryPanel", new Vector2(470f, 430f),
            new Vector2(560f, -35f));
        EnsureResultGoalsView(panel);

        if (bossImage == null)
        {
            Transform existingBossImage = panel.Find("BossImage");
            if (existingBossImage != null)
                bossImage = existingBossImage.GetComponent<Image>();
        }

        if (bossImage == null)
        {
            GameObject bossObject = new GameObject(
                "BossImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            bossObject.transform.SetParent(panel, false);
            SetRect(
                bossObject.GetComponent<RectTransform>(),
                new Vector2(230f, 260f),
                new Vector2(315f, 20f));
            bossImage = bossObject.GetComponent<Image>();
            bossImage.color = new Color32(31, 34, 35, 255);
            bossImage.raycastTarget = false;

            Outline outline = bossObject.GetComponent<Outline>();
            outline.effectColor = new Color32(224, 245, 30, 255);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject labelObject = new GameObject(
                "BossLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(bossObject.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "DEAD END\nBUTCHER";
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(190, 194, 194, 255);
            label.raycastTarget = false;
        }

        if (exitButton == null)
        {
            Transform existingExit = panel.Find("ExitButton");
            if (existingExit != null)
                exitButton = existingExit.GetComponent<Button>();
        }

        if (exitButton == null)
            exitButton = CreateResultButton(panel, "ExitButton", "나가기");

        if (retryButton == null)
        {
            Transform existingRetry = panel.Find("RetryButton");
            if (existingRetry != null)
                retryButton = existingRetry.GetComponent<Button>();
        }

        if (retryButton == null)
            retryButton = CreateResultButton(panel, "RetryButton", "다시 도전");

        ArrangeResultPanel(panel);
        StyleResultContents(panel);
    }

    private void EnsureResultGoalsView(Transform panel)
    {
        if (resultGoalsText == null)
        {
            Transform existing = panel.Find("GoalList");
            if (existing != null)
                resultGoalsText = existing.GetComponent<Text>();
        }

        if (resultGoalsText != null)
            return;

        GameObject goalsObject = new GameObject(
            "GoalList",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        goalsObject.transform.SetParent(panel, false);
        SetRect(
            goalsObject.GetComponent<RectTransform>(),
            new Vector2(430f, 105f),
            new Vector2(-25f, -100f));

        resultGoalsText = goalsObject.GetComponent<Text>();
        resultGoalsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultGoalsText.fontSize = 21;
        resultGoalsText.fontStyle = FontStyle.Bold;
        resultGoalsText.alignment = TextAnchor.UpperLeft;
        resultGoalsText.color = Color.white;
        resultGoalsText.raycastTarget = false;
        resultGoalsText.supportRichText = true;
        resultGoalsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultGoalsText.verticalOverflow = VerticalWrapMode.Overflow;
        resultGoalsText.lineSpacing = 1.05f;
    }

    private static void ArrangeResultPanel(Transform panel)
    {
        SetChildRect(panel, "Accent", new Vector2(1660f, 8f), new Vector2(0f, 356f));
        SetChildRect(panel, "Header", new Vector2(760f, 92f), new Vector2(0f, 278f));

        SetChildRect(panel, "Reason", new Vector2(430f, 82f), new Vector2(-565f, 130f));
        SetChildRect(panel, "Score", new Vector2(430f, 105f), new Vector2(-565f, 35f));
        SetChildRect(panel, "Damage", new Vector2(430f, 82f), new Vector2(-565f, -70f));
        SetChildRect(panel, "GoalList", new Vector2(430f, 165f), new Vector2(-565f, -198f));

        SetChildRect(panel, "Rank", new Vector2(460f, 430f), new Vector2(0f, -18f));

        SetChildRect(panel, "Hint", new Vector2(430f, 46f), new Vector2(560f, 190f));
        SetChildRect(panel, "BossImage", new Vector2(400f, 275f), new Vector2(560f, -15f));

        SetChildRect(panel, "RetryButton", new Vector2(300f, 62f), new Vector2(235f, -308f));
        SetChildRect(panel, "ExitButton", new Vector2(300f, 62f), new Vector2(570f, -308f));
    }

    private static void SetChildRect(
        Transform parent,
        string childName,
        Vector2 size,
        Vector2 position)
    {
        Transform child = parent.Find(childName);
        if (child is RectTransform childRect)
            SetRect(childRect, size, position);
    }

    private static void StyleResultRoot(Transform resultRoot, Transform panel)
    {
        Transform dim = resultRoot.Find("Dim");
        if (dim != null && dim.TryGetComponent(out Image dimImage))
            dimImage.color = new Color32(0, 0, 0, 238);

        if (panel.TryGetComponent(out Image panelImage))
            panelImage.color = new Color32(25, 35, 37, 252);

        Transform accent = panel.Find("Accent");
        if (accent != null && accent.TryGetComponent(out Image accentImage))
            accentImage.color = new Color32(255, 180, 23, 255);
    }

    private static void EnsureResultBackdrop(
        Transform parent,
        string objectName,
        Vector2 size,
        Vector2 position)
    {
        Transform existing = parent.Find(objectName);
        GameObject backdropObject;

        if (existing == null)
        {
            backdropObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            backdropObject.transform.SetParent(parent, false);
        }
        else
        {
            backdropObject = existing.gameObject;
        }

        SetRect(backdropObject.GetComponent<RectTransform>(), size, position);
        Image image = backdropObject.GetComponent<Image>();
        image.color = objectName == "BossSummaryPanel"
            ? new Color32(10, 14, 16, 245)
            : new Color32(13, 19, 21, 238);
        image.raycastTarget = false;

        Outline outline = backdropObject.GetComponent<Outline>();
        if (outline == null)
            outline = backdropObject.AddComponent<Outline>();
        outline.effectColor = new Color32(73, 84, 87, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        backdropObject.transform.SetAsFirstSibling();
    }

    private static void StyleResultContents(Transform panel)
    {
        StyleResultText(panel, "Header", "도전 결과", 62, FontStyle.Bold,
            TextAnchor.MiddleCenter, new Color32(212, 216, 216, 255));
        StyleResultText(panel, "Reason", null, 27, FontStyle.Bold,
            TextAnchor.MiddleLeft, Color.white);
        StyleResultText(panel, "Rank", null, 260, FontStyle.BoldAndItalic,
            TextAnchor.MiddleCenter, new Color32(255, 178, 25, 255));
        StyleResultText(panel, "Score", null, 38, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Color32(255, 198, 32, 255));
        StyleResultText(panel, "Damage", null, 23, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Color32(219, 224, 225, 255));
        StyleResultText(panel, "GoalList", null, 21, FontStyle.Bold,
            TextAnchor.UpperLeft, Color.white);
        StyleResultText(panel, "Hint", "DEAD END BUTCHER", 28, FontStyle.Bold,
            TextAnchor.MiddleCenter, new Color32(221, 224, 225, 255));

        Transform rankTransform = panel.Find("Rank");
        if (rankTransform != null)
        {
            Outline outline = rankTransform.GetComponent<Outline>();
            if (outline == null)
                outline = rankTransform.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(100, 46, 0, 255);
            outline.effectDistance = new Vector2(6f, -6f);

        }

        Transform bossTransform = panel.Find("BossImage");
        if (bossTransform != null && bossTransform.TryGetComponent(out Image portrait))
        {
            portrait.preserveAspect = true;
            Outline outline = bossTransform.GetComponent<Outline>();
            if (outline == null)
                outline = bossTransform.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 180, 23, 255);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        StyleResultButton(panel.Find("RetryButton"), "다시 도전");
        StyleResultButton(panel.Find("ExitButton"), "나가기");
    }

    private static void StyleResultText(
        Transform parent,
        string childName,
        string value,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
    {
        Transform child = parent.Find(childName);
        if (child == null || !child.TryGetComponent(out Text text))
            return;

        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (value != null)
            text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.12f;
    }

    private static void StyleResultButton(Transform buttonTransform, string labelValue)
    {
        if (buttonTransform == null)
            return;

        Button button = buttonTransform.GetComponent<Button>();
        Image background = buttonTransform.GetComponent<Image>();
        if (button == null || background == null)
            return;

        background.color = new Color32(18, 23, 25, 255);
        background.raycastTarget = true;
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(18, 23, 25, 255);
        colors.highlightedColor = new Color32(112, 92, 32, 255);
        colors.pressedColor = new Color32(255, 188, 28, 255);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Transform labelTransform = buttonTransform.Find("Label");
        if (labelTransform != null && labelTransform.TryGetComponent(out Text label))
        {
            label.text = labelValue;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 25;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }

    private static Button CreateResultButton(Transform parent, string objectName, string labelValue)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(300f, 62f), Vector2.zero);

        Image background = buttonObject.GetComponent<Image>();
        background.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Stretch(labelObject.GetComponent<RectTransform>());

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = labelValue;
        label.raycastTarget = false;
        return button;
    }

    private void RefreshBossPortrait()
    {
        if (bossImage == null)
            return;

        bossImage.sprite = bossPortrait;
        bossImage.preserveAspect = true;
        bossImage.color = bossPortrait != null
            ? Color.white
            : new Color32(31, 34, 35, 255);

        Transform label = bossImage.transform.Find("BossLabel");
        if (label != null)
            label.gameObject.SetActive(bossPortrait == null);
    }

    private void BindExitButton()
    {
        if (exitButton == null)
            return;

        exitButton.onClick.RemoveListener(QuitGame);
        exitButton.onClick.AddListener(QuitGame);
    }

    private void UnbindExitButton()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(QuitGame);
    }
    private void BindRetryButton()
    {
        if (retryButton == null)
            return;

        retryButton.onClick.RemoveListener(RetryBattle);
        retryButton.onClick.AddListener(RetryBattle);
    }

    private void UnbindRetryButton()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryBattle);
    }


    private static void SetGroupVisible(
        CanvasGroup group,
        bool visible,
        bool interactive = false)
    {
        if (group == null)
            return;
        if (visible)
            group.transform.SetAsLastSibling();


        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && interactive;
        group.blocksRaycasts = visible && interactive;
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
}
