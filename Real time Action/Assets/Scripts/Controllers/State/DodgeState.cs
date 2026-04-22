using UnityEngine;

public class DodgeState : IPlayerState
{
    private readonly PlayerController player;
    private float dodgeDuration = 0.3f;
    private float timer;
    private Vector3 dodgeDirection;

    // 일단은 전방 회피만
    private const string EVADE = "Avatar_Female_Size02_EllenOnCampus_Ani_Evade_Front";

    public DodgeState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        timer = dodgeDuration;

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
    }

    public void HandleAttack() { }
    public void HandleDodge() { }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        // player.ChangeState(player.SkillState);
    }
}
