using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class IdleState : IPlayerState
{
    PlayerController player;

    public IdleState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("Idle Enter");
        player.Animator.CrossFade("Idle", 0.1f);
    }

    public void Update()
    {
        // 이동 처리
        Vector3 move = new Vector3(player.MoveInput.x, 0, player.MoveInput.y);

        player.HandleGravity();

        move.y = player.YVelocity;

        player.Controller.Move(move * player.MoveSpeed * Time.deltaTime);
    }

    public void Exit()
    {
        Debug.Log("Idle Exit");
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
