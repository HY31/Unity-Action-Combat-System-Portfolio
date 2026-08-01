using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private enum EnemyAttackPhase
    {
        None,
        Attack
    }

    [SerializeField] private SupportPointManager supportPointManager;
    [SerializeField] private EnemyData enemyData;
    public EnemyData EnemyData => enemyData;
    
    [Header("Stats")]
    [SerializeField] private float currentHp = 100f;
    [SerializeField] private float currentStun = 0f;
    [SerializeField] private bool isGroggy;
    [SerializeField] private float groggyTimeRemaining;

    [Header("Hit Reaction")]
    [SerializeField] private float currentHitReactionGauge = 0f;

    [Header("Attributes")]
    [SerializeField] private CombatElement currentAnomalyElement = CombatElement.None;

    [Header("Anomaly")]
    [SerializeField] private float currentAnomalyGauge = 0f;
    [SerializeField] private EnemyAnomalyState[] anomalyStates;

    public float CurrentAnomalyGauge => currentAnomalyGauge;
    public CombatElement CurrentAnomalyElement => currentAnomalyElement;
    public float CurrentHp => currentHp;
    public float CurrentStun => currentStun;
    public float CurrentHitReactionGauge => currentHitReactionGauge;
    public bool IsGroggy => isGroggy;
    public float GroggyTimeRemaining => groggyTimeRemaining;
    public float CurrentDamageTakenMultiplier =>
        isGroggy && enemyData != null
        ? Mathf.Max(1f, enemyData.groggyDamageMultiplier)
        : 1f;
    public static event System.Action<EnemyController, PlayerController> ChainSkillRequested;
    public float CurrentHpNormalized =>
        enemyData == null || enemyData.maxHp <= 0f
        ? 0f
        : currentHp / enemyData.maxHp;

    public float CurrentAnomalyNormalized =>
        enemyData == null || enemyData.anomalyThreshold <= 0f
        ? 0f
        : currentAnomalyGauge / enemyData.anomalyThreshold;

    public float CurrentStunNormalized =>
        enemyData == null || enemyData.maxStun <= 0f
        ? 0f
        : CurrentStun / enemyData.maxStun;

    public float MaxHp => enemyData != null ? enemyData.maxHp : 0f;
    public float AnomalyThreshold => enemyData != null ? enemyData.anomalyThreshold : 0f;

    public CombatElement DisplayAnomalyElement => currentAnomalyElement;

    public Animator animator;
    private EnemyAttackData currentAttack;
    public HitBox attackHitBox;

    [SerializeField] GameObject warningSign_Yellow;
    [SerializeField] GameObject warningSign_Red;

    public WarningType CurrentWarningType => currentAttack != null ? currentAttack.warningType : WarningType.None;

    public bool IsAttacking => phase == EnemyAttackPhase.Attack;

    public bool IsInWarningWindow { get; private set; }
    public bool IsInActiveWindow { get; private set; }
    public bool IsInReactionWindow { get; private set; }

    EnemyAttackPhase phase;

    [SerializeField] private KeyCode triggerKey = KeyCode.R;

    private void Awake()
    {
        if (enemyData == null)
        {
            Debug.LogError("적 데이터가 올바르지 않습니다.");
            enabled = false;
            return;
        }

        currentHp = enemyData.maxHp;
        currentStun = 0f;
        currentHitReactionGauge = 0f;
        isGroggy = false;
        groggyTimeRemaining = 0f;
        currentAnomalyGauge = 0f;

        // UI에는 최근 속성 하나만 보여도 실제 축적량은 속성별로 독립 보존한다.
        anomalyStates = new EnemyAnomalyState[]
    {
        new EnemyAnomalyState { element = CombatElement.Fire, gauge = 0f },
        new EnemyAnomalyState { element = CombatElement.Ice, gauge = 0f },
        new EnemyAnomalyState { element = CombatElement.Physical, gauge = 0f },
        new EnemyAnomalyState { element = CombatElement.Electric, gauge = 0f },
        new EnemyAnomalyState { element = CombatElement.Wind, gauge = 0f },
        new EnemyAnomalyState { element = CombatElement.Ether, gauge = 0f }
    };
    }
    private void Update()
    {
        UpdateHitReactionGauge();

        if (isGroggy)
        {
            UpdateGroggy();
            return;
        }

        if (Input.GetKeyDown(triggerKey))
        {
            TryStartAttack();
        }

        if (phase == EnemyAttackPhase.Attack)
        {
            UpdateAttack(animator.GetCurrentAnimatorStateInfo(0));
        }
    }

    private void UpdateHitReactionGauge()
    {
        if (currentHitReactionGauge <= 0f)
            return;

        // 강공격이 이어지지 않으면 숨은 경직 게이지가 빠르게 0으로 돌아간다.
        currentHitReactionGauge = Mathf.MoveTowards(
            currentHitReactionGauge,
            0f,
            enemyData.hitReactionDecayPerSecond * Time.deltaTime);
    }

    private void AddHitReactionBuildUp(float amount)
    {
        if (isGroggy || amount <= 0f)
            return;

        // 공격에 설정된 경직 누적치를 최대 게이지 범위 안에서 반영한다.
        currentHitReactionGauge = Mathf.Clamp(
            currentHitReactionGauge + amount,
            0f,
            enemyData.maxHitReactionGauge);

        Debug.Log(
            $"{name} 경직 게이지 = " +
            $"{currentHitReactionGauge:F1} / {enemyData.maxHitReactionGauge:F1}");
    }

    public bool TryStartAttack()
    {
        if (isGroggy || IsAttacking)
            return false;

        if (enemyData == null || animator == null || attackHitBox == null)
            return false;

        if (enemyData.attackPatterns == null || enemyData.attackPatterns.Length == 0)
            return false;

        EnemyAttackData selectedAttack =
            enemyData.attackPatterns[Random.Range(0, enemyData.attackPatterns.Length)];

        if (selectedAttack == null || string.IsNullOrEmpty(selectedAttack.attackAnim))
            return false;

        currentAttack = selectedAttack;
        phase = EnemyAttackPhase.Attack;

        attackHitBox.SetActive(false);
        animator.CrossFade(currentAttack.attackAnim, 0.05f);

        return true;
    }

    private void UpdateAttack(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        bool canUseParrySupport =
            supportPointManager != null &&
            supportPointManager.HasEnoughSupportPoint(1);

        // 노란색 공격도 지원 포인트가 부족하면 플레이어에게 빨간색 경고로 안내한다.
        bool showWarning = t >= currentAttack.warningStart && t < currentAttack.warningEnd;

        bool showYellow = showWarning &&
            currentAttack.warningType == WarningType.Yellow &&
            canUseParrySupport;

        bool showRed = showWarning && (
            currentAttack.warningType == WarningType.Red ||
            (currentAttack.warningType == WarningType.Yellow && !canUseParrySupport)
            );

        bool shouldHit = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        bool canReaction = t >= currentAttack.reactionStart && t < currentAttack.reactionEnd;

        IsInWarningWindow = showWarning;
        IsInActiveWindow = shouldHit;
        IsInReactionWindow = canReaction;

        attackHitBox.SetActive(shouldHit);
        warningSign_Yellow.SetActive(showYellow);
        warningSign_Red.SetActive(showRed);

        if (t >= 1f)
        {
            // 별도 종료 클립이 없는 패턴은 즉시 대기 상태로 돌려보낼 수 있게 공격 상태만 정리한다.
            if (string.IsNullOrEmpty(currentAttack.endAnim))
            {
                IsInWarningWindow = false;
                IsInActiveWindow = false;
                IsInReactionWindow = false;
                attackHitBox.SetActive(false);  
                currentAttack = null;

                phase = EnemyAttackPhase.None;
                return;
            }

            IsInWarningWindow = false;
            IsInActiveWindow = false;
            IsInReactionWindow = false;
            animator.CrossFade(currentAttack.endAnim, 0.05f);
            attackHitBox.SetActive(false);
            currentAttack = null;
            phase = EnemyAttackPhase.None;
        }
    }

    public void InterruptAttack()
    {
        if (attackHitBox != null)
            attackHitBox.SetActive(false);
        if (warningSign_Yellow != null)
            warningSign_Yellow.SetActive(false);
        if (warningSign_Red != null)
            warningSign_Red.SetActive(false);
        IsInWarningWindow = false;
        IsInActiveWindow = false;
        IsInReactionWindow = false;

        if (animator != null && currentAttack != null && !string.IsNullOrEmpty(currentAttack.endAnim))
            animator.CrossFade(currentAttack.endAnim, 0.05f);

        currentAttack = null;
        phase = EnemyAttackPhase.None;
    }

    public void ReceiveHit(CombatHitData hitData)
    {
        if (hitData.attacker == null) return;
        if (enemyData == null) return;

        float baseDamage = hitData.attacker.CurrentAttack * hitData.damageMultiplier;

        // 관통률로 줄어든 유효 방어력을 완만한 승수로 바꿔 방어력이 피해를 완전히 막지 않게 한다.
        float effectiveDefense = enemyData.defense * (1f - hitData.attacker.CurrentPenRatio);
        effectiveDefense = Mathf.Max(0f, effectiveDefense);

        float defenseMultiplier = 100f / (100f + effectiveDefense);

        // 그로기 중 받는 피해 배율은 UI가 아닌 적 전투 데이터에서 결정한다.
        float finalDamage = baseDamage * defenseMultiplier * CurrentDamageTakenMultiplier;

        currentHp = Mathf.Clamp(currentHp - finalDamage, 0f, enemyData.maxHp);

        float stunDamage = 0f;
        if (!isGroggy)
        {
            stunDamage = hitData.attacker.CurrentImpact *
                hitData.impactMultiplier *
                (1f - enemyData.stunResistance);
            currentStun = Mathf.Clamp(currentStun + stunDamage, 0f, enemyData.maxStun);
        }

        // 일반 공격은 0을 전달하므로 강공격에 설정된 누적치만 게이지에 반영된다.
        AddHitReactionBuildUp(hitData.hitReactionBuildUp);

        Debug.Log($"{name} 피해 {finalDamage:F1} / 현재 체력 {currentHp:F1}");
        Debug.Log($"{name} 그로기 누적 {stunDamage:F1} / 현재 그로기 수치 {currentStun:F1}");
        Debug.Log($"{name} 속성 = {hitData.resolvedElement}, 이상 누적치 = {hitData.anomalyBuildUp}");

        if (!isGroggy && enemyData.maxStun > 0f && currentStun >= enemyData.maxStun)
            EnterGroggy(hitData.attacker, hitData.canTriggerChainSkill);

        if (hitData.resolvedElement != CombatElement.None)
        {
            EnemyElementModifier modifier = enemyData.GetElementModifier(hitData.resolvedElement);
            float appliedBuildUp = hitData.anomalyBuildUp * modifier.anomalyMultiplier;

            // 배열 원소가 struct이므로 복사본을 수정한 뒤 같은 인덱스에 다시 기록해야 한다.
            for (int i = 0; i < anomalyStates.Length; i++)
            {
                if (anomalyStates[i].element != hitData.resolvedElement)
                    continue;

                EnemyAnomalyState state = anomalyStates[i];
                state.gauge = Mathf.Clamp(state.gauge + appliedBuildUp, 0f, enemyData.anomalyThreshold);
                currentAnomalyElement = state.element;
                currentAnomalyGauge = state.gauge;
                anomalyStates[i] = state;

                Debug.Log($"{name} {state.element} 이상 게이지 = {state.gauge:F1} / {enemyData.anomalyThreshold:F1}");

                TryTriggerAnomaly(state.element, i);

                break;
            }
        }
    }

    public float GetAnomalyGauge(CombatElement element)
    {
        for (int i = 0; i < anomalyStates.Length; i++)
        {
            if (anomalyStates[i].element == element)
                return anomalyStates[i].gauge;
        }

        return 0f;
    }
    private void TriggerAnomaly(CombatElement element)
    {
        Debug.Log($"{name} 이상 효과 발동: {element}");
    }

    private void TryTriggerAnomaly(CombatElement element, int stateIndex)
    {
        if (anomalyStates[stateIndex].gauge < enemyData.anomalyThreshold)
            return;

        TriggerAnomaly(element);

        // 발동한 속성 게이지만 비우고 다른 속성의 누적 상태는 유지한다.
        EnemyAnomalyState state = anomalyStates[stateIndex];
        state.gauge = 0f;
        anomalyStates[stateIndex] = state;

        if (currentAnomalyElement == element)
        {
            currentAnomalyGauge = 0f;
        }
    }

    private void EnterGroggy(PlayerController attacker, bool requestChainSkill)
    {
        isGroggy = true;
        currentStun = enemyData.maxStun;
        groggyTimeRemaining = Mathf.Max(0.01f, enemyData.groggyDuration);
        InterruptAttack();

        if (requestChainSkill)
            ChainSkillRequested?.Invoke(this, attacker);
    }

    private void UpdateGroggy()
    {
        float duration = Mathf.Max(0.01f, enemyData.groggyDuration);
        groggyTimeRemaining = Mathf.Max(0f, groggyTimeRemaining - Time.deltaTime);
        currentStun = enemyData.maxStun * (groggyTimeRemaining / duration);

        if (groggyTimeRemaining > 0f)
            return;

        currentStun = 0f;
        isGroggy = false;
    }
}
