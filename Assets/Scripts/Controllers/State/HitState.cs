using UnityEngine;

public class HitState : IPlayerState, IUltimateBlockingState
{
    private PlayerController player;
    private float hitDuration = 0.5f;
    private float timer;

    public HitState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("피격 상태 진입");
        timer = hitDuration;
        player.Animator.CrossFade(player.CharacterData.hitLightFrontAnim, 0.05f);
    }
    public void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }
    public void Exit()
    {
        Debug.Log("피격 상태 종료");
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

    public void HandleParry()
    {
        // player.ChangeState(player.SupportState);
    }
    #endregion
}


