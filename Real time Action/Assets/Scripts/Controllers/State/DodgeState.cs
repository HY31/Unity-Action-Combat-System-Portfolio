using UnityEngine;

public class DodgeState : IPlayerState
{
    private PlayerController player;
    private float dodgeDuration = 0.3f;
    private float timer;
    private Vector3 dodgeDirection;

    private float dodgeCount = 2;

    public DodgeState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("Dodge Enter");
        timer = dodgeDuration;
        dodgeDirection = player.transform.forward;
    }
    public void Update()
    {
        Debug.Log("회피!!");

        timer -= Time.deltaTime;

        player.Controller.Move(dodgeDirection * player.DodgeSpeed * Time.deltaTime);

        if (timer < 0)
        {
            player.ChangeState(player.IdleState);
        }
    }
    public void Exit()
    {
        Debug.Log("Dodge Exit");
    }

    public void HandleAttack()
    {
        // 회피 반격
    }

    public void HandleDodge()
    {
        // 연속 회피(회피 카운트 추가해서 넣기(기본 2회)
        
    }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }
}
