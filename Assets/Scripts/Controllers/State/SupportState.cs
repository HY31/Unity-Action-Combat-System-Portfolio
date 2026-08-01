using UnityEngine;

public class SupportState : IPlayerState
{
    private readonly PlayerController player;

    public SupportState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        Debug.Log("지원 상태 진입");
        // 지원 캐릭터가 필드에 들어온 순간부터 모션이 끝날 때까지 피격 상태 전이를 막는다.
        player.SetInvincible(true);
        player.Animator.CrossFade(player.CharacterData.parrySupportStartAnim, 0.05f);
    }

    public void Update()
    {
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);
        float t = info.normalizedTime;

        if (t >= 1f)
        {
            player.SetInvincible(false);
            player.ChangeState(player.LocomotionState);
        }
    }
    public void Exit()
    {
        // 강제 상태 전이로 Update의 정상 종료 지점을 지나쳐도 무적을 반드시 해제한다.
        player.SetInvincible(false);
        Debug.Log("지원 상태 종료");
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
