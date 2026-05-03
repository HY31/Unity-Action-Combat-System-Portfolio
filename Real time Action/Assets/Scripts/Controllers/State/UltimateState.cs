using UnityEngine;

public class UltimateState : IPlayerState
{
    private readonly PlayerController player;
    private float decibelCost = 3000f;

    public UltimateState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        if(!player.TryUseDecibel(decibelCost))
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        Debug.Log("Ultimate Enter");
    }

    public void Update()
    {

    }

    public void Exit()
    {
    }

    #region Handle
    public void HandleAttack()
    { 

    }
    public void HandleDodge()
    { 

    }
    public void HandleHit()
    {

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



