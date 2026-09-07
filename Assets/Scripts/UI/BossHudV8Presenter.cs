using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class BossHudV8Presenter : MonoBehaviour
{
    private const int MaxDisplayedStunPercent = 99;

    [SerializeField] private Image healthTrailFill;
    [SerializeField] private Image currentHealthFill;
    [SerializeField] private Image stunFill;
    [SerializeField] private Text stunText;
    [SerializeField] private Color stunBackgroundColor = new Color32(71, 76, 78, 255);
    [SerializeField] private Color normalStunColor = new Color32(255, 205, 24, 255);
    [SerializeField] private Color normalStunTextColor = new Color32(255, 149, 20, 255);
    [SerializeField] private Color exhaustedStunColor = new Color32(112, 118, 126, 255);
    [SerializeField, Min(0.1f)] private float groggyHueCyclesPerSecond = 1.6f;
    [SerializeField, Min(0f)] private float damageTrailHoldDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float damageTrailCatchUpDuration = 0.3f;

    private AssaultBattleController battleController;
    private EnemyController observedBoss;
    private float displayedTrailHealth;
    private float lastObservedHealth;
    private float trailHoldRemaining;

    public void Configure(
        Image trail,
        Image current,
        Image stun,
        Text stunValue)
    {
        healthTrailFill = trail;
        currentHealthFill = current;
        stunFill = stun;
        stunText = stunValue;
        EnsureStunBackground();
        EnsureStunTextOutline();
    }

    private void Awake()
    {
        EnsureStunBackground();
        EnsureStunTextOutline();
    }
    private void OnEnable()
    {
        ResolveBoss();
        SnapToBoss();
    }

    private void LateUpdate()
    {
        ResolveBoss();
        if (observedBoss == null)
        {
            SetEmpty();
            return;
        }

        float currentHealth = Mathf.Clamp01(observedBoss.CurrentHpNormalized);
        UpdateHealthTrail(currentHealth);

        if (currentHealthFill != null)
            currentHealthFill.fillAmount = currentHealth;

        float stun = ResolveDisplayedStunNormalized();
        Color stunColor = ResolveStunColor();
        if (stunFill != null)
        {
            stunFill.fillAmount = stun;
            stunFill.color = stunColor;
        }

        if (stunText != null)
        {
            int percent = Mathf.Clamp(
                Mathf.RoundToInt(stun * 100f),
                0,
                MaxDisplayedStunPercent);
            stunText.text = percent.ToString("00");
            stunText.color = observedBoss.IsGroggy
                ? stunColor
                : normalStunTextColor;
        }
    }

    private void ResolveBoss()
    {
        if (battleController == null)
            battleController = FindFirstObjectByType<AssaultBattleController>();

        EnemyController nextBoss = battleController != null
            ? battleController.Boss
            : null;
        if (nextBoss == observedBoss)
            return;

        observedBoss = nextBoss;
        SnapToBoss();
    }

    private void SnapToBoss()
    {
        displayedTrailHealth = observedBoss != null
            ? Mathf.Clamp01(observedBoss.CurrentHpNormalized)
            : 0f;
        lastObservedHealth = displayedTrailHealth;
        trailHoldRemaining = 0f;

        if (healthTrailFill != null)
            healthTrailFill.fillAmount = displayedTrailHealth;
    }

    private void UpdateHealthTrail(float currentHealth)
    {
        if (currentHealth < lastObservedHealth)
            trailHoldRemaining = damageTrailHoldDuration;
        lastObservedHealth = currentHealth;

        if (currentHealth >= displayedTrailHealth)
        {
            displayedTrailHealth = currentHealth;
            trailHoldRemaining = 0f;
        }
        else if (trailHoldRemaining > 0f)
        {
            trailHoldRemaining -= Time.unscaledDeltaTime;
        }
        else
        {
            float speed = 1f / Mathf.Max(0.01f, damageTrailCatchUpDuration);
            displayedTrailHealth = Mathf.MoveTowards(
                displayedTrailHealth,
                currentHealth,
                Time.unscaledDeltaTime * speed);
        }

        if (healthTrailFill != null)
            healthTrailFill.fillAmount = displayedTrailHealth;
    }

    private void SetEmpty()
    {
        if (healthTrailFill != null)
            healthTrailFill.fillAmount = 0f;
        if (currentHealthFill != null)
            currentHealthFill.fillAmount = 0f;
        if (stunFill != null)
            stunFill.fillAmount = 0f;
        if (stunText != null)
        {
            stunText.text = "--";
            stunText.color = normalStunTextColor;
        }
    }

    private Color ResolveStunColor()
    {
        if (observedBoss == null || !observedBoss.IsGroggy)
            return normalStunColor;

        if (observedBoss.IsChainSkillSequenceComplete)
            return exhaustedStunColor;

        return Color.HSVToRGB(
            Mathf.Repeat(Time.unscaledTime * groggyHueCyclesPerSecond, 1f),
            0.8f,
            1f);
    }

    private float ResolveDisplayedStunNormalized()
    {
        if (observedBoss == null)
            return 0f;

        return Mathf.Clamp01(observedBoss.CurrentStunNormalized);
    }

    private void EnsureStunTextOutline()
    {
        if (stunText == null)
            return;

        Outline outline = stunText.GetComponent<Outline>();
        if (outline == null)
            outline = stunText.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private void EnsureStunBackground()
    {
        if (stunFill == null || stunFill.transform.parent == null)
            return;

        Transform parent = stunFill.transform.parent;
        Transform existing = parent.Find("Background");
        Image background = existing != null
            ? existing.GetComponent<Image>()
            : null;

        if (background == null)
        {
            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            background = backgroundObject.GetComponent<Image>();
        }

        RectTransform backgroundRect = background.rectTransform;
        RectTransform fillRect = stunFill.rectTransform;
        backgroundRect.anchorMin = fillRect.anchorMin;
        backgroundRect.anchorMax = fillRect.anchorMax;
        backgroundRect.pivot = fillRect.pivot;
        backgroundRect.anchoredPosition = fillRect.anchoredPosition;
        backgroundRect.sizeDelta = fillRect.sizeDelta;
        backgroundRect.localScale = Vector3.one;

        background.sprite = stunFill.sprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = stunFill.preserveAspect;
        background.color = stunBackgroundColor;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();
        stunFill.transform.SetAsLastSibling();
    }
}
