using UnityEngine;

public class HitState : IPlayerState, IUltimateBlockingState
{
    private PlayerController player;
    private const float LightHitDuration = 0.42f;
    private const float HeavyHitDuration = 0.58f;
    private float timer;

    public HitState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        timer = player.LastHitWasHeavy ? HeavyHitDuration : LightHitDuration;
        player.SetCurrentSpeed(0f);

        string hitAnimation = player.LastHitWasHeavy &&
            !string.IsNullOrEmpty(player.CharacterData.hitHeavyFrontAnim)
            ? player.CharacterData.hitHeavyFrontAnim
            : player.CharacterData.hitLightFrontAnim;

        if (!string.IsNullOrEmpty(hitAnimation))
            player.Animator.CrossFade(hitAnimation, 0.04f);
    }
    public void Update()
    {
        timer -= player.ActionDeltaTime;

        if (timer <= 0f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }
    public void Exit()
    {
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


