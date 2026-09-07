using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AssaultScoreboardV1Presenter : MonoBehaviour
{
    [SerializeField] private AssaultBattleController battleController;
    [SerializeField] private RectTransform bubbleRoot;
    [SerializeField] private RectTransform fairyIconRoot;
    [SerializeField] private Text currentScoreText;
    [SerializeField] private Text operationScoreText;
    [SerializeField] private Text targetScoreText;
    [SerializeField] private Image[] rankMedals = new Image[3];
    [SerializeField] private Sprite activeMedalSprite;
    [SerializeField] private Sprite inactiveMedalSprite;
    [SerializeField, Min(0)] private int bRankScore = 6000;
    [SerializeField, Min(0)] private int aRankScore = 14000;
    [SerializeField, Min(0)] private int sRankScore = 20000;
    [SerializeField, Min(0.01f)] private float scoreCountDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float collapseDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float expandDuration = 0.44f;

    private float displayedScore;
    private int targetScore;
    private int appliedRank;
    private int pendingRank;
    private bool initialized;
    private bool subscribed;
    private Coroutine rankTransition;

    public void Configure(
        RectTransform newBubbleRoot,
        RectTransform newFairyIconRoot,
        Text newCurrentScoreText,
        Text newOperationScoreText,
        Text newTargetScoreText,
        Image[] newRankMedals,
        Sprite newActiveMedalSprite,
        Sprite newInactiveMedalSprite)
    {
        bubbleRoot = newBubbleRoot;
        fairyIconRoot = newFairyIconRoot;
        currentScoreText = newCurrentScoreText;
        operationScoreText = newOperationScoreText;
        targetScoreText = newTargetScoreText;
        rankMedals = newRankMedals;
        activeMedalSprite = newActiveMedalSprite;
        inactiveMedalSprite = newInactiveMedalSprite;
    }

    private void Awake()
    {
        ResolveBattleController();
        ResetTransforms();
    }

    private void OnEnable()
    {
        ResolveBattleController();
        Subscribe();
        InitializeFromBattle();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (rankTransition != null)
        {
            StopCoroutine(rankTransition);
            rankTransition = null;
        }

        ResetTransforms();
    }

    private void Update()
    {
        if (!initialized)
            return;

        float difference = Mathf.Abs(targetScore - displayedScore);
        if (difference > 0.01f)
        {
            float duration = Mathf.Max(0.01f, scoreCountDuration);
            float speed = Mathf.Max(120f, difference / duration);
            displayedScore = Mathf.MoveTowards(
                displayedScore,
                targetScore,
                speed * Time.unscaledDeltaTime);
            RefreshScoreTexts();
        }
    }

    private void ResolveBattleController()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();
    }

    private void Subscribe()
    {
        if (subscribed || battleController == null)
            return;

        battleController.ScoreChanged += HandleScoreChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || battleController == null)
            return;

        battleController.ScoreChanged -= HandleScoreChanged;
        subscribed = false;
    }

    private void InitializeFromBattle()
    {
        int score = battleController != null
            ? Mathf.Max(0, battleController.CurrentScore)
            : 0;
        targetScore = score;
        displayedScore = score;
        appliedRank = EvaluateRank(score);
        pendingRank = appliedRank;
        initialized = true;
        ApplyMedalState(appliedRank);
        RefreshScoreTexts();
    }

    private void HandleScoreChanged(int score)
    {
        targetScore = Mathf.Max(0, score);
        int nextRank = EvaluateRank(targetScore);
        pendingRank = nextRank;

        if (!initialized)
        {
            displayedScore = targetScore;
            appliedRank = nextRank;
            initialized = true;
            ApplyMedalState(appliedRank);
            RefreshScoreTexts();
            return;
        }

        if (nextRank > appliedRank)
        {
            if (rankTransition == null && isActiveAndEnabled)
                rankTransition = StartCoroutine(PlayRankTransition());
        }
        else if (nextRank < appliedRank)
        {
            appliedRank = nextRank;
            ApplyMedalState(appliedRank);
        }

        RefreshScoreTexts();
    }

    private void RefreshScoreTexts()
    {
        int visibleScore = Mathf.Max(0, Mathf.RoundToInt(displayedScore));
        int operationScore = battleController != null
            ? Mathf.Max(0, battleController.OperationScore)
            : 0;

        int visibleTarget = GetVisibleRankTarget(visibleScore);
        if (currentScoreText != null &&
            operationScoreText == null &&
            targetScoreText == null)
        {
            currentScoreText.text =
                $"<color=#D9A32A>{visibleScore}   ({operationScore})</color>" +
                $"    <color=#DDE1E1>/ {visibleTarget}</color>";
            return;
        }

        if (currentScoreText != null)
            currentScoreText.text = visibleScore.ToString();
        if (operationScoreText != null)
            operationScoreText.text = $"({operationScore})";
        if (targetScoreText != null)
            targetScoreText.text = $"/ {visibleTarget}";
    }

    private int GetVisibleRankTarget(int score)
    {
        if (score < bRankScore)
            return bRankScore;
        if (score < aRankScore)
            return aRankScore;
        return sRankScore;
    }

    private int EvaluateRank(int score)
    {
        int rank = 0;
        if (score >= bRankScore)
            rank++;
        if (score >= aRankScore)
            rank++;
        if (score >= sRankScore)
            rank++;
        return rank;
    }

    private void ApplyMedalState(int rank)
    {
        if (rankMedals == null)
            return;

        int count = Mathf.Min(rankMedals.Length, 3);
        for (int index = 0; index < count; index++)
        {
            Image medal = rankMedals[index];
            if (medal == null)
                continue;

            bool active = index < rank;
            medal.sprite = active ? activeMedalSprite : inactiveMedalSprite;
            medal.color = Color.white;
        }
    }

    private IEnumerator PlayRankTransition()
    {
        while (pendingRank > appliedRank)
        {
            yield return AnimateFold(1f, 0.025f, collapseDuration, false);

            appliedRank = pendingRank;
            ApplyMedalState(appliedRank);

            yield return AnimateFold(0.025f, 1f, expandDuration, true);
            ResetTransforms();
        }

        rankTransition = null;
    }

    private IEnumerator AnimateFold(
        float start,
        float end,
        float duration,
        bool overshoot)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = overshoot ? EaseOutBack(t) : t * t * t;
            float scaleX = Mathf.LerpUnclamped(start, end, eased);

            if (bubbleRoot != null)
                bubbleRoot.localScale = new Vector3(scaleX, 1f, 1f);

            if (fairyIconRoot != null)
            {
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.11f;
                fairyIconRoot.localScale = Vector3.one * pulse;
            }

            yield return null;
        }

        if (bubbleRoot != null)
            bubbleRoot.localScale = new Vector3(end, 1f, 1f);
        if (fairyIconRoot != null)
            fairyIconRoot.localScale = Vector3.one;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.35f;
        float shifted = t - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted
            + overshoot * shifted * shifted;
    }

    private void ResetTransforms()
    {
        if (bubbleRoot != null)
            bubbleRoot.localScale = Vector3.one;
        if (fairyIconRoot != null)
            fairyIconRoot.localScale = Vector3.one;
    }

    private void OnValidate()
    {
        bRankScore = Mathf.Max(0, bRankScore);
        aRankScore = Mathf.Max(bRankScore, aRankScore);
        sRankScore = Mathf.Max(aRankScore, sRankScore);
    }
}
