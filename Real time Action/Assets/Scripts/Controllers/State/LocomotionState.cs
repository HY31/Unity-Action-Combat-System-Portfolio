using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LocomotionState : IPlayerState
{
    PlayerController player;

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("Locomotion Enter");
        player.Animator.CrossFade("Avatar_Female_Size02_EllenOnCampus_Ani_Idle_Loop", 0.1f);
    }

    public void Update()
    {
        Vector3 move = player.GetCameraRelativeMoveDirection();

        player.RotateToward(move);

        player.UpdateSpeed(move.magnitude > 0.1f);

        player.HandleGravity();
        move.y = player.YVelocity;

        player.Controller.Move(move * player.CurrentSpeed * Time.deltaTime);

        player.Animator.SetFloat("MoveSpeed", player.CurrentSpeed);

        player.Animator.CrossFade("Avatar_Female_Size02_EllenOnCampus_Ani_Idle_Loop", 0.1f);
    }

    public void Exit()
    {
        Debug.Log("Locomotion Exit");
    }
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
}
