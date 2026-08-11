using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Wipeout")]
    [SerializeField] private CanvasGroup wipeoutGroup;
    [SerializeField] private Image wipeoutTint;
    [SerializeField] private Text wipeoutText;
    [Tooltip("전투가 멈춘 뒤 WIPEOUT 문구를 보여주는 실제 시간이다.")]
    [SerializeField, Min(0.1f)] private float wipeoutDuration = 1.35f;

    [Header("Result")]
    [SerializeField] private CanvasGroup resultGroup;
    [SerializeField] private Text resultReasonText;
    [SerializeField] private Text resultRankText;
    [SerializeField] private Text resultScoreText;
    [SerializeField] private Text resultDamageText;
    [SerializeField] private Image bossImage;
    [SerializeField] private Sprite bossPortrait;
    [SerializeField] private Button exitButton;

    [Header("Rank Score")]
    [Tooltip("B 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int bRankScore = 8000;
    [Tooltip("A 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int aRankScore = 16000;
    [Tooltip("S 등급에 필요한 최소 총점이다.")]
    [SerializeField, Min(0)] private int sRankScore = 25000;

    private bool subscribed;
    private bool ownsBattlePause;
    private Coroutine finishSequence;

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
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnbindExitButton();

        if (finishSequence != null)
        {
            StopCoroutine(finishSequence);
            finishSequence = null;
        }

        RestoreBattleTime();
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
        RefreshAll();
    }

    public void SetBossPortrait(Sprite portrait)
    {
        bossPortrait = portrait;
        RefreshBossPortrait();
    }

    public void QuitGame()
    {
        RestoreBattleTime();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

        bool fighting = battleController.State == AssaultBattleState.Fighting;
        bool finished = battleController.State == AssaultBattleState.Finished;
        SetGroupVisible(hudGroup, fighting);
        SetGroupVisible(wipeoutGroup, false);
        SetGroupVisible(resultGroup, finished, finished);

        if (finished)
            UpdateResult(null);
    }

    private void OnBattleStarted()
    {
        RestoreBattleTime();
        SetGroupVisible(resultGroup, false);
        SetGroupVisible(wipeoutGroup, false);
        SetGroupVisible(hudGroup, true);
        UpdateTimer(battleController.RemainingTime);
        UpdateScore(battleController.CurrentScore);
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

        if (wipeoutGroup != null)
        {
            SetGroupVisible(wipeoutGroup, true);
            float duration = Mathf.Max(0.1f, wipeoutDuration);
            float elapsed = 0f;
            Color baseTint = wipeoutTint != null
                ? wipeoutTint.color
                : new Color(1f, 0.72f, 0.05f, 0.82f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float fadeIn = Mathf.Clamp01(normalized / 0.12f);
                float fadeOut = Mathf.Clamp01((1f - normalized) / 0.22f);
                wipeoutGroup.alpha = Mathf.Min(fadeIn, fadeOut);

                if (wipeoutTint != null)
                {
                    float flash = 0.72f + Mathf.Sin(normalized * Mathf.PI * 6f) * 0.08f;
                    wipeoutTint.color = new Color(
                        baseTint.r,
                        baseTint.g,
                        baseTint.b,
                        Mathf.Clamp01(flash));
                }

                if (wipeoutText != null)
                {
                    float scale = Mathf.Lerp(1.22f, 1f, Mathf.SmoothStep(0f, 1f, normalized));
                    wipeoutText.rectTransform.localScale = Vector3.one * scale;
                }

                yield return null;
            }
        }

        SetGroupVisible(wipeoutGroup, false);
        SetGroupVisible(resultGroup, true, true);
        finishSequence = null;
    }

    private void UpdateTimer(float remainingTime)
    {
        if (timerText == null)
            return;

        timerText.text = FormatTime(remainingTime, true);
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"{Mathf.Max(0, score):00000}";

        if (damageText != null && battleController != null)
        {
            damageText.text =
                $"DMG {battleController.DamageScore:00000}  " +
                $"OP {battleController.OperationScore:0000}";
        }

        if (scoreFill != null && battleController != null)
        {
            float maximumScore = Mathf.Max(1, battleController.MaximumTotalScore);
            scoreFill.fillAmount = Mathf.Clamp01(score / maximumScore);
        }
    }

    private void UpdateResult(AssaultBattleEndReason? reason)
    {
        if (battleController == null)
            return;

        int score = Mathf.Max(0, battleController.CurrentScore);

        if (resultReasonText != null)
        {
            string resultReason = reason == AssaultBattleEndReason.BossDefeated
                ? "TARGET DEFEATED"
                : reason == AssaultBattleEndReason.TimeExpired
                    ? "TIME EXPIRED"
                    : "BATTLE COMPLETE";
            resultReasonText.text =
                $"{resultReason}  {FormatTime(battleController.ElapsedTime, false)}";
        }

        if (resultRankText != null)
            resultRankText.text = EvaluateRank(score);

        if (resultScoreText != null)
            resultScoreText.text = $"TOTAL SCORE  {score:00000}";

        if (resultDamageText != null)
        {
            resultDamageText.text =
                $"DAMAGE SCORE  {battleController.DamageScore:00000}   " +
                $"OPERATION  {battleController.OperationScore:0000}";
        }
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
        {
            panelRect.sizeDelta = new Vector2(
                Mathf.Max(920f, panelRect.sizeDelta.x),
                Mathf.Max(460f, panelRect.sizeDelta.y));
        }

        ArrangeResultPanel(panel);

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
            exitButton = resultGroup.GetComponentInChildren<Button>(true);

        if (exitButton == null)
            exitButton = CreateExitButton(panel);
    }

    private static void ArrangeResultPanel(Transform panel)
    {
        SetChildRect(panel, "Accent", new Vector2(920f, 9f), new Vector2(0f, 225f));
        SetChildRect(panel, "Header", new Vector2(420f, 42f), new Vector2(-230f, 190f));
        SetChildRect(panel, "Reason", new Vector2(390f, 36f), new Vector2(235f, 190f));
        SetChildRect(panel, "Rank", new Vector2(210f, 230f), new Vector2(-330f, 15f));
        SetChildRect(panel, "Score", new Vector2(400f, 58f), new Vector2(-40f, 60f));
        SetChildRect(panel, "Damage", new Vector2(430f, 42f), new Vector2(-25f, 4f));
        SetChildRect(panel, "Hint", new Vector2(430f, 30f), new Vector2(-25f, -52f));
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

    private static Button CreateExitButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "ExitButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetRect(
            buttonObject.GetComponent<RectTransform>(),
            new Vector2(230f, 58f),
            new Vector2(315f, -180f));

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color32(20, 23, 24, 255);
        background.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(20, 23, 24, 255);
        colors.highlightedColor = new Color32(62, 68, 50, 255);
        colors.pressedColor = new Color32(224, 245, 30, 255);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Stretch(labelObject.GetComponent<RectTransform>());
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "나가기";
        label.fontSize = 25;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
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

    private static void SetGroupVisible(
        CanvasGroup group,
        bool visible,
        bool interactive = false)
    {
        if (group == null)
            return;

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