using UnityEngine;

public class DodgeState : IPlayerState
{
    private PlayerController player;
    private float dodgeDuration = 0.3f;
    private float timer;
    private Vector3 dodgeDirection;

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
        Debug.Log("È¸ÇÇ!!");

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
}
