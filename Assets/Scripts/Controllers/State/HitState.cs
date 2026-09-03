using UnityEngine;

/// <summary>
/// 피격 애니메이션이 끝날 때까지 다른 플레이어 입력을 차단하는 강제 상태다.
/// </summary>
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
    }

    public void HandleDodge()
    {
    }

    public void HandleHit()
    {
    }

    public void HandleSkill()
    {
    }
    public void HandleUltimate()
    {
    }

    public void HandleParry()
    {
    }
    #endregion
}


