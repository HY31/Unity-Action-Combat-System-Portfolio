using UnityEngine;

public class AttackState : IPlayerState
{
    private PlayerController player;
    private float attackDuration = 0.5f;
    private float timer;

    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("Attack Enter");
        timer = attackDuration;
    }

    public void Update()
    {
        timer -= Time.deltaTime;
        if(timer < 0)
        {
            player.ChangeState(player.IdleState);
        }
    }

    public void Exit()
    {
        Debug.Log("Attack Exit");
    }
}
