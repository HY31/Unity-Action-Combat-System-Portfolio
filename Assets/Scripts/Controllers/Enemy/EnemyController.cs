using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class EnemyController : MonoBehaviour
{
    private enum EnemyAttackPhase
    {
        None,
        Attack,
        FollowUp
    }

    [SerializeField] private SupportPointManager supportPointManager;
    [SerializeField] private EnemyData enemyData;
    public EnemyData EnemyData => enemyData;

    [Header("Stats")]
    [SerializeField] private float currentHp = 100f;
    [SerializeField] private float currentStun = 0f;
    [SerializeField] private bool isDefeated;
    [SerializeField] private bool isGroggy;
    [SerializeField] private float groggyTimeRemaining;

    [Header("Hit Reaction")]
    [SerializeField] private float currentHitReactionGauge = 0f;
    [SerializeField] private bool isInHitReaction;
    private float hitReactionTimeRemaining;
    private bool groggyLoopStarted;

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
    public bool IsInHitReaction => isInHitReaction;
    public bool IsGroggy => isGroggy;
    public bool IsDefeated => isDefeated;
    public float CurrentAttackRecoveryDelay =>
        currentAttack != null
        ? Mathf.Max(0f, currentAttack.additionalRecoveryDelay)
        : 0f;
    public float GroggyTimeRemaining => groggyTimeRemaining;
    public float CurrentDamageTakenMultiplier =>
        isGroggy && enemyData != null
        ? Mathf.Max(1f, enemyData.groggyDamageMultiplier)
        : 1f;
    public static event System.Action<EnemyController, PlayerController> ChainSkillRequested;
    public event System.Action<EnemyController, float> DamageTaken;
    public event System.Action<EnemyController> Defeated;
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
    private float baseAnimatorSpeed = 1f;
    private Rigidbody enemyRigidbody;

    private Vector3 attackMoveDirection;
    private float previousAttackMovementTime;
    private float pendingAttackMoveDistance;

    private EnemyAttackData currentAttack;
    private EnemyAttackData lastAttack;
    private readonly Dictionary<EnemyAttackData, float> patternReadyTimes =
        new Dictionary<EnemyAttackData, float>();
    private bool attackSwingPlayed;
    public HitBox attackHitBox;
    private BoxCollider attackBoxCollider;
    private Vector3 defaultAttackHitBoxCenter;
    private Vector3 defaultAttackHitBoxSize;

    private Transform attackTarget;
    private bool attackTrackingActive;
    private Vector3 attackTrackingDirection;
    private float attackTrackingRotationSpeed;

    [SerializeField] GameObject warningSign_Yellow;
    [SerializeField] GameObject warningSign_Red;

    private Vector3 warningYellowBaseScale = Vector3.one;
    private Vector3 warningRedBaseScale = Vector3.one;
    private bool warningYellowVisible;
    private bool warningRedVisible;
    private Tween warningYellowTween;
    private Tween warningRedTween;

    public WarningType CurrentWarningType => currentAttack != null ? currentAttack.warningType : WarningType.None;

    public bool IsAttacking => phase != EnemyAttackPhase.None;

    public bool IsInWarningWindow { get; private set; }
    public bool IsInActiveWindow { get; private set; }
    public bool IsInReactionWindow { get; private set; }
    public WarningType VisibleWarningType
    {
        get
        {
            if (warningSign_Yellow != null && warningSign_Yellow.activeInHierarchy)
                return WarningType.Yellow;

            if (warningSign_Red != null && warningSign_Red.activeInHierarchy)
                return WarningType.Red;

            return WarningType.None;
        }
    }

    public bool IsParryWarningVisible => VisibleWarningType == WarningType.Yellow;
    public bool IsAnyWarningVisible => VisibleWarningType != WarningType.None;

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

        if (animator != null)
            baseAnimatorSpeed = animator.speed;

        enemyRigidbody = GetComponent<Rigidbody>();

        // 높은 물리 콜라이더 대신 수평 차단기를 사용해 캐릭터가 위로 튀지 않게 한다.
        EnemyBodyBlocker bodyBlocker = GetComponent<EnemyBodyBlocker>();
        if (bodyBlocker == null)
            bodyBlocker = gameObject.AddComponent<EnemyBodyBlocker>();

        if (attackHitBox != null)
        {
            attackBoxCollider = attackHitBox.GetComponent<BoxCollider>();

            if (attackBoxCollider != null)
            {
                defaultAttackHitBoxCenter = attackBoxCollider.center;
                defaultAttackHitBoxSize = attackBoxCollider.size;
            }
        }

        InitializeWarningSigns();

        currentHp = enemyData.maxHp;
        isDefeated = false;
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
    private void OnDestroy()
    {
        warningYellowTween?.Kill(false);
        warningRedTween?.Kill(false);
    }

    private void Update()
    {
        UpdateHitReactionGauge();

        if (isInHitReaction)
        {
            UpdateHitReaction();
            return;
        }

        if (isGroggy)
        {
            UpdateGroggy();
            return;
        }

        if (Input.GetKeyDown(triggerKey))
        {
            TryStartAttack();
        }

        if (phase != EnemyAttackPhase.None)
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

        if (currentHitReactionGauge >= enemyData.hitReactionThreshold)
            BeginHitReaction();
    }

    public void SetAttackTarget(Transform target)
    {
        attackTarget = target;
    }
    public bool TryStartAttack()
    {
        if (isGroggy || isInHitReaction || IsAttacking)
            return false;

        if (enemyData == null || animator == null || attackHitBox == null)
            return false;

        if (enemyData.attackPatterns == null || enemyData.attackPatterns.Length == 0)
            return false;

        EnemyAttackData selectedAttack = SelectAttackPattern();

        if (selectedAttack == null)
            return false;

        currentAttack = selectedAttack;
        lastAttack = selectedAttack;
        BeginPatternSelectionCooldown(selectedAttack);
        attackSwingPlayed = false;
        ConfigureAttackHitBox(false);
        attackHitBox.SetFeedback(currentAttack.hitFeedback);
        attackHitBox.SetHitData(new CombatHitData
        {
            rawDamage = Mathf.Max(0f, currentAttack.damage),
            damageMultiplier = 1f,
            impactMultiplier = 1f,
            resolvedElement = CombatElement.None
        });

        phase = EnemyAttackPhase.Attack;

        BeginAttackMovement();

        attackHitBox.SetActive(false);

        float playbackSpeed = Mathf.Max(
            0.01f,
            currentAttack.playbackSpeed);

        animator.speed = baseAnimatorSpeed * playbackSpeed;
        animator.CrossFade(currentAttack.attackAnim, 0.05f);

        return true;
    }

    private EnemyAttackData SelectAttackPattern()
    {
        bool hasTarget = attackTarget != null;
        float targetDistance = 0f;

        if (hasTarget)
        {
            Vector3 targetOffset = attackTarget.position - transform.position;
            targetOffset.y = 0f;
            targetDistance = targetOffset.magnitude;
        }

        EnemyAttackData selectedAttack = null;
        EnemyAttackData repeatFallback = null;
        float totalSelectionWeight = 0f;

        foreach (EnemyAttackData attackPattern in enemyData.attackPatterns)
        {
            if (attackPattern == null || string.IsNullOrEmpty(attackPattern.attackAnim))
                continue;

            if (hasTarget && !attackPattern.CanUseAtDistance(targetDistance))
                continue;

            float selectionWeight = Mathf.Max(0f, attackPattern.selectionWeight);

            if (selectionWeight <= 0f)
                continue;

            if (!IsAttackPatternReady(attackPattern))
                continue;

            if (attackPattern == lastAttack)
            {
                repeatFallback = attackPattern;
                continue;
            }

            // 후보 배열 없이 누적 가중치만으로 각 패턴의 설정 비율에 맞춰 하나를 남긴다.
            totalSelectionWeight += selectionWeight;

            if (Random.value <= selectionWeight / totalSelectionWeight)
                selectedAttack = attackPattern;
        }

        // 거리 조건을 만족하는 다른 패턴이 하나도 없을 때만 직전 공격을 다시 허용한다.
        return selectedAttack != null ? selectedAttack : repeatFallback;
    }

    private bool IsAttackPatternReady(EnemyAttackData attackPattern)
    {
        if (!patternReadyTimes.TryGetValue(attackPattern, out float readyTime))
            return true;

        if (Time.time < readyTime)
            return false;

        patternReadyTimes.Remove(attackPattern);
        return true;
    }

    private void BeginPatternSelectionCooldown(EnemyAttackData attackPattern)
    {
        float cooldown = Mathf.Max(0f, attackPattern.selectionCooldown);

        if (cooldown <= 0f)
        {
            patternReadyTimes.Remove(attackPattern);
            return;
        }

        patternReadyTimes[attackPattern] = Time.time + cooldown;
    }

    private void UpdateAttack(AnimatorStateInfo info)
    {
        if (currentAttack == null)
        {
            phase = EnemyAttackPhase.None;
            return;
        }

        bool isFollowUp = phase == EnemyAttackPhase.FollowUp;
        string expectedAnimation = isFollowUp
            ? currentAttack.followUpAnim
            : currentAttack.attackAnim;

        if (!info.IsName(expectedAnimation))
            return;

        float t = info.normalizedTime;

        UpdateAttackSwing(t, isFollowUp);
        UpdateAttackTracking(t, isFollowUp);

        if (!isFollowUp)
            QueueAttackMovement(t);

        bool canUseParrySupport =
            supportPointManager != null &&
            supportPointManager.HasEnoughSupportPoint(1);

        bool showWarning;
        bool shouldHit;
        bool canReaction;

        if (isFollowUp)
        {
            showWarning = IsInAnyWindow(t, currentAttack.followUpWarningWindows);
            shouldHit = IsInAnyWindow(t, currentAttack.followUpActiveWindows);
            canReaction = IsInAnyWindow(t, currentAttack.followUpReactionWindows);
        }
        else if (currentAttack.useTimingWindows)
        {
            showWarning = IsInAnyWindow(t, currentAttack.warningWindows);
            shouldHit = IsInAnyWindow(t, currentAttack.activeWindows);
            canReaction = IsInAnyWindow(t, currentAttack.reactionWindows);
        }
        else
        {
            showWarning = IsInLegacyWindow(
                t,
                currentAttack.warningStart,
                currentAttack.warningEnd);

            shouldHit = IsInLegacyWindow(
                t,
                currentAttack.startUpEnd,
                currentAttack.activeEnd);

            canReaction = IsInLegacyWindow(
                t,
                currentAttack.reactionStart,
                currentAttack.reactionEnd);
        }

        bool showYellow = showWarning &&
            currentAttack.warningType == WarningType.Yellow &&
            canUseParrySupport;

        bool showRed = showWarning && (
            currentAttack.warningType == WarningType.Red ||
            (currentAttack.warningType == WarningType.Yellow && !canUseParrySupport)
            );

        IsInWarningWindow = showWarning;
        IsInActiveWindow = shouldHit;
        IsInReactionWindow = canReaction;

        attackHitBox.SetActive(shouldHit);
        UpdateWarningSigns(showYellow, showRed);

        bool hasFollowUp = !string.IsNullOrEmpty(currentAttack.followUpAnim);
        float stageEnd = !isFollowUp && hasFollowUp
            ? Mathf.Clamp01(currentAttack.followUpStartNormalized)
            : 1f;

        if (t < stageEnd)
            return;

        if (!isFollowUp && hasFollowUp)
        {
            BeginFollowUp();
            return;
        }

        FinishAttack();
    }

    private void UpdateAttackSwing(float normalizedTime, bool isFollowUp)
    {
        if (attackSwingPlayed || currentAttack == null)
            return;

        float activeStart = ResolveAttackActiveStart(isFollowUp);
        if (normalizedTime < Mathf.Max(0f, activeStart - 0.10f))
            return;

        attackSwingPlayed = true;
        CombatAudio.PlayEnemyAttackSwing();
    }

    private float ResolveAttackActiveStart(bool isFollowUp)
    {
        EnemyAttackWindow[] windows = isFollowUp
            ? currentAttack.followUpActiveWindows
            : currentAttack.useTimingWindows
                ? currentAttack.activeWindows
                : null;

        if (windows != null && windows.Length > 0)
        {
            float earliest = float.PositiveInfinity;
            foreach (EnemyAttackWindow window in windows)
                earliest = Mathf.Min(earliest, Mathf.Min(window.start, window.end));

            if (!float.IsPositiveInfinity(earliest))
                return earliest;
        }

        return isFollowUp ? 0.30f : currentAttack.startUpEnd;
    }

    private static bool IsInLegacyWindow(float normalizedTime, float start, float end)
    {
        float minimum = Mathf.Min(start, end);
        float maximum = Mathf.Max(start, end);
        return normalizedTime >= minimum && normalizedTime < maximum;
    }

    private static bool IsInAnyWindow(
        float normalizedTime,
        EnemyAttackWindow[] windows)
    {
        if (windows == null)
            return false;

        foreach (EnemyAttackWindow window in windows)
        {
            if (IsInLegacyWindow(normalizedTime, window.start, window.end))
                return true;
        }

        return false;
    }

    private void InitializeWarningSigns()
    {
        if (warningSign_Yellow != null)
        {
            warningYellowBaseScale = warningSign_Yellow.transform.localScale;
            warningSign_Yellow.SetActive(false);
        }

        if (warningSign_Red != null)
        {
            warningRedBaseScale = warningSign_Red.transform.localScale;
            warningSign_Red.SetActive(false);
        }
    }

    private void UpdateWarningSigns(bool showYellow, bool showRed)
    {
        SetWarningSignVisible(
            warningSign_Yellow,
            warningYellowBaseScale,
            showYellow,
            ref warningYellowVisible,
            ref warningYellowTween);

        SetWarningSignVisible(
            warningSign_Red,
            warningRedBaseScale,
            showRed,
            ref warningRedVisible,
            ref warningRedTween);
    }

    private static void SetWarningSignVisible(
        GameObject sign,
        Vector3 baseScale,
        bool visible,
        ref bool currentState,
        ref Tween tween)
    {
        if (sign == null || currentState == visible)
            return;

        currentState = visible;
        tween?.Kill(false);
        tween = null;

        if (!visible)
        {
            sign.transform.localScale = baseScale;
            sign.SetActive(false);
            return;
        }

        sign.SetActive(true);
        sign.transform.localScale = baseScale * 0.35f;

        tween = DOTween.Sequence()
            .Append(sign.transform
                .DOScale(baseScale, 0.09f)
                .SetEase(Ease.OutBack))
            .Append(sign.transform
                .DOPunchScale(baseScale * 0.12f, 0.14f, 5, 0.55f))
            .SetUpdate(true);

    }

    private void HideWarningSigns()
    {
        UpdateWarningSigns(false, false);
    }

    private void BeginFollowUp()
    {
        IsInWarningWindow = false;
        IsInActiveWindow = false;
        IsInReactionWindow = false;
        attackHitBox.SetActive(false);
        HideWarningSigns();
        ClearAttackMovement();
        ConfigureAttackHitBox(true);
        attackSwingPlayed = false;

        phase = EnemyAttackPhase.FollowUp;
        animator.speed = baseAnimatorSpeed * Mathf.Max(
            0.01f,
            currentAttack.followUpPlaybackSpeed);
        animator.CrossFade(currentAttack.followUpAnim, 0.05f);
    }

    private void FinishAttack()
    {
        string endAnimation = currentAttack.endAnim;

        IsInWarningWindow = false;
        IsInActiveWindow = false;
        IsInReactionWindow = false;
        attackHitBox.SetActive(false);
        HideWarningSigns();
        ClearAttackMovement();
        RestoreAnimatorSpeed();
        attackSwingPlayed = false;

        currentAttack = null;
        phase = EnemyAttackPhase.None;

        if (!string.IsNullOrEmpty(endAnimation))
            animator.CrossFade(endAnimation, 0.05f);
    }

    private void UpdateAttackTracking(float normalizedTime, bool isFollowUp)
    {
        attackTrackingActive = false;

        if (currentAttack == null || attackTarget == null || !currentAttack.useTargetTracking)
            return;

        EnemyAttackWindow[] trackingWindows = isFollowUp
            ? currentAttack.followUpTargetTrackingWindows
            : currentAttack.targetTrackingWindows;

        if (!IsInAnyWindow(normalizedTime, trackingWindows))
            return;

        Vector3 direction = attackTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        attackTrackingDirection = direction.normalized;
        attackTrackingRotationSpeed = Mathf.Max(
            0f,
            currentAttack.targetTrackingRotationSpeed);
        attackTrackingActive = true;

        if (currentAttack.steerMovementWhileTracking)
            attackMoveDirection = attackTrackingDirection;
    }
    private void ConfigureAttackHitBox(bool useFollowUpShape)
    {
        if (attackBoxCollider == null || currentAttack == null)
            return;

        bool hasFollowUpShape =
            useFollowUpShape && currentAttack.useFollowUpHitBoxShape;
        bool hasMainShape = currentAttack.useCustomHitBoxShape;

        Vector3 center;
        Vector3 size;

        if (hasFollowUpShape)
        {
            center = currentAttack.followUpHitBoxCenter;
            size = currentAttack.followUpHitBoxSize;
        }
        else if (hasMainShape)
        {
            center = currentAttack.hitBoxCenter;
            size = currentAttack.hitBoxSize;
        }
        else
        {
            center = defaultAttackHitBoxCenter;
            size = defaultAttackHitBoxSize;
        }

        attackBoxCollider.center = center;
        attackBoxCollider.size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(size.x)),
            Mathf.Max(0.01f, Mathf.Abs(size.y)),
            Mathf.Max(0.01f, Mathf.Abs(size.z)));
    }
    private void RestoreAnimatorSpeed()
    {
        if (animator != null)
            animator.speed = baseAnimatorSpeed;
    }

    private void BeginAttackMovement()
    {
        previousAttackMovementTime = 0f;
        pendingAttackMoveDistance = 0f;

        // 공격 도중 플레이어를 계속 추적하지 않도록 시작 순간의 정면을 고정한다.
        attackMoveDirection = transform.forward;
        attackMoveDirection.y = 0f;

        if (attackMoveDirection.sqrMagnitude > 0.0001f)
            attackMoveDirection.Normalize();
    }

    private void QueueAttackMovement(float normalizedTime)
    {
        if (currentAttack == null || !currentAttack.useForwardMovement)
            return;

        float currentTime = Mathf.Clamp01(normalizedTime);

        if (currentAttack.useDistanceBasedMovement)
        {
            float previousProgress = EvaluateMovementProgress(
                previousAttackMovementTime,
                currentAttack.moveStart,
                currentAttack.moveEnd);

            float currentProgress = EvaluateMovementProgress(
                currentTime,
                currentAttack.moveStart,
                currentAttack.moveEnd);

            float progressDelta = Mathf.Max(0f, currentProgress - previousProgress);

            pendingAttackMoveDistance +=
                currentAttack.forwardMoveDistance * progressDelta;
        }
        else if (currentTime >= currentAttack.moveStart &&
                 currentTime <= currentAttack.moveEnd)
        {
            pendingAttackMoveDistance +=
                currentAttack.forwardMoveSpeed * Time.deltaTime;
        }

        previousAttackMovementTime =
            Mathf.Max(previousAttackMovementTime, currentTime);
    }

    private static float EvaluateMovementProgress(
        float normalizedTime,
        float start,
        float end)
    {
        if (end <= start)
            return normalizedTime >= end ? 1f : 0f;

        return Mathf.InverseLerp(start, end, normalizedTime);
    }

    private void FixedUpdate()
    {
        if (enemyRigidbody == null)
            return;

        if (attackTrackingActive && attackTrackingDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                attackTrackingDirection,
                Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                enemyRigidbody.rotation,
                targetRotation,
                attackTrackingRotationSpeed * Time.fixedDeltaTime);

            enemyRigidbody.MoveRotation(nextRotation);
        }

        if (pendingAttackMoveDistance <= 0f)
            return;

        float moveDistance = pendingAttackMoveDistance;
        pendingAttackMoveDistance = 0f;

        Vector3 nextPosition =
            enemyRigidbody.position + attackMoveDirection * moveDistance;

        enemyRigidbody.MovePosition(nextPosition);
    }

    private void ClearAttackMovement()
    {
        previousAttackMovementTime = 0f;
        pendingAttackMoveDistance = 0f;
        attackMoveDirection = Vector3.zero;
        attackTrackingActive = false;
        attackTrackingDirection = Vector3.zero;
        attackTrackingRotationSpeed = 0f;
    }

    public void InterruptAttack()
    {
        if (attackHitBox != null)
            attackHitBox.SetActive(false);

        HideWarningSigns();

        IsInWarningWindow = false;
        IsInActiveWindow = false;
        IsInReactionWindow = false;

        RestoreAnimatorSpeed();
        ClearAttackMovement();
        attackSwingPlayed = false;

        if (animator != null && currentAttack != null && !string.IsNullOrEmpty(currentAttack.endAnim))
            animator.CrossFade(currentAttack.endAnim, 0.05f);

        currentAttack = null;
        phase = EnemyAttackPhase.None;
    }

    public void ApplyParryReaction()
    {
        // 공격 판정, 경고 표시, 추적 이동을 먼저 모두 끊은 뒤 패링 전용 경직을 시작한다.
        InterruptAttack();

        if (isGroggy || enemyData == null)
            return;

        isInHitReaction = true;
        hitReactionTimeRemaining = Mathf.Max(
            0.01f,
            enemyData.parryReactionDuration);

        if (animator != null && !string.IsNullOrEmpty(enemyData.parryReactionAnim))
        {
            animator.CrossFade(
                enemyData.parryReactionAnim,
                Mathf.Clamp(enemyData.parryReactionBlendDuration, 0f, 0.25f));
        }
    }
    private void BeginHitReaction()
    {
        if (isGroggy || enemyData.hitReactionDuration <= 0f)
            return;

        currentHitReactionGauge = Mathf.Clamp(
            enemyData.hitReactionResetValue,
            0f,
            enemyData.maxHitReactionGauge);
        isInHitReaction = true;
        hitReactionTimeRemaining = enemyData.hitReactionDuration;
        InterruptAttack();

        if (animator != null && !string.IsNullOrEmpty(enemyData.hitReactionAnim))
            animator.CrossFade(enemyData.hitReactionAnim, 0.04f);

        CombatPresentationEffects.Flash(Color.white, 0.025f, 0.14f);
    }

    private void UpdateHitReaction()
    {
        hitReactionTimeRemaining = Mathf.Max(
            0f,
            hitReactionTimeRemaining - Time.deltaTime);

        if (hitReactionTimeRemaining > 0f)
            return;

        isInHitReaction = false;

        if (animator != null && !string.IsNullOrEmpty(enemyData.hitReactionEndAnim))
            animator.CrossFade(enemyData.hitReactionEndAnim, 0.08f);
    }
    public void ReceiveHit(CombatHitData hitData)
    {
        if (isDefeated) return;
        if (hitData.attacker == null) return;
        if (enemyData == null) return;

        float baseDamage = hitData.attacker.CurrentAttack * hitData.damageMultiplier;

        // 관통률로 줄어든 유효 방어력을 완만한 승수로 바꿔 방어력이 피해를 완전히 막지 않게 한다.
        float effectiveDefense = enemyData.defense * (1f - hitData.attacker.CurrentPenRatio);
        effectiveDefense = Mathf.Max(0f, effectiveDefense);

        float defenseMultiplier = 100f / (100f + effectiveDefense);

        // 그로기 중 받는 피해 배율은 UI가 아닌 적 전투 데이터에서 결정한다.
        float finalDamage = baseDamage * defenseMultiplier * CurrentDamageTakenMultiplier;
        float hpBeforeDamage = currentHp;

        currentHp = Mathf.Clamp(currentHp - finalDamage, 0f, enemyData.maxHp);
        float appliedDamage = hpBeforeDamage - currentHp;

        bool emphasizeDamage =
            hitData.canTriggerChainSkill ||
            hitData.damageMultiplier >= 1.5f ||
            hitData.impactMultiplier >= 1.25f;
        CombatDamageNumberUI.Play(
            transform.position + Vector3.up * 1.65f,
            finalDamage,
            hitData.resolvedElement,
            emphasizeDamage);

        if (appliedDamage > 0f)
            DamageTaken?.Invoke(this, appliedDamage);

        if (currentHp <= 0f)
        {
            HandleDefeat();
            return;
        }

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

                TryTriggerAnomaly(state.element, i);

                break;
            }
        }
    }

    private void HandleDefeat()
    {
        if (isDefeated)
            return;

        isDefeated = true;
        currentHp = 0f;
        currentStun = 0f;
        currentHitReactionGauge = 0f;
        isInHitReaction = false;
        isGroggy = false;
        groggyTimeRemaining = 0f;

        InterruptAttack();

        EnemyCombatAI combatAI = GetComponent<EnemyCombatAI>();
        if (combatAI != null)
            combatAI.enabled = false;

        enabled = false;
        Defeated?.Invoke(this);
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
        isInHitReaction = false;
        hitReactionTimeRemaining = 0f;
        currentHitReactionGauge = 0f;
        groggyLoopStarted = false;
        currentStun = enemyData.maxStun;
        groggyTimeRemaining = Mathf.Max(0.01f, enemyData.groggyDuration);

        CombatPresentationEffects.PlayGroggy(currentAnomalyElement);
        CombatHitVfx.Play(
            transform.position + Vector3.up * 1.6f,
            Vector3.up,
            currentAnomalyElement,
            1.6f);
        InterruptAttack();

        if (animator != null && !string.IsNullOrEmpty(enemyData.groggyStartAnim))
            animator.CrossFade(enemyData.groggyStartAnim, 0.05f);

        if (requestChainSkill)
            ChainSkillRequested?.Invoke(this, attacker);
    }

    private void UpdateGroggy()
    {
        if (!groggyLoopStarted && animator != null &&
            !string.IsNullOrEmpty(enemyData.groggyStartAnim))
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(enemyData.groggyStartAnim) && info.normalizedTime >= 0.9f)
            {
                groggyLoopStarted = true;
                if (!string.IsNullOrEmpty(enemyData.groggyLoopAnim))
                    animator.CrossFade(enemyData.groggyLoopAnim, 0.08f);
            }
        }

        float duration = Mathf.Max(0.01f, enemyData.groggyDuration);
        groggyTimeRemaining = Mathf.Max(0f, groggyTimeRemaining - Time.deltaTime);
        currentStun = enemyData.maxStun * (groggyTimeRemaining / duration);

        if (groggyTimeRemaining > 0f)
            return;

        currentStun = 0f;
        isGroggy = false;
        groggyLoopStarted = false;

        if (animator != null && !string.IsNullOrEmpty(enemyData.groggyEndAnim))
            animator.CrossFade(enemyData.groggyEndAnim, 0.08f);
    }
}
