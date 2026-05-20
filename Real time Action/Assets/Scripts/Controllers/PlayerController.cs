using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;


public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    public CharacterController Controller => controller;

    public bool IsInvincible { get; private set; }

    [Header("Reference")]
    [SerializeField] private Transform cameraYawPivot;
    public Transform CameraYawPivot => cameraYawPivot;

    [Header("Move")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float runThreshold = 4f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    public float MaxSpeed => maxSpeed;
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

    public LocomotionState LocomotionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }
    public SkillState SkillState { get; private set; }
    public UltimateState UltimateState { get; private set; }

    [Header("Attack Combo")]
    public AttackData[] normalCombo;

    [Header("Attack Assist")]
    [SerializeField] private LayerMask attackTargetMask = ~0;

    [Header("Skill")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 20f;
    [SerializeField] private float energyRecoveryRate = 1.2f;
    [SerializeField] SkillData normalSkillBranch;
    [SerializeField] SkillData enhancedSkillBranch;
    public float MaxEnergy => maxEnergy;
    public float CurrentEnergy => currentEnergy;
    public float EnergyRecoveryRate => energyRecoveryRate;
    public SkillData NormalSkillBranch => normalSkillBranch;
    public SkillData EnhancedSkillBranch => enhancedSkillBranch;
    public bool IsEnhancedBranchReady =>
        enhancedSkillBranch != null &&
        currentEnergy >= enhancedSkillBranch.requiredEntryEnergy;

    public TMP_Text energyText_temp;

    [Header("Ultimate")]
    [SerializeField] private float maxDecibel = 3000f;
    [SerializeField] private float currentDecibel = 0f;
    [SerializeField] private float normalAttackDecibelGain = 80f;
    [SerializeField] private float skillDecibelGain = 120f;
    [SerializeField] UltimateData ultimateData;
    [SerializeField] private HitBox ultHitBox;
    public HitBox UltHitBox => ultHitBox;

    public UltimateData UltimateData => ultimateData;
    public float MaxDecibel => maxDecibel;
    public float CurrentDecibel => currentDecibel;
    public bool CanUseUltimate => currentDecibel >= maxDecibel;

    public TMP_Text decibelText_temp;

    [Header("SkillHitBox")]
    [SerializeField] private HitBox[] skillHitBoxSlots;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 8f;
    public float DodgeSpeed => dodgeSpeed;

    [Header("Animation")]
    public Animator Animator { get; private set; }

    

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Animator = GetComponent<Animator>();

        LocomotionState = new LocomotionState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);
        SkillState = new SkillState(this);
        UltimateState = new UltimateState(this);
    }

    private void Start()
    {
        ChangeState(LocomotionState);
    }

    private void Update()
    {
        currentState?.Update();

        if (energyText_temp != null)
            energyText_temp.text = currentEnergy.ToString("F0");

        if (decibelText_temp != null)
            decibelText_temp.text = currentDecibel.ToString("F0");
    }

    public void ChangeState(IPlayerState newState)
    {
        if (currentState == newState) return;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void ReceiveHit()
    {
        if (IsInvincible) return;
        
        ChangeState(HitState);
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
        float targetSpeed = hasInput ? maxSpeed : 0f;
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
        if(skillHitBoxSlots == null) return null;

        if(slotIndex < 0 || slotIndex >= skillHitBoxSlots.Length) return null;

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

    #region Input
    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleAttack();
    }

    public void OnDodge(InputValue value)
    {
        if (value.isPressed)
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
        if (value.isPressed)
            currentState?.HandleUltimate();
    }
    #endregion
}


