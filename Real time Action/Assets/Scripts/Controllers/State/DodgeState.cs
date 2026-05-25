using UnityEngine;

public enum DodgeType
{
    Normal,
    Perfect
}

public class DodgeState : IPlayerState
{
    private readonly PlayerController player;
    private const float NormalDodgeDuration = 0.3f;
    private const float PerfectDodgeDuration = 0.45f;

    private float timer;
    private Vector3 dodgeDirection;
    private DodgeType dodgeType = DodgeType.Normal;

    // 일단은 전방 회피만
    private const string EVADE = "Avatar_Female_Size02_EllenOnCampus_Ani_Evade_Front";

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
        timer = dodgeType == DodgeType.Perfect ? PerfectDodgeDuration : NormalDodgeDuration;
        player.SetInvincible(true);

        Vector3 inputDir = player.GetCameraRelativeMoveDirection();
        dodgeDirection = inputDir.sqrMagnitude > 0.0001f ? inputDir : player.transform.forward;

        player.RotateToward(dodgeDirection);
        player.Animator.CrossFade(EVADE, 0.05f);
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        player.HandleGravity();

        Vector3 move = dodgeDirection * player.DodgeSpeed;
        move.y = player.YVelocity;

        player.Controller.Move(move * Time.deltaTime);

        if (timer <= 0f)
            player.ChangeState(player.LocomotionState);
    }

    public void Exit()
    {
        player.SetInvincible(false);
        dodgeType = DodgeType.Normal;
    }

    #region Handle
    public void HandleAttack()
    {

    }

    public void HandleDodge() { }

    public void HandleHit()
    {
        if (!player.IsInvincible)
            player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        // player.ChangeState(player.SkillState);
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
}

