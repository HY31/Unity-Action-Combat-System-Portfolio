using UnityEngine;

public enum DodgeType
{
    Normal,
    Perfect
}

public class DodgeState : IPlayerState
{
    private readonly PlayerController player;
    private CharacterData characterData;
    private const float NormalDodgeDuration = 0.3f;
    private const float PerfectDodgeDuration = 0.45f;

    private float timer;
    private Vector3 dodgeDirection;
    private DodgeType dodgeType = DodgeType.Normal;

    private float elapsedTime;

    private bool bufferedAttackInput;
    private float bufferedAttackTimer;

    private bool bufferedSkillInput;
    private float bufferedSkillTimer;

    private const float InputBufferDuration = 0.2f;

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
        ResetFollowUpInputs();

        // 회피 타입별 유효 시간 동안만 PlayerController의 공통 피격 진입점을 차단한다.
        timer = dodgeType == DodgeType.Perfect ? PerfectDodgeDuration : NormalDodgeDuration;
        player.SetInvincible(true);

        Vector3 inputDir = player.GetCameraRelativeMoveDirection();
        dodgeDirection = inputDir.sqrMagnitude > 0.0001f ? inputDir : player.transform.forward;

        player.RotateToward(dodgeDirection);
        player.Animator.CrossFade(characterData.dodgeFrontAnim, 0.05f);

        if (dodgeType == DodgeType.Perfect)
            CombatPresentationEffects.PlayPerfectDodge();
    }

    public void Update()
    {
        float deltaTime = Time.deltaTime;

        timer -= deltaTime;
        elapsedTime += deltaTime;

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
        // player.ChangeState(player.UltimateState);
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

        if (elapsedTime < characterData.dodgeAttackCancelTime)
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

        if (elapsedTime < characterData.dodgeSkillCancelTime)
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
            bufferedAttackTimer -= Time.deltaTime;

            if (bufferedAttackTimer <= 0f)
            {
                bufferedAttackInput = false;
                bufferedAttackTimer = 0f;
            }
        }

        if (bufferedSkillInput)
        {
            bufferedSkillTimer -= Time.deltaTime;

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
}

