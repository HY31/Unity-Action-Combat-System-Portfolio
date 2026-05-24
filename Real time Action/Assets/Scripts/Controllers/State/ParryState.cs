using UnityEngine;

public class ParryState : IPlayerState
{
    private readonly PlayerController player;

    private const string PARRY_START = "Avatar_Female_Size02_EllenOnCampus_Ani_Attack_ParryAid_Start";  // 기본 패링인데 이게 있어야되는 이유는 모르겠음
    private const string PARRY_H = "Avatar_Female_Size02_EllenOnCampus_Ani_Attack_ParryAid_H";          // 강공격 패링
    private const string PARRY_H_END = "Avatar_Female_Size02_EllenOnCampus_Ani_Attack_ParryAid_H_END";  
    private const string PARRY_L = "Avatar_Female_Size02_EllenOnCampus_Ani_Attack_ParryAid_L";          // 약공격 패링
    private const string PARRY_L_END = "Avatar_Female_Size02_EllenOnCampus_Ani_Attack_ParryAid_L_END";

    public ParryState(PlayerController player)
    {
        this.player = player;
    }
    public void Enter()
    {
        Debug.Log("Parry State Enter");
        player.SetInvincible(true);
        player.Animator.CrossFade(PARRY_START, 0.05f);
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
        player.SetInvincible(false);
        Debug.Log("Parry State Exit");
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
        // player.ChangeState(player.ParryState);
    }
    #endregion
}
