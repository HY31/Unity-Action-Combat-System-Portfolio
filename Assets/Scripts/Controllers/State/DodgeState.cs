using UnityEngine;

public enum DodgeType
{
    Normal,
    Perfect
}

/// <summary>
/// 일반·극한 회피의 이동과 무적을 처리하고, 극한 회피 성공 시 불릿 타임과 회피 반격을 연결한다.
/// </summary>
public class DodgeState : IPlayerState
{
    private readonly PlayerController player;
    private CharacterData characterData;
    private const float NormalDodgeDuration = 0.3f;
    private const float PerfectDodgeDuration = 0.58f;
    private const float PerfectDodgeBulletTimeDuration = 0.78f;
    private const float NormalDodgeInvincibilityDuration = 0.45f;
    private const float PerfectDodgeInvincibilityDuration = 0.9f;
    private const float PerfectDodgeFollowUpOpenTime = 0.24f;
    private const float PerfectDodgeSlowPhaseDuration = 0.1f;
    private const float PerfectDodgeAccelerationEndTime = 0.3f;
    private const float PerfectDodgeSettleEndTime = 0.44f;
    private const float PerfectDodgeInitialAnimationSpeed = 0.18f;
    private const float PerfectDodgePeakAnimationSpeed = 1.45f;

    private float timer;
    private Vector3 dodgeDirection;
    private DodgeType dodgeType = DodgeType.Normal;

    private float elapsedTime;
    private float baseAnimatorSpeed = 1f;

    private bool bufferedAttackInput;
    private float bufferedAttackTimer;

    private bool bufferedSkillInput;
    private float bufferedSkillTimer;

    private const float InputBufferDuration = 0.35f;

    public DodgeState(PlayerController player)
    {
        this.player = player;
    }

    public void SetDodgeType(DodgeType type)
    {
        dodgeType = type;
    }

    public void Enter()
    {
        characterData = player.CharacterData;

        elapsedTime = 0f;
        baseAnimatorSpeed = player.Animator != null ? player.Animator.speed : 1f;
        ResetFollowUpInputs();

        // 회피 상태가 끝나 후속 공격으로 넘어가도 별도의 실시간 무적 시간이 남아 다단 공격을 받아낸다.
        timer = dodgeType == DodgeType.Perfect ? PerfectDodgeDuration : NormalDodgeDuration;
        player.SetInvincible(true);
        player.GrantTimedInvincibility(
            dodgeType == DodgeType.Perfect
                ? PerfectDodgeInvincibilityDuration
                : NormalDodgeInvincibilityDuration);

        Vector3 inputDir = player.GetCameraRelativeMoveDirection();
        dodgeDirection = inputDir.sqrMagnitude > 0.0001f ? inputDir : player.transform.forward;

        player.RotateToward(dodgeDirection);
        player.Animator.CrossFade(characterData.dodgeFrontAnim, 0.05f);

        if (dodgeType == DodgeType.Perfect)
        {
            // 주변은 강하게 느려지고 회피 애니메이션은 순간 감속 뒤 급가속해 성공 순간을 강조한다.
            player.BeginUnscaledActionTime(PerfectDodgeBulletTimeDuration);
            SetAnimatorSpeed(PerfectDodgeInitialAnimationSpeed);
            CombatAudio.PlayPerfectDodge();
            CombatPresentationEffects.PlayPerfectDodge(player);
            CombatOperationEvents.Report(CombatOperationType.PerfectDodge, player);
        }
    }

    public void Update()
    {
        float deltaTime = player.ActionDeltaTime;

        timer -= deltaTime;
        elapsedTime += deltaTime;

        UpdatePerfectDodgeAnimationSpeed();
        UpdateInputBuffers();

        player.HandleGravity();

        Vector3 move = dodgeDirection * characterData.dodgeSpeed;
        move.y = player.YVelocity;

        player.Controller.Move(move * deltaTime);

        // 동시에 입력됐을 경우 특수 스킬을 평타보다 우선한다.
        if (TryCancelToSkill())
            return;

        if (TryCancelToAttack())
            return;

        if (timer <= 0f)
            player.ChangeState(player.LocomotionState);
    }

    public void Exit()
    {
        SetAnimatorSpeed(1f);
        player.SetInvincible(false);
        dodgeType = DodgeType.Normal;
        elapsedTime = 0f;
        ResetFollowUpInputs();
    }

    #region Handle
    public void HandleAttack()
    {
        bufferedAttackInput = true;
        bufferedAttackTimer = InputBufferDuration;
    }

    public void HandleDodge() { }

    public void HandleHit()
    {
        if (!player.IsInvincible)
            player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        bufferedSkillInput = true;
        bufferedSkillTimer = InputBufferDuration;
    }

    public void HandleUltimate()
    {
    }
    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion

    private bool TryCancelToAttack()
    {
        if (!bufferedAttackInput)
            return false;

        float cancelOpenTime = dodgeType == DodgeType.Perfect
            ? Mathf.Max(characterData.dodgeAttackCancelTime, PerfectDodgeFollowUpOpenTime)
            : characterData.dodgeAttackCancelTime;

        if (elapsedTime < cancelOpenTime)
            return false;

        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;

        player.ChangeState(player.AttackState);
        return true;
    }

    private bool TryCancelToSkill()
    {
        if (!bufferedSkillInput)
            return false;

        float cancelOpenTime = dodgeType == DodgeType.Perfect
            ? Mathf.Max(characterData.dodgeSkillCancelTime, PerfectDodgeFollowUpOpenTime)
            : characterData.dodgeSkillCancelTime;

        if (elapsedTime < cancelOpenTime)
            return false;

        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;

        player.ChangeState(player.SkillState);
        return true;
    }

    private void UpdateInputBuffers()
    {
        if (bufferedAttackInput)
        {
            bufferedAttackTimer -= player.ActionDeltaTime;

            if (bufferedAttackTimer <= 0f)
            {
                bufferedAttackInput = false;
                bufferedAttackTimer = 0f;
            }
        }

        if (bufferedSkillInput)
        {
            bufferedSkillTimer -= player.ActionDeltaTime;

            if (bufferedSkillTimer <= 0f)
            {
                bufferedSkillInput = false;
                bufferedSkillTimer = 0f;
            }
        }
    }

    private void ResetFollowUpInputs()
    {
        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;

        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
    }

    private void UpdatePerfectDodgeAnimationSpeed()
    {
        if (dodgeType != DodgeType.Perfect)
            return;

        float speedMultiplier;

        if (elapsedTime <= PerfectDodgeSlowPhaseDuration)
        {
            speedMultiplier = PerfectDodgeInitialAnimationSpeed;
        }
        else if (elapsedTime <= PerfectDodgeAccelerationEndTime)
        {
            float t = Mathf.InverseLerp(
                PerfectDodgeSlowPhaseDuration,
                PerfectDodgeAccelerationEndTime,
                elapsedTime);
            speedMultiplier = Mathf.SmoothStep(
                PerfectDodgeInitialAnimationSpeed,
                PerfectDodgePeakAnimationSpeed,
                t);
        }
        else if (elapsedTime <= PerfectDodgeSettleEndTime)
        {
            float t = Mathf.InverseLerp(
                PerfectDodgeAccelerationEndTime,
                PerfectDodgeSettleEndTime,
                elapsedTime);
            speedMultiplier = Mathf.SmoothStep(
                PerfectDodgePeakAnimationSpeed,
                1f,
                t);
        }
        else
        {
            speedMultiplier = 1f;
        }

        SetAnimatorSpeed(speedMultiplier);
    }

    private void SetAnimatorSpeed(float multiplier)
    {
        if (player.Animator != null)
            player.Animator.speed = baseAnimatorSpeed * Mathf.Max(0f, multiplier);
    }
}

