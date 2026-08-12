using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static IPlayerState;


public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    public CharacterController Controller => controller;

    [SerializeField] private CharacterData characterData;
    public CharacterData CharacterData => characterData;

    public bool IsInvincible { get; private set; }

    [Header("Party")]
    [SerializeField] private PartyManager partyManager;
    public PartyManager PartyManager => partyManager;

    [Header("Stats")]
    [SerializeField] private int currentLevel = 1;
    public int CurrentLevel => currentLevel;

    private CharacterLevelStat CurrentStat
    {
        get
        {
            if (characterData == null || characterData.statData == null)
                return default;

            // 런타임 전투 계산은 CharacterData가 가리키는 레벨별 원본 스탯에서 가져온다.
            return characterData.statData.GetStatByLevel(currentLevel);
        }
    }

    public float CurrentAttack => CurrentStat.attack;
    public float CurrentDefense => CurrentStat.defense;
    public float CurrentMaxHp => CurrentStat.hp;
    public float CurrentImpact => CurrentStat.impact;
    public float CurrentCritRate => CurrentStat.critRate;
    public float CurrentCritDamage => CurrentStat.critDamage;
    public float CurrentPenRatio => CurrentStat.penRatio;

    [SerializeField, Min(0f)] private float currentHp;
    public float CurrentHp => currentHp;
    public float CurrentHpNormalized => CurrentMaxHp > 0f
        ? Mathf.Clamp01(currentHp / CurrentMaxHp)
        : 0f;
    public bool IsDefeated => currentHp <= 0f;
    public event System.Action<PlayerController> HealthChanged;
    public event System.Action<PlayerController> Defeated;


    [Header("Reference")]
    [SerializeField] private Transform cameraYawPivot;
    public Transform CameraYawPivot => cameraYawPivot;

    [SerializeField] private Transform cameraFollowTarget;
    public Transform CameraFollowTarget => cameraFollowTarget;

    [Header("Move")]
    [SerializeField] private float runThreshold = 4f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    public float RunThreshold => runThreshold;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float RotationSpeed => rotationSpeed;

    public Vector2 MoveInput { get; private set; }
    public float CurrentSpeed { get; private set; }

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundedGravity = -2f;
    private float yVelocity;
    public float YVelocity => yVelocity;

    [Header("State")]
    private IPlayerState currentState;
    public IPlayerState CurrentState => currentState;
    public LocomotionState LocomotionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }
    public SkillState SkillState { get; private set; }
    public UltimateState UltimateState { get; private set; }
    public SupportState ParryState { get; private set; }

    public Vector3 LastHitDirection { get; private set; }
    public bool LastHitWasHeavy { get; private set; }

    [Header("Attack Assist")]
    [SerializeField] private LayerMask attackTargetMask = ~0;

    [Header("Skill")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 20f;
    [SerializeField] private float energyRecoveryRate = 1.2f;
    public float MaxEnergy => maxEnergy;
    public float CurrentEnergy => currentEnergy;
    public float EnergyRecoveryRate => energyRecoveryRate;
    public event System.Action<PlayerController> EnergyChanged;
    public bool IsEnhancedBranchReady =>
        characterData.enhancedSkillBranch != null &&
        currentEnergy >= characterData.enhancedSkillBranch.requiredEntryEnergy;

    [Header("Attack HitBox")]
    [SerializeField] private HitBox attackHitBox;
    public HitBox AttackHitBox => attackHitBox;


    [Header("Ultimate")]
    [SerializeField] private float maxDecibel = 3000f;
    [SerializeField] private float currentDecibel = 0f;
    [SerializeField] private float normalAttackDecibelGain = 80f;
    [SerializeField] private float skillDecibelGain = 120f;

    public float MaxDecibel => maxDecibel;
    public float CurrentDecibel => currentDecibel;
    public bool CanUseUltimate => currentDecibel >= maxDecibel;
    public event System.Action<PlayerController> DecibelChanged;

    public TMP_Text decibelText_temp;


    [Header("SupportPoint")]
    [SerializeField] private SupportPointManager supportPointManager;
    public SupportPointManager SupportPointManager => supportPointManager;
    public int CurrentSupportPoint => supportPointManager != null ? supportPointManager.CurrentSupportPoint : 0;

    public TMP_Text supportPointText_temp;

    [Header("Animation")]
    public Animator Animator { get; private set; }

    private bool isInitialized;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Animator = GetComponent<Animator>();

        if (characterData == null)
        {
            Debug.LogError("캐릭터 데이터가 없습니다.", this);
            enabled = false;
            return;
        }

        if (characterData.statData == null || !characterData.statData.HasUsableStats)
        {
            // 빈 스탯 데이터는 모든 전투 수치를 0으로 만들어도 컴파일 오류가 나지 않으므로 시작 시 명시적으로 차단한다.
            Debug.LogError($"'{characterData.characterName}'의 레벨별 전투 스탯이 없습니다.", this);
            enabled = false;
            return;
        }

        // 파티 HUD와 전투 판정이 같은 런타임 체력 원본을 사용하도록 시작 시 최대 체력으로 초기화한다.
        currentHp = Mathf.Max(0f, CurrentMaxHp);

        LocomotionState = new LocomotionState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);
        SkillState = new SkillState(this);
        UltimateState = new UltimateState(this);
        ParryState = new SupportState(this);

        isInitialized = true;
    }

    private void Start()
    {
        if (!isInitialized) return;

        // 처음 활성화된 대기 캐릭터가 교체 직후 지정받은 패링/회피 State를 덮어쓰지 않는다.
        if (currentState == null)
            ChangeState(LocomotionState);
    }

    private void Update()
    {
        if (ChainSkillPromptUI.IsAnyOpen)
            MoveInput = Vector2.zero;

        currentState?.Update();

        if (decibelText_temp != null)
            decibelText_temp.text = currentDecibel.ToString("F0");

        if (supportPointText_temp != null && supportPointManager != null)
            supportPointText_temp.text = supportPointManager.CurrentSupportPoint.ToString("F0");
    }

    public void ChangeState(IPlayerState newState)
    {
        if (currentState == newState) return;

        // 모든 상태 전이는 Exit 정리 후 Enter 초기화가 한 경로에서 실행되도록 통제한다.
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public bool TryReceiveHit()
    {
        return TryReceiveHit(default, transform.position - transform.forward, false);
    }

    public bool TryReceiveHit(Vector3 sourcePosition, bool heavyReaction)
    {
        return TryReceiveHit(default, sourcePosition, heavyReaction);
    }

    public bool TryReceiveHit(
        CombatHitData hitData,
        Vector3 sourcePosition,
        bool heavyReaction)
    {
        if (IsInvincible || IsDefeated)
            return false;

        ApplyDamage(hitData.rawDamage);

        // 전투 불능 이벤트가 파티 자동 교체를 즉시 실행하므로 기존 캐릭터의 피격 상태를 더 진행하지 않는다.
        if (IsDefeated)
            return true;

        Vector3 awayFromSource = transform.position - sourcePosition;
        awayFromSource.y = 0f;

        if (awayFromSource.sqrMagnitude > 0.0001f)
        {
            LastHitDirection = awayFromSource.normalized;
            transform.rotation = Quaternion.LookRotation(-LastHitDirection, Vector3.up);
        }
        else
        {
            LastHitDirection = -transform.forward;
        }

        LastHitWasHeavy = heavyReaction;
        ChangeState(HitState);
        return true;
    }

    public void ApplyDamage(float damage)
    {
        damage = Mathf.Max(0f, damage);
        if (damage <= 0f || IsDefeated)
            return;

        SetCurrentHealth(currentHp - damage);
    }

    public void RestoreHealth(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        SetCurrentHealth(currentHp + amount);
    }

    private void SetCurrentHealth(float value)
    {
        bool wasDefeated = IsDefeated;
        float clampedValue = Mathf.Clamp(value, 0f, Mathf.Max(0f, CurrentMaxHp));
        if (Mathf.Approximately(currentHp, clampedValue))
            return;

        currentHp = clampedValue;
        HealthChanged?.Invoke(this);

        if (!wasDefeated && IsDefeated)
        {
            SetCombatControlEnabled(false);
            Defeated?.Invoke(this);
        }
    }

    public void SetCombatControlEnabled(bool controlEnabled)
    {
        if (controlEnabled)
        {
            if (IsDefeated)
                return;

            enabled = true;
            if (isInitialized && currentState == null)
                ChangeState(LocomotionState);
            return;
        }

        // 시간 종료와 전투 불능 모두 같은 정리 경로를 사용해 공격 판정이나 무적 상태가 결과 화면 뒤에 남지 않게 한다.
        MoveInput = Vector2.zero;
        SetCurrentSpeed(0f);
        currentState?.Exit();
        currentState = null;
        attackHitBox?.SetActive(false);
        IsInvincible = false;
        enabled = false;
    }

    public void HandleGravity()
    {
        if (controller.isGrounded)
        {
            if (yVelocity < 0f)
                yVelocity = groundedGravity;
        }
        else
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    public void UpdateSpeed(bool hasInput)
    {
        float targetSpeed = hasInput ? characterData.maxSpeed : 0f;
        float speedChangeRate = hasInput ? acceleration : deceleration;

        CurrentSpeed = Mathf.MoveTowards(
            CurrentSpeed,
            targetSpeed,
            speedChangeRate * Time.deltaTime
        );
    }

    public Vector3 GetCameraRelativeMoveDirection()
    {
        if (cameraYawPivot == null)
            return Vector3.zero;

        Vector3 forward = cameraYawPivot.forward;
        Vector3 right = cameraYawPivot.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * MoveInput.y + right * MoveInput.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        return moveDir;
    }

    public void RotateToward(Vector3 moveDirection, float rotationMultiplier = 1f)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        float turnSpeed = rotationSpeed * Mathf.Max(0f, rotationMultiplier);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    public Transform FindAttackTarget(float radius, float maxAngle)
    {
        Vector3 origin = transform.position;
        Vector3 referenceForward = cameraYawPivot != null ? cameraYawPivot.forward : transform.forward;
        Vector3 playerForward = transform.forward;

        referenceForward.y = 0f;
        playerForward.y = 0f;

        if (referenceForward.sqrMagnitude < 0.0001f)
            referenceForward = transform.forward;

        if (playerForward.sqrMagnitude < 0.0001f)
            playerForward = transform.forward;

        referenceForward.Normalize();
        playerForward.Normalize();

        Collider[] hits = Physics.OverlapSphere(
            origin,
            radius,
            attackTargetMask,
            QueryTriggerInteraction.Collide);

        Transform selfRoot = transform.root;
        Transform bestPreferredTarget = null;
        float bestPreferredScore = float.MaxValue;
        Transform bestFallbackTarget = null;
        float bestFallbackScore = float.MaxValue;

        // 카메라 정면의 적을 우선하되 정면 후보가 없으면 반경 안의 적을 후순위로 고른다.
        foreach (Collider hit in hits)
        {
            HurtBox hurtBox = hit.GetComponent<HurtBox>();

            if (hurtBox == null)
                hurtBox = hit.GetComponentInParent<HurtBox>();

            if (hurtBox == null)
                continue;

            Transform targetRoot = hurtBox.OwnerRoot != null ? hurtBox.OwnerRoot : hurtBox.transform.root;

            if (targetRoot == null || targetRoot == selfRoot)
                continue;

            Vector3 toTarget = targetRoot.position - origin;
            toTarget.y = 0f;

            float sqrDistance = toTarget.sqrMagnitude;

            if (sqrDistance < 0.0001f)
                continue;

            float distance = Mathf.Sqrt(sqrDistance);
            Vector3 direction = toTarget / distance;
            float cameraAngle = Vector3.Angle(referenceForward, direction);
            float playerAngle = Vector3.Angle(playerForward, direction);
            float fallbackScore = (playerAngle * 0.35f) + (distance * 6f);

            if (Vector3.Dot(playerForward, direction) < 0f)
                fallbackScore += 15f;

            if (fallbackScore < bestFallbackScore)
            {
                bestFallbackScore = fallbackScore;
                bestFallbackTarget = targetRoot;
            }

            // maxAngle은 대상을 버리는 조건이 아니라 카메라 정면 우선권의 범위다.
            if (cameraAngle > maxAngle)
                continue;

            float preferredScore = cameraAngle + fallbackScore;

            if (preferredScore >= bestPreferredScore)
                continue;

            bestPreferredScore = preferredScore;
            bestPreferredTarget = targetRoot;
        }

        return bestPreferredTarget != null
            ? bestPreferredTarget
            : bestFallbackTarget;
    }

    public Vector3 GetAttackAssistDirection(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return direction.normalized;
    }

    public void GainEnergy(float amount)
    {
        SetEnergy(currentEnergy + amount);
    }

    public void RecoveryEnergyOverTime(float recoveryPerSecond)
    {
        SetEnergy(currentEnergy + recoveryPerSecond * Time.deltaTime);
    }

    public bool TryUseEnergy(float cost)
    {
        cost = Mathf.Max(0f, cost);
        if (currentEnergy < cost)
            return false;

        SetEnergy(currentEnergy - cost);
        return true;
    }

    private void SetEnergy(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, maxEnergy);
        if (Mathf.Approximately(currentEnergy, clampedValue))
            return;

        currentEnergy = clampedValue;
        EnergyChanged?.Invoke(this);
    }

    public void SetCurrentSpeed(float speed)
    {
        CurrentSpeed = speed;
    }

    public void GainDecibel(float amount)
    {
        SetDecibel(currentDecibel + amount);
    }

    public bool TryUseDecibel(float cost)
    {
        if (currentDecibel < cost)
            return false;

        SetDecibel(currentDecibel - cost);
        return true;
    }

    private void SetDecibel(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, maxDecibel);
        if (Mathf.Approximately(currentDecibel, clampedValue))
            return;

        currentDecibel = clampedValue;
        DecibelChanged?.Invoke(this);
    }

    public void GrantDecibelForNormalHit()
    {
        GainDecibel(normalAttackDecibelGain);
    }

    public void GrantDecibelForSkillHit()
    {
        GainDecibel(skillDecibelGain);
    }

    public void SetInvincible(bool value)
    {
        IsInvincible = value;
    }

    public void SetRuntimeReferences(PartyManager party, SupportPointManager support, Transform yawPivot)
    {
        // 씬 전역 참조는 프리팹에 고정하지 않고 파티 초기화 시 현재 전투 컨텍스트가 주입한다.
        partyManager = party;
        supportPointManager = support;
        cameraYawPivot = yawPivot;
    }

    #region Input
    public void OnMove(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen)
        {
            MoveInput = Vector2.zero;
            return;
        }

        MoveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen) return;

        if (value.isPressed)
            currentState?.HandleAttack();
    }

    public void OnDodge(InputValue value)
    {
        if (!value.isPressed) return;
        if (ChainSkillPromptUI.IsAnyOpen) return;


        EnemyController dodgeEnemy = partyManager.FindPerfectDodgeEnemy(this);

        if (dodgeEnemy != null)
        {
            DodgeState.SetDodgeType(DodgeType.Perfect);
            currentState?.HandleDodge();
            return;
        }

        DodgeState.SetDodgeType(DodgeType.Normal);
        currentState?.HandleDodge();
    }
    public void OnHitTest(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleHit();
    }

    public void OnSkill(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen) return;

        if (value.isPressed)
            currentState?.HandleSkill();
    }

    public void OnUltimate(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen)
            return;

        if (!value.isPressed)
            return;

        if (!isInitialized || currentState == null)
            return;

        // 이미 궁극기 상태라면 같은 궁극기를 다시 시작하지 않는다.
        if (currentState == UltimateState)
            return;

        // 피격·사망·컷신 등 명시적으로 잠긴 상태에서는 궁극기를 사용할 수 없다.
        if (currentState is IUltimateBlockingState)
            return;

        if (!CanUseUltimate)
            return;

        // 궁극기는 일반 행동의 캔슬 시점과 관계없이 현재 State를 즉시 종료한다.
        ChangeState(UltimateState);
    }

    public void OnSwitch_Nxt(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen)
            return;

        if (!value.isPressed)
            return;

        if (partyManager == null)
        {
            Debug.LogWarning("파티 관리자가 할당되지 않았습니다.");
            return;
        }

        partyManager.Switch_Nxt();
    }

    public void OnSwitch_Pre(InputValue value)
    {
        if (ChainSkillPromptUI.IsAnyOpen)
            return;

        if (!value.isPressed)
            return;

        if (partyManager == null)
        {
            Debug.LogWarning("파티 관리자가 할당되지 않았습니다.");
            return;
        }

        partyManager.Switch_Pre();
    }
    #endregion
}


