using UnityEngine;

// AI가 붙는 보스 루트에는 공격 실행기와 이동용 Rigidbody가 반드시 함께 있어야 한다.
[RequireComponent(typeof(EnemyController), typeof(Rigidbody))]
public class EnemyCombatAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyManager partyManager;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [Tooltip("이 거리 안에서는 이동을 멈추고 연속 공격을 시도한다.")]
    [SerializeField, Min(0f)] private float stoppingDistance = 4.5f;

    [Header("Animation")]
    [SerializeField] private string moveAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Run_Loop";
    [SerializeField] private string idleAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Idle_Loop";
    [SerializeField, Range(0f, 0.5f)] private float animationBlendDuration = 0.1f;

    [Header("Combat Pressure")]
    [Tooltip("한 패턴이 끝난 뒤 다음 패턴을 시작하기까지의 최소 대기 시간이다.")]
    [SerializeField, Min(0f)] private float minAttackCooldown = 0.12f;
    [Tooltip("한 패턴이 끝난 뒤 다음 패턴을 시작하기까지의 최대 대기 시간이다.")]
    [SerializeField, Min(0f)] private float maxAttackCooldown = 0.32f;
    [SerializeField, Range(0f, 180f)] private float attackFacingTolerance = 22f;

    private EnemyController enemyController;
    private Rigidbody enemyRigidbody;
    private PlayerController targetPlayer;
    private string currentMovementAnimation;
    private float attackCooldownRemaining;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        enemyRigidbody = GetComponent<Rigidbody>();

        // 런타임에 생성되는 보스는 씬의 PartyManager를 미리 참조할 수 없으므로 필요할 때 찾는다.
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        ResetAttackCooldown();
    }

    private void OnEnable()
    {
        // 강습전 트리거가 보스를 활성화할 때 첫 공격도 짧은 준비 시간 뒤 시작한다.
        ResetAttackCooldown();
        currentMovementAnimation = null;
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
            // 공격·경직·그로기 애니메이션은 EnemyController가 소유하므로 이동 애니메이션으로 덮지 않는다.
            currentMovementAnimation = null;
            return;
        }

        if (targetPlayer == null)
        {
            SetMovementAnimation(idleAnimationName);
            return;
        }

        RotateTowardTarget();
        bool moved = MoveTowardTarget();
        SetMovementAnimation(moved ? moveAnimationName : idleAnimationName);
    }

    private void RefreshTarget()
    {
        if (partyManager == null)
            partyManager = FindFirstObjectByType<PartyManager>();

        targetPlayer = partyManager != null
            ? partyManager.GetCurrentCharacter()
            : null;
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

    private bool MoveTowardTarget()
    {
        Vector3 direction = targetPlayer.transform.position - enemyRigidbody.position;
        direction.y = 0f;

        float distance = direction.magnitude;
        float remainingDistance = distance - stoppingDistance;
        if (remainingDistance <= 0f || distance <= 0.0001f)
            return false;

        // 공격 가능 거리에 들어오면 정확히 멈추도록 남은 거리보다 많이 이동하지 않는다.
        float moveDistance = Mathf.Min(
            moveSpeed * Time.fixedDeltaTime,
            remainingDistance);
        Vector3 nextPosition =
            enemyRigidbody.position + direction.normalized * moveDistance;

        enemyRigidbody.MovePosition(nextPosition);
        return true;
    }

    private void SetMovementAnimation(string nextAnimation)
    {
        if (currentMovementAnimation == nextAnimation)
            return;

        currentMovementAnimation = nextAnimation;
        if (enemyController.animator == null || string.IsNullOrEmpty(nextAnimation))
            return;

        // 이동과 대기 상태가 실제로 바뀔 때만 전환해 루프가 매 물리 프레임 재시작되지 않게 한다.
        enemyController.animator.CrossFade(nextAnimation, animationBlendDuration);
    }

    private void UpdateAttackDecision()
    {
        if (targetPlayer == null ||
            enemyController.IsAttacking ||
            enemyController.IsGroggy ||
            enemyController.IsInHitReaction)
        {
            return;
        }

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

        // 공격할 수 있는 거리에서는 선회하지 않고 다음 패턴까지의 짧은 텀만 계산한다.
        attackCooldownRemaining -= Time.deltaTime;
        if (attackCooldownRemaining > 0f)
            return;

        if (!enemyController.TryStartAttack())
            return;

        currentMovementAnimation = null;
        ResetAttackCooldown(enemyController.CurrentAttackRecoveryDelay);
    }

    private void ResetAttackCooldown(float additionalDelay = 0f)
    {
        float minimum = Mathf.Min(minAttackCooldown, maxAttackCooldown);
        float maximum = Mathf.Max(minAttackCooldown, maxAttackCooldown);

        attackCooldownRemaining =
            Random.Range(minimum, maximum) + Mathf.Max(0f, additionalDelay);
    }
}
