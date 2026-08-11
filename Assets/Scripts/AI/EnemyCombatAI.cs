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
    [SerializeField, Min(0f)] private float stoppingDistance = 4.5f;

    [Header("Animation")]
    [SerializeField]
    private string moveAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Run_Loop";

    [SerializeField]
    private string idleAnimationName =
        "NotoriousDeadEndButcher_Ani_P1_Idle_Loop";

    [SerializeField, Range(0f, 0.5f)] private float animationBlendDuration = 0.1f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float minAttackCooldown = 1f;
    [SerializeField, Min(0f)] private float maxAttackCooldown = 2f;
    [SerializeField, Range(0f, 180f)] private float attackFacingTolerance = 15f;

    private EnemyController enemyController;
    private Rigidbody enemyRigidbody;

    private PlayerController targetPlayer;

    private bool isMoving;

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

    private void Update()
    {
        RefreshTarget();
        enemyController.SetAttackTarget(
            targetPlayer != null ? targetPlayer.transform : null);
        UpdateAttackDecision();
    }

    private void FixedUpdate()
    {
        if (enemyController.IsAttacking)
        {
            // 공격 종료 후 필요하면 이동 애니메이션을 다시 재생할 수 있게 상태만 초기화한다.
            isMoving = false;
            return;
        }

        if (enemyController.IsGroggy || enemyController.IsInHitReaction)
        {
            // 경직·그로기 애니메이션은 EnemyController가 소유하므로 이동 애니메이션으로 덮지 않는다.
            isMoving = false;
            return;
        }

        if (targetPlayer == null)
        {
            SetMovementAnimation(false);
            return;
        }

        RotateTowardTarget();

        bool moved = MoveTowardTarget();
        SetMovementAnimation(moved);
    }

    private void RefreshTarget()
    {
        if (partyManager == null)
        {
            targetPlayer = null;
            return;
        }

        targetPlayer = partyManager.GetCurrentCharacter();
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

        if (remainingDistance <= 0f)
            return false;

        // 한 프레임 이동량을 남은 거리보다 작게 제한해 정지 지점을 넘나드는 떨림을 막는다.
        float moveDistance = Mathf.Min(
            moveSpeed * Time.fixedDeltaTime,
            remainingDistance);

        Vector3 nextPosition =
            enemyRigidbody.position + direction.normalized * moveDistance;

        enemyRigidbody.MovePosition(nextPosition);
        return true;
    }

    private void SetMovementAnimation(bool shouldMove)
    {
        if (isMoving == shouldMove)
            return;

        isMoving = shouldMove;

        string nextAnimation = shouldMove
            ? moveAnimationName
            : idleAnimationName;

        if (enemyController.animator == null || string.IsNullOrEmpty(nextAnimation))
            return;

        // 상태가 달라질 때만 CrossFade해 매 물리 프레임마다 애니메이션이 재시작되지 않게 한다.
        enemyController.animator.CrossFade(nextAnimation, animationBlendDuration);
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

        // 공격 거리와 방향 조건을 모두 만족한 시간만 쿨타임으로 계산한다.
        attackCooldownRemaining -= Time.deltaTime;

        if (attackCooldownRemaining > 0f)
            return;

        if (enemyController.TryStartAttack())
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
