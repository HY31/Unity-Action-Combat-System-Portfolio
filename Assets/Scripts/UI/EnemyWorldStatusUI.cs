using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적의 월드 위치를 따라가며 HP, 그로기 수치와 그로기 상태를 표시한다.
/// 테스트 씬에서는 적을 자동 탐색할 수 있고, 실제 전투에서는 Bind로 명시적인 대상을 받을 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyWorldStatusUI : MonoBehaviour
{
    private const float GaugeEdgeHorizontalShear = 0.4663f;
    [Header("Target")]
    [SerializeField] private EnemyController targetEnemy;
    [SerializeField] private Transform worldAnchor;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.7f, 0f);
    [SerializeField] private bool autoBindTarget = true;
    [SerializeField] private string autoBindObjectName = "Enemy";
    [SerializeField, Min(0.1f)] private float targetSearchInterval = 0.5f;

    [Header("Views")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image hpDamageTrail;
    [SerializeField] private Image stunFill;
    [SerializeField] private Text stunPercentText;
    [SerializeField] private Text damageMultiplierText;
    [SerializeField] private GameObject anomalyIconRoot;
    [SerializeField] private Image anomalyFill;
    [SerializeField] private Image anomalyBackground;
    [SerializeField] private Image anomalyIcon;
    [SerializeField] private ElementIconEntry[] anomalyIcons;

    [Header("Colors")]
    [SerializeField] private Color normalStunColor = new Color32(255, 205, 24, 255);
    [SerializeField] private Color exhaustedGroggyColor = new Color32(112, 118, 126, 255);
    [SerializeField, Min(0.1f)] private float groggyHueCyclesPerSecond = 1.6f;

    [Header("Health Trail")]
    [SerializeField, Min(0f)] private float healthTrailDelay = 0.12f;
    [SerializeField, Min(0.01f)] private float healthTrailCatchupDuration = 0.24f;

    public EnemyController TargetEnemy => targetEnemy;

    private float nextTargetSearchTime;
    private float displayedHpTrail = -1f;
    private float lastHpNormalized = -1f;
    private float healthTrailDelayRemaining;

    private void Awake()
    {
        ResolveAnomalyParts();
        ApplyFinalLayout();
        ConfigureSlantedGaugeEdges();
        ConfigureStunPercentOutline();

        if (damageMultiplierText != null)
            damageMultiplierText.gameObject.SetActive(false);

        ResolveTarget();
    }

    /// <summary>
    /// 회색 소진 단계에서도 그로기 수치가 게이지 배경에 묻히지 않도록 검은 외곽선을 보장한다.
    /// </summary>
    private void ConfigureStunPercentOutline()
    {
        if (stunPercentText == null)
            return;

        BossHudTextShear shear =
            stunPercentText.GetComponent<BossHudTextShear>();
        if (shear == null)
            shear = stunPercentText.gameObject.AddComponent<BossHudTextShear>();
        shear.HorizontalShear = Mathf.Tan(20f * Mathf.Deg2Rad);

        Outline outline = stunPercentText.GetComponent<Outline>();
        if (outline == null)
            outline = stunPercentText.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
    }

    private void ConfigureSlantedGaugeEdges()
    {
        ConfigureSlantedGaugeEdge(hpFill);
        ConfigureSlantedGaugeEdge(hpDamageTrail);
        ConfigureSlantedGaugeEdge(stunFill);
    }

    private static void ConfigureSlantedGaugeEdge(Image image)
    {
        if (image == null)
            return;

        SlantedFillEdgeEffect effect =
            image.GetComponent<SlantedFillEdgeEffect>();
        if (effect == null)
            effect = image.gameObject.AddComponent<SlantedFillEdgeEffect>();

        effect.HorizontalShear = GaugeEdgeHorizontalShear;
    }

    private void OnEnable()
    {
        ResolveTarget();
    }

    private void LateUpdate()
    {
        // 적이 교체되거나 앵커가 끊긴 경우에만 일정 주기로 다시 탐색해 매 프레임 검색을 피한다.
        if (autoBindTarget && (targetEnemy == null || !IsAnchorOwnedByTarget()))
        {
            if (Time.unscaledTime >= nextTargetSearchTime)
                ResolveTarget();
        }

        if (targetEnemy == null || hpFill == null || stunFill == null || worldAnchor == null)
        {
            if (visualRoot != null)
                visualRoot.SetActive(false);

            return;
        }

        // 월드 UI는 별도 Canvas에 있으므로 보스 본체의 활성 상태를 직접 따라가야 한다.
        if (!targetEnemy.gameObject.activeInHierarchy)
        {
            if (visualRoot != null)
                visualRoot.SetActive(false);

            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 screenPosition = camera.WorldToScreenPoint(worldAnchor.position + worldOffset);
        transform.position = screenPosition;

        bool visible = screenPosition.z > 0f && targetEnemy.CurrentHp > 0f;
        if (visualRoot != null)
            visualRoot.SetActive(visible);

        if (!visible)
            return;

        // 전투 계산은 EnemyController가 담당하고 UI는 정규화된 결과만 표시한다.
        UpdateHealthVisual(targetEnemy.CurrentHpNormalized);
        float displayedStunNormalized = ResolveDisplayedStunNormalized();
        stunFill.fillAmount = displayedStunNormalized;
        Color stunColor = ResolveStunColor();
        stunFill.color = stunColor;

        if (stunPercentText != null)
        {
            int displayedStun = Mathf.Clamp(
                Mathf.RoundToInt(displayedStunNormalized * 100f),
                0,
                EnemyController.MaxDisplayedStunPercent);
            stunPercentText.text = displayedStun.ToString("00");
            // 콤보 스킬을 모두 사용한 소진 단계에는 게이지와 함께 숫자도 회색으로 표시한다.
            stunPercentText.color = targetEnemy.IsChainSkillSequenceComplete
                ? exhaustedGroggyColor
                : stunColor;
        }

        if (damageMultiplierText != null)
        {
            damageMultiplierText.gameObject.SetActive(targetEnemy.IsGroggy);
            int multiplier = Mathf.RoundToInt(
                targetEnemy.CurrentDamageTakenMultiplier * 100f);
            damageMultiplierText.text =
                $"<color=#E38B15>DMG</color> <color=#F0C62E>{multiplier}%</color>";
        }

        UpdateAnomalyVisual();
    }


    private void UpdateHealthVisual(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        hpFill.fillAmount = normalized;

        if (hpDamageTrail == null)
            return;

        if (displayedHpTrail < 0f || lastHpNormalized < 0f)
        {
            displayedHpTrail = normalized;
            lastHpNormalized = normalized;
            hpDamageTrail.fillAmount = normalized;
            return;
        }

        if (normalized > displayedHpTrail)
            displayedHpTrail = normalized;

        if (normalized < lastHpNormalized - 0.0001f)
            healthTrailDelayRemaining = healthTrailDelay;

        if (healthTrailDelayRemaining > 0f)
        {
            healthTrailDelayRemaining = Mathf.Max(
                0f,
                healthTrailDelayRemaining - Time.unscaledDeltaTime);
        }
        else
        {
            float speed = 1f / Mathf.Max(0.01f, healthTrailCatchupDuration);
            displayedHpTrail = Mathf.MoveTowards(
                displayedHpTrail,
                normalized,
                speed * Time.unscaledDeltaTime);
        }

        displayedHpTrail = Mathf.Max(displayedHpTrail, normalized);
        hpDamageTrail.fillAmount = displayedHpTrail;
        lastHpNormalized = normalized;
    }

    public void ConfigureHealthTrail(Image healthTrail)
    {
        hpDamageTrail = healthTrail;
        displayedHpTrail = -1f;
        lastHpNormalized = -1f;
        healthTrailDelayRemaining = 0f;
    }

    public void Bind(EnemyController enemy, Transform anchor = null)
    {
        targetEnemy = enemy;
        worldAnchor = anchor != null ? anchor : enemy != null ? enemy.transform : null;
    }

    public void ConfigureAutoTarget(string objectName, Vector3 offset)
    {
        autoBindTarget = true;
        autoBindObjectName = objectName;
        worldOffset = offset;
    }

    public void Configure(
        Image health,
        Image stun,
        Text stunPercent,
        Text damageMultiplier,
        GameObject visuals,
        GameObject anomalyRoot)
    {
        hpFill = health;
        stunFill = stun;
        stunPercentText = stunPercent;
        damageMultiplierText = damageMultiplier;
        visualRoot = visuals;
        anomalyIconRoot = anomalyRoot;
    }
    public void Configure(
        Image health,
        Image stun,
        Text stunPercent,
        Text damageMultiplier,
        GameObject visuals,
        GameObject anomalyRoot,
        Image anomalyGauge,
        Image anomalyElementIcon)
    {
        Configure(health, stun, stunPercent, damageMultiplier, visuals, anomalyRoot);
        anomalyFill = anomalyGauge;
        anomalyIcon = anomalyElementIcon;
    }
    public void ConfigureAnomalyIcons(ElementIconEntry[] icons)
    {
        anomalyIcons = icons;
    }


    private void ApplyFinalLayout()
    {
        if (stunPercentText != null)
        {
            SetCenteredRect(
                stunPercentText.rectTransform,
                new Vector2(27f, 22f),
                new Vector2(167.5f, 17f));
        }

        if (anomalyIconRoot == null)
            return;

        string[] stretchedLayers =
        {
            "AnomalyBack",
            "AnomalyFrame"
        };

        for (int i = 0; i < stretchedLayers.Length; i++)
        {
            Transform layer = anomalyIconRoot.transform.Find(
                stretchedLayers[i]);
            if (layer is RectTransform layerRect)
                StretchRect(layerRect);
        }

        if (anomalyFill != null)
        {
            SetCenteredRect(
                anomalyFill.rectTransform,
                new Vector2(57.17931f, 57.17931f),
                new Vector2(7.52356f, -0.75236f));
        }
    }

    private static void SetCenteredRect(
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

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    private void ResolveAnomalyParts()
    {
        if (anomalyIconRoot == null)
            return;

        if (anomalyBackground == null)
        {
            Transform background = anomalyIconRoot.transform.Find("AnomalyBack");
            if (background != null)
                anomalyBackground = background.GetComponent<Image>();
        }
    }
    private void UpdateAnomalyVisual()
    {
        if (anomalyIconRoot == null || targetEnemy == null)
            return;

        float normalized = Mathf.Clamp01(targetEnemy.CurrentAnomalyNormalized);
        CombatElement element = targetEnemy.DisplayAnomalyElement;
        bool visible = normalized > 0f && element != CombatElement.None;
        anomalyIconRoot.SetActive(visible);

        if (!visible)
            return;

        ResolveAnomalyParts();
        Color elementColor = ResolveAnomalyColor(element);
        if (anomalyBackground != null)
        {
            Color mutedColor = Color.Lerp(
                new Color32(12, 16, 18, 255),
                elementColor,
                0.22f);
            mutedColor.a = 1f;
            anomalyBackground.color = mutedColor;
        }

        if (anomalyFill != null)
        {
            anomalyFill.type = Image.Type.Filled;
            anomalyFill.fillMethod = Image.FillMethod.Radial360;
            anomalyFill.fillOrigin = (int)Image.Origin360.Top;
            anomalyFill.fillClockwise = true;
            anomalyFill.fillAmount = normalized;
            anomalyFill.color = elementColor;
        }

        if (anomalyIcon == null)
            return;

        bool iconResolved = false;
        if (anomalyIcons != null)
        {
            for (int i = 0; i < anomalyIcons.Length; i++)
            {
                if (anomalyIcons[i].element != element || anomalyIcons[i].sprite == null)
                    continue;

                anomalyIcon.sprite = anomalyIcons[i].sprite;
                iconResolved = true;
                break;
            }
        }

        anomalyIcon.enabled = iconResolved;
        anomalyIcon.color = Color.white;

        RectTransform iconRect = anomalyIcon.rectTransform;
        iconRect.anchoredPosition = new Vector2(7.52356f, -0.75236f);
        float iconSize = element == CombatElement.Physical ? 35.02662f : 33.59255f;
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
    }

    private static Color ResolveAnomalyColor(CombatElement element)
    {
        switch (element)
        {
            case CombatElement.Fire:
                return new Color32(255, 86, 42, 255);
            case CombatElement.Ice:
                return new Color32(88, 220, 255, 255);
            case CombatElement.Physical:
                return new Color32(255, 201, 47, 255);
            case CombatElement.Electric:
                return new Color32(165, 92, 255, 255);
            case CombatElement.Wind:
                return new Color32(85, 236, 158, 255);
            case CombatElement.Ether:
                return new Color32(255, 91, 206, 255);
            default:
                return Color.white;
        }
    }

    private Color ResolveStunColor()
    {
        if (!targetEnemy.IsGroggy)
            return normalStunColor;

        return targetEnemy.IsChainSkillSequenceComplete
            ? exhaustedGroggyColor
            : GetGroggyFlashColor();
    }

    private float ResolveDisplayedStunNormalized()
    {
        if (targetEnemy == null)
            return 0f;

        return Mathf.Clamp01(targetEnemy.CurrentStunNormalized);
    }

    private Color GetGroggyFlashColor()
    {
        return Color.HSVToRGB(
            Mathf.Repeat(Time.unscaledTime * groggyHueCyclesPerSecond, 1f),
            0.8f,
            1f);
    }

    private void ResolveTarget()
    {
        nextTargetSearchTime = Time.unscaledTime + targetSearchInterval;

        // 지정 이름을 우선하고 찾지 못하면 씬의 첫 EnemyController를 테스트용 폴백으로 사용한다.
        if (targetEnemy == null && !string.IsNullOrEmpty(autoBindObjectName))
        {
            GameObject namedTarget = GameObject.Find(autoBindObjectName);
            if (namedTarget != null)
            {
                targetEnemy = namedTarget.GetComponent<EnemyController>();
                if (targetEnemy == null)
                    targetEnemy = namedTarget.GetComponentInChildren<EnemyController>();
            }
        }

        if (targetEnemy == null)
            targetEnemy = FindFirstObjectByType<EnemyController>();

        if (targetEnemy != null && !IsAnchorOwnedByTarget())
            worldAnchor = targetEnemy.transform;
    }

    private bool IsAnchorOwnedByTarget()
    {
        if (targetEnemy == null || worldAnchor == null)
            return false;

        Transform targetTransform = targetEnemy.transform;
        return worldAnchor == targetTransform || worldAnchor.IsChildOf(targetTransform);
    }
}
