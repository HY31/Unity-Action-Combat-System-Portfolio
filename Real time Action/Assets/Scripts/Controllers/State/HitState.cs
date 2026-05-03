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
        Debug.Log("피격!!!");

        if (timer <= 0f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }
    public void Exit()
    {
        Debug.Log("Hit Exit");
    }

    #region Handle
    public void HandleAttack()
    {
        // player.ChangeState(player.AttackState);
    }

    public void HandleDodge()
    {
        // player.ChangeState(player.DodgeState);
    }

    public void HandleHit()
    {
        // player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        // player.ChangeState(player.SkillState);
    }
    public void HandleUltimate()
    {
        // player.ChangeState(player.UltimateState);
    }
    #endregion
}


