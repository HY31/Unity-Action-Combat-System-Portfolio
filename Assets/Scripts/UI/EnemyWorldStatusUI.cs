using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 적의 월드 위치를 따라가며 HP, 그로기 수치와 그로기 상태를 표시한다.
/// 테스트 씬에서는 적을 자동 탐색할 수 있고, 실제 전투에서는 Bind로 명시적인 대상을 받을 수 있다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyWorldStatusUI : MonoBehaviour
{
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
    [SerializeField] private Image stunFill;
    [SerializeField] private Text stunPercentText;
    [SerializeField] private Text damageMultiplierText;
    [SerializeField] private GameObject anomalyIconRoot;

    [Header("Colors")]
    [SerializeField] private Color normalStunColor = new Color32(255, 205, 24, 255);
    [SerializeField] private Color[] groggyFlashColors =
    {
        new Color32(146, 88, 255, 255),
        new Color32(50, 191, 255, 255),
        new Color32(255, 220, 35, 255),
        new Color32(255, 55, 42, 255)
    };
    [SerializeField, Min(1f)] private float groggyFlashRate = 12f;

    public EnemyController TargetEnemy => targetEnemy;

    private float nextTargetSearchTime;

    private void Awake()
    {
        if (damageMultiplierText != null)
            damageMultiplierText.gameObject.SetActive(false);

        ResolveTarget();
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
        hpFill.fillAmount = targetEnemy.CurrentHpNormalized;
        stunFill.fillAmount = targetEnemy.CurrentStunNormalized;
        Color stunColor = targetEnemy.IsGroggy ? GetGroggyFlashColor() : normalStunColor;
        stunFill.color = stunColor;

        if (stunPercentText != null)
        {
            stunPercentText.text = Mathf.RoundToInt(targetEnemy.CurrentStunNormalized * 100f).ToString("00");
            stunPercentText.color = stunColor;
        }

        if (damageMultiplierText != null)
        {
            damageMultiplierText.gameObject.SetActive(targetEnemy.IsGroggy);
            damageMultiplierText.text = $"DMG {Mathf.RoundToInt(targetEnemy.CurrentDamageTakenMultiplier * 100f)}%";
        }

        // 현재는 아이콘 컨테이너의 표시만 담당하며 속성별 Sprite 연결은 다음 단계에서 확장한다.
        if (anomalyIconRoot != null)
            anomalyIconRoot.SetActive(true);
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

    private Color GetGroggyFlashColor()
    {
        if (groggyFlashColors == null || groggyFlashColors.Length == 0)
            return normalStunColor;

        int index = Mathf.FloorToInt(Time.unscaledTime * groggyFlashRate) % groggyFlashColors.Length;
        return groggyFlashColors[index];
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
