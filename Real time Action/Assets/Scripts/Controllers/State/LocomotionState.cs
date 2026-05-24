using UnityEngine;

public class LocomotionState : IPlayerState
{
    private readonly PlayerController player;
    private string currentAnim;

    private const string IDLE = "Avatar_Female_Size02_EllenOnCampus_Ani_Idle_Loop";
    private const string WALK_START = "Avatar_Female_Size02_EllenOnCampus_Ani_Walk_Start";
    private const string WALK_LOOP = "Avatar_Female_Size02_EllenOnCampus_Ani_Walk_Loop";
    private const string WALK_END = "Avatar_Female_Size02_EllenOnCampus_Ani_Walk_End";
    private const string RUN_LOOP = "Avatar_Female_Size02_EllenOnCampus_Ani_Run_Loop";
    private const string RUN_END = "Avatar_Female_Size02_EllenOnCampus_Ani_Run_End";

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        PlayAnimation(IDLE, 0.1f);
    }

    public void Update()
    {
        // 임시로 Locomotion State에서만 에너지 자동회복
        player.RecoveryEnergyOverTime(player.EnergyRecoveryRate);

        Vector3 moveDir = player.GetCameraRelativeMoveDirection();
        bool hasInput = player.MoveInput.sqrMagnitude > 0.0001f;

        player.UpdateSpeed(hasInput);

        if (hasInput)
            player.RotateToward(moveDir);

        Vector3 move = moveDir * player.CurrentSpeed;

        player.HandleGravity();
        move.y = player.YVelocity;

        player.Controller.Move(move * Time.deltaTime);

        UpdateAnimation(hasInput);
    }

    public void Exit()
    {
    }

    #region Handle
    public void HandleAttack()
    {
        player.ChangeState(player.AttackState);
    }

    public void HandleDodge()
    {
        player.ChangeState(player.DodgeState);
    }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        player.ChangeState(player.SkillState);
    }
    public void HandleUltimate()
    {
        if (!player.CanUseUltimate) return;
        player.ChangeState(player.UltimateState);
    }
    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion

    private void UpdateAnimation(bool hasInput)
    {
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (!hasInput)
        {
            if (currentAnim == WALK_START)
            {
                if (info.normalizedTime >= 0.95f)
                    PlayAnimation(WALK_END, 0.08f);
                return;
            }

            if (currentAnim == WALK_LOOP)
            {
                PlayAnimation(WALK_END, 0.08f);
                return;
            }

            if (currentAnim == RUN_LOOP)
            {
                PlayAnimation(RUN_END, 0.08f);
                return;
            }

            if (currentAnim == WALK_END)
            {
                if (info.normalizedTime >= 0.95f)
                    PlayAnimation(IDLE, 0.08f);
                return;
            }

            if (currentAnim != IDLE)
                PlayAnimation(IDLE, 0.08f);

            return;
        }

        // 입력 있음
        if (currentAnim == IDLE || currentAnim == WALK_END)
        {
            PlayAnimation(WALK_START, 0.08f);
            return;
        }

        if (currentAnim == WALK_START)
        {
            if (info.normalizedTime >= 0.95f)
            {
                if (player.CurrentSpeed >= player.RunThreshold)
                    PlayAnimation(RUN_LOOP, 0.08f);
                else
                    PlayAnimation(WALK_LOOP, 0.08f);
            }
            return;
        }

        if (player.CurrentSpeed >= player.RunThreshold)
            PlayAnimation(RUN_LOOP, 0.1f);
        else
            PlayAnimation(WALK_LOOP, 0.1f);
    }

    private void PlayAnimation(string animationName, float fadeTime)
    {
        if (currentAnim == animationName)
            return;

        currentAnim = animationName;
        player.Animator.CrossFade(animationName, fadeTime);
    }
}


