using UnityEngine;

public enum EnemyLocomotionMode
{
    Idle,
    Approach,
    Retreat,
    StrafeLeft,
    StrafeRight
}

// AI가 붙는 보스 루트에는 공격 실행기와 이동용 Rigidbody가 반드시 함께 있어야 한다.
[RequireComponent(typeof(EnemyController), typeof(Rigidbody))]
public class EnemyCombatAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyManager partyManager;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [Tooltip("이 거리 안에 들어오면 직선 추적을 멈추고 전투 포지셔닝을 시작한다.")]
    [SerializeField, Min(0f)] private float stoppingDistance = 4.5f;

    [Header("Combat Positioning")]
    [Tooltip("이 거리보다 가까우면 공격 공간을 확보하기 위해 뒤로 물러난다.")]
    [SerializeField, Min(0f)] private float minimumCombatDistance = 2.7f;
    [Tooltip("선회 중 유지하려는 기준 거리다.")]
    [SerializeField, Min(0f)] private float preferredCombatDistance = 3.65f;
    [Tooltip("기준 거리에서 이 값만큼 벗어나면 앞뒤로 거리를 보정한다.")]
    [SerializeField, Min(0f)] private float distanceTolerance = 0.4f;
    [SerializeField, Min(0f)] private float strafeSpeed = 1.9f;
    [SerializeField, Min(0f)] private float retreatSpeed = 2.25f;
    [SerializeField, Min(0.1f)] private float minStrafeDuration = 1.1f;
    [SerializeField, Min(0.1f)] private float maxStrafeDuration = 2.35f;
    [SerializeField, Min(0f)] private float minStrafePause = 0.12f;
    [SerializeField, Min(0f)] private float maxStrafePause = 0.38f;

    [Header("Animation")]
    [SerializeField] private string moveAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Run_Loop";
    [SerializeField] private string retreatAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Walk_B_Loop";
    [SerializeField] private string strafeLeftAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Walk_L_Loop";
    [SerializeField] private string strafeRightAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Walk_R_Loop";
    [SerializeField] private string idleAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Idle_Loop";
    [SerializeField, Range(0f, 0.5f)] private float animationBlendDuration = 0.1f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float minAttackCooldown = 1f;
    [SerializeField, Min(0f)] private float maxAttackCooldown = 2f;
    [SerializeField, Range(0f, 180f)] private float attackFacingTolerance = 15f;

    [Header("Runtime")]
    [SerializeField] private EnemyLocomotionMode locomotionMode = EnemyLocomotionMode.Idle;

    private EnemyController enemyController;
    private Rigidbody enemyRigidbody;
    private PlayerController targetPlayer;
    private string currentLocomotionAnimation;
    private float attackCooldownRemaining;
    private float strafeTimeRemaining;
    private float strafePauseRemaining;
    private int strafeDirection = 1;

    public EnemyLocomotionMode LocomotionMode => locomotionMode;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        enemyRigidbody = GetComponent<Rigidbody>();

        // 런타임에 생성되는 보스는 씬의 PartyManager를 미리 참조할 수 없으므로 필요할 때 찾는다.
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        ResetAttackCooldown();
        ResetStrafeCycle(true);
    }

    private void OnEnable()
    {
        ResetAttackCooldown();
        ResetStrafeCycle(true);
        currentLocomotionAnimation = null;
        locomotionMode = EnemyLocomotionMode.Idle;
    }

    private void Update()
    {
        RefreshTarget();
        enemyController.SetAttackTarget(
            targetPlayer != null ? targetPlayer.transform : null);
        UpdateAttackDecision();
    }

    private void FixedUpdate()
    {
        if (enemyController.IsAttacking ||
            enemyController.IsGroggy ||
            enemyController.IsInHitReaction)
        {
            // 공격·경직 애니메이션이 끝난 뒤 이동 애니메이션을 다시 선택하도록 캐시만 비운다.
            currentLocomotionAnimation = null;
            locomotionMode = EnemyLocomotionMode.Idle;
            return;
        }

        if (targetPlayer == null)
        {
            SetLocomotion(EnemyLocomotionMode.Idle, Vector3.zero, 0f);
            return;
        }

        RotateTowardTarget();
        UpdateCombatMovement();
    }

    private void RefreshTarget()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        PlayerController nextTarget = partyManager != null
            ? partyManager.GetCurrentCharacter()
            : null;

        if (nextTarget == targetPlayer)
            return;

        targetPlayer = nextTarget;
        ResetStrafeCycle(true);
    }

    private void RotateTowardTarget()
    {
        Vector3 direction = targetPlayer.transform.position - enemyRigidbody.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            enemyRigidbody.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        enemyRigidbody.MoveRotation(nextRotation);
    }

    private void UpdateCombatMovement()
    {
        Vector3 towardTarget = targetPlayer.transform.position - enemyRigidbody.position;
        towardTarget.y = 0f;

        float distance = towardTarget.magnitude;
        if (distance <= 0.0001f)
        {
            SetLocomotion(EnemyLocomotionMode.Idle, Vector3.zero, 0f);
            return;
        }

        towardTarget /= distance;

        if (distance > stoppingDistance)
        {
            SetLocomotion(EnemyLocomotionMode.Approach, towardTarget, moveSpeed);
            return;
        }

        if (distance < minimumCombatDistance)
        {
            SetLocomotion(EnemyLocomotionMode.Retreat, -towardTarget, retreatSpeed);
            return;
        }

        UpdateStrafeCycle();
        if (strafePauseRemaining > 0f)
        {
            SetLocomotion(EnemyLocomotionMode.Idle, Vector3.zero, 0f);
            return;
        }

        float lowerDistance = Mathf.Max(
            minimumCombatDistance,
            preferredCombatDistance - distanceTolerance);
        float upperDistance = Mathf.Min(
            stoppingDistance,
            preferredCombatDistance + distanceTolerance);

        Vector3 radialCorrection = Vector3.zero;
        if (distance < lowerDistance)
            radialCorrection = -towardTarget;
        else if (distance > upperDistance)
            radialCorrection = towardTarget;

        Vector3 tangent = Vector3.Cross(Vector3.up, towardTarget) * strafeDirection;
        Vector3 moveDirection = (tangent + radialCorrection * 0.45f).normalized;
        EnemyLocomotionMode strafeMode = strafeDirection < 0
            ? EnemyLocomotionMode.StrafeLeft
            : EnemyLocomotionMode.StrafeRight;

        SetLocomotion(strafeMode, moveDirection, strafeSpeed);
    }

    private void UpdateStrafeCycle()
    {
        if (strafePauseRemaining > 0f)
        {
            strafePauseRemaining = Mathf.Max(
                0f,
                strafePauseRemaining - Time.fixedDeltaTime);

            if (strafePauseRemaining <= 0f)
                ResetStrafeCycle(false);

            return;
        }

        strafeTimeRemaining = Mathf.Max(
            0f,
            strafeTimeRemaining - Time.fixedDeltaTime);
        if (strafeTimeRemaining > 0f)
            return;

        float minimumPause = Mathf.Min(minStrafePause, maxStrafePause);
        float maximumPause = Mathf.Max(minStrafePause, maxStrafePause);
        strafePauseRemaining = Random.Range(minimumPause, maximumPause);
        strafeDirection *= -1;
    }

    private void ResetStrafeCycle(bool randomizeDirection)
    {
        float minimum = Mathf.Min(minStrafeDuration, maxStrafeDuration);
        float maximum = Mathf.Max(minStrafeDuration, maxStrafeDuration);
        strafeTimeRemaining = Random.Range(minimum, maximum);
        strafePauseRemaining = 0f;

        if (randomizeDirection)
            strafeDirection = Random.value < 0.5f ? -1 : 1;
    }

    private void SetLocomotion(
        EnemyLocomotionMode nextMode,
        Vector3 moveDirection,
        float speed)
    {
        locomotionMode = nextMode;
        string nextAnimation = ResolveLocomotionAnimation(nextMode);

        if (currentLocomotionAnimation != nextAnimation)
        {
            currentLocomotionAnimation = nextAnimation;

            if (enemyController.animator != null &&
                !string.IsNullOrEmpty(nextAnimation))
            {
                // 이동 모드가 바뀔 때만 전환해 루프 애니메이션이 매 프레임 재시작되지 않게 한다.
                enemyController.animator.CrossFade(
                    nextAnimation,
                    animationBlendDuration);
            }
        }

        if (speed <= 0f || moveDirection.sqrMagnitude < 0.0001f)
            return;

        Vector3 nextPosition = enemyRigidbody.position +
            moveDirection.normalized * speed * Time.fixedDeltaTime;
        enemyRigidbody.MovePosition(nextPosition);
    }

    private string ResolveLocomotionAnimation(EnemyLocomotionMode mode)
    {
        return mode switch
        {
            EnemyLocomotionMode.Approach => moveAnimationName,
            EnemyLocomotionMode.Retreat => retreatAnimationName,
            EnemyLocomotionMode.StrafeLeft => strafeLeftAnimationName,
            EnemyLocomotionMode.StrafeRight => strafeRightAnimationName,
            _ => idleAnimationName
        };
    }

    private void UpdateAttackDecision()
    {
        if (targetPlayer == null)
            return;

        if (enemyController.IsAttacking ||
            enemyController.IsGroggy ||
            enemyController.IsInHitReaction)
            return;

        Vector3 direction = targetPlayer.transform.position - enemyRigidbody.position;
        direction.y = 0f;

        float attackDistanceSqr = stoppingDistance * stoppingDistance;
        if (direction.sqrMagnitude > attackDistanceSqr)
            return;

        if (direction.sqrMagnitude > 0.0001f)
        {
            float facingAngle = Vector3.Angle(
                enemyRigidbody.rotation * Vector3.forward,
                direction);

            if (facingAngle > attackFacingTolerance)
                return;
        }

        // 전투 거리와 방향 조건을 만족한 시간만 쿨타임으로 계산한다.
        attackCooldownRemaining -= Time.deltaTime;
        if (attackCooldownRemaining > 0f)
            return;

        if (enemyController.TryStartAttack())
        {
            currentLocomotionAnimation = null;
            ResetAttackCooldown(enemyController.CurrentAttackRecoveryDelay);
            ResetStrafeCycle(true);
        }
    }

    private void ResetAttackCooldown(float additionalDelay = 0f)
    {
        float minimum = Mathf.Min(minAttackCooldown, maxAttackCooldown);
        float maximum = Mathf.Max(minAttackCooldown, maxAttackCooldown);

        attackCooldownRemaining =
            Random.Range(minimum, maximum) + Mathf.Max(0f, additionalDelay);
    }
}
