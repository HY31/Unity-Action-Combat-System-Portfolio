using UnityEngine;

public class HitState : IPlayerState
{
    private PlayerController player;
    float hitDuration = 0.5f;
    float timer;

    public HitState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("Hit Enter");
        timer = hitDuration;
    }
    public void Update()
    {
        timer -= Time.deltaTime;
        Debug.Log("ÇÇ°Ý!!!");

        if (timer <= 0f)
        {
            player.ChangeState(player.IdleState);
        }
    }
    public void Exit()
    {
        Debug.Log("Hit Exit");
    }
}
