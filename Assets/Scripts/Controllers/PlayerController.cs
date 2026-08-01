using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;


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

    [Header("Attack Assist")]
    [SerializeField] private LayerMask attackTargetMask = ~0;

    [Header("Skill")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 20f;
    [SerializeField] private float energyRecoveryRate = 1.2f;
    public float MaxEnergy => maxEnergy;
    public float CurrentEnergy => currentEnergy;
    public float EnergyRecoveryRate => energyRecoveryRate;
    public bool IsEnhancedBranchReady =>
        characterData.enhancedSkillBranch != null &&
        currentEnergy >= characterData.enhancedSkillBranch.requiredEntryEnergy;

    public TMP_Text energyText_temp;

    [Header("Ultimate")]
    [SerializeField] private float maxDecibel = 3000f;
    [SerializeField] private float currentDecibel = 0f;
    [SerializeField] private float normalAttackDecibelGain = 80f;
    [SerializeField] private float skillDecibelGain = 120f;
    [SerializeField] private HitBox ultHitBox;
    public HitBox UltHitBox => ultHitBox;

    public float MaxDecibel => maxDecibel;
    public float CurrentDecibel => currentDecibel;
    public bool CanUseUltimate => currentDecibel >= maxDecibel;

    public TMP_Text decibelText_temp;

    [Header("SkillHitBox")]
    [SerializeField] private HitBox[] skillHitBoxSlots;
    public int SkillHitBoxSlotCount => skillHitBoxSlots != null ? skillHitBoxSlots.Length : 0;

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
            Debug.LogError("CharacterData is missing", this);
            enabled = false;
            return;
        }

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

        if (currentState == null)
            ChangeState(LocomotionState);
    }

    private void Update()
    {
        currentState?.Update();

        if (energyText_temp != null)
            energyText_temp.text = currentEnergy.ToString("F0");

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
        if (IsInvincible) return false;

        ChangeState(HitState);
        return true;
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
        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        // 카메라 정면을 우선하되 캐릭터 방향과 거리도 합산해 조작자가 의도한 적을 고른다.
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

            if (cameraAngle > maxAngle)
                continue;

            float playerAngle = Vector3.Angle(playerForward, direction);
            float score = cameraAngle + (playerAngle * 0.35f) + (distance * 6f);

            if (Vector3.Dot(playerForward, direction) < 0f)
                score += 15f;

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = targetRoot;
        }

        return bestTarget;
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
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        // NotifyEnergyChanged();  // 나중에 이벤트 용(무지갯빛 스킬 게이지 UI 옵저버 패턴)
    }

    public void RecoveryEnergyOverTime(float recoveryPerSecond)
    {
        currentEnergy = Mathf.Clamp(
            currentEnergy + recoveryPerSecond * Time.deltaTime,
            0f,
            maxEnergy);
        // NotifyEnergyChanged();
    }

    public bool TryUseEnergy(float cost)
    {
        if (currentEnergy < cost) return false;

        currentEnergy -= cost;
        // NotifyEnergyChanged();
        return true;
    }

    public HitBox GetSkillHitBox(int slotIndex)
    {
        if (skillHitBoxSlots == null) return null;

        if (slotIndex < 0 || slotIndex >= skillHitBoxSlots.Length) return null;

        return skillHitBoxSlots[slotIndex];
    }
    public void SetCurrentSpeed(float speed)
    {
        CurrentSpeed = speed;
    }

    public void GainDecibel(float amount)
    {
        currentDecibel = Mathf.Clamp(currentDecibel + amount, 0f, maxDecibel);
    }

    public bool TryUseDecibel(float cost)
    {
        if (currentDecibel < cost)
            return false;

        currentDecibel -= cost;
        return true;
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
        MoveInput = value.Get<Vector2>();
        Debug.Log($"{name} MoveInput = {MoveInput}");
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleAttack();
    }

    public void OnDodge(InputValue value)
    {
        if (!value.isPressed) return;

        EnemyController dodgeEnemy = partyManager.FindReactionEnemy(this);

        if (dodgeEnemy != null)
        {
            DodgeState.SetDodgeType(DodgeType.Perfect);
            currentState?.HandleDodge();

            Debug.Log("극한 회피!!!");
            return;
        }

        DodgeState.SetDodgeType(DodgeType.Normal);
        Debug.Log("회피!");
        currentState?.HandleDodge();
    }
    public void OnHitTest(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleHit();
    }

    public void OnSkill(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleSkill();
    }

    public void OnUltimate(InputValue value)
    {
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
        if (!value.isPressed)
            return;

        if (partyManager == null)
        {
            Debug.LogWarning("PartyManager is not assigned.");
            return;
        }

        partyManager.Switch_Nxt();
    }

    public void OnSwitch_Pre(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (partyManager == null)
        {
            Debug.LogWarning("PartyManager is not assigned.");
            return;
        }

        partyManager.Switch_Pre();
    }
    #endregion
}


