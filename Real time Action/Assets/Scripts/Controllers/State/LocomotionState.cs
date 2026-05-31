using UnityEngine;

public class LocomotionState : IPlayerState
{
    private readonly PlayerController player;
    private CharacterData characterData;
    private string currentAnim;

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        characterData = player.CharacterData;
        PlayAnimation(characterData.idleAnim, 0.1f);
    }

    public void Update()
    {
        // 임시로 Locomotion State에서만 에너지 자동회복
        player.RecoveryEnergyOverTime(player.EnergyRecoveryRate);

        Vector3 moveDir = player.GetCameraRelativeMoveDirection();
        bool hasInput = player.MoveInput.sqrMagnitude > 0.0001f;

        player.UpdateSpeed(hasInput);

        if (hasInput)
            player.RotateToward(moveDir);

        Vector3 move = moveDir * player.CurrentSpeed;

        player.HandleGravity();
        move.y = player.YVelocity;

        player.Controller.Move(move * Time.deltaTime);

        UpdateAnimation(hasInput);
    }

    public void Exit()
    {
    }

    #region Handle
    public void HandleAttack()
    {
        player.ChangeState(player.AttackState);
    }

    public void HandleDodge()
    {
        player.ChangeState(player.DodgeState);
    }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        player.ChangeState(player.SkillState);
    }
    public void HandleUltimate()
    {
        if (!player.CanUseUltimate) return;
        player.ChangeState(player.UltimateState);
    }
    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion

    private void UpdateAnimation(bool hasInput)
    {
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (!hasInput)
        {
            if (currentAnim == characterData.walkStartAnim)
            {
                if (info.normalizedTime >= 0.95f)
                    PlayAnimation(characterData.walkEndAnim, 0.08f);
                return;
            }

            if (currentAnim == characterData.walkLoopAnim)
            {
                PlayAnimation(characterData.walkEndAnim, 0.08f);
                return;
            }

            if (currentAnim == characterData.runLoopAnim)
            {
                PlayAnimation(characterData.runEndAnim, 0.08f);
                return;
            }

            if (currentAnim == characterData.walkEndAnim)
            {
                if (info.normalizedTime >= 0.95f)
                    PlayAnimation(characterData.idleAnim, 0.08f);
                return;
            }

            if (currentAnim != characterData.idleAnim)
                PlayAnimation(characterData.idleAnim, 0.08f);

            return;
        }

        // 입력 있음
        if (currentAnim == characterData.idleAnim || currentAnim == characterData.walkEndAnim)
        {
            PlayAnimation(characterData.walkStartAnim, 0.08f);
            return;
        }

        if (currentAnim == characterData.walkStartAnim)
        {
            if (info.normalizedTime >= 0.95f)
            {
                if (player.CurrentSpeed >= player.RunThreshold)
                    PlayAnimation(characterData.runLoopAnim, 0.08f);
                else
                    PlayAnimation(characterData.walkLoopAnim, 0.08f);
            }
            return;
        }

        if (player.CurrentSpeed >= player.RunThreshold)
            PlayAnimation(characterData.runLoopAnim, 0.1f);
        else
            PlayAnimation(characterData.walkLoopAnim, 0.1f);
    }

    private void PlayAnimation(string animationName, float fadeTime)
    {
        if (currentAnim == animationName)
            return;

        currentAnim = animationName;
        player.Animator.CrossFade(animationName, fadeTime);
    }
}


