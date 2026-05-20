using Unity.VisualScripting;
using UnityEngine;

public class UltimateState : IPlayerState
{
    private enum UltimatePhase
    {
        Start,
        Hit,
        End
    }

    private UltimateData ultData;

    private readonly PlayerController player;

    private int currentHitWindowIndex = -1;

    private Transform assistTarget;
    private Vector3 ultAssistDirection;

    private UltimatePhase phase;

    public UltimateState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        if (ultData == null)
        {
            ultData = player.UltimateData;
        }

        if (!player.TryUseDecibel(ultData.decibelCost))
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        assistTarget = player.FindAttackTarget(ultData.autoAimRadius, ultData.autoAimMaxAngle);
        ultAssistDirection = player.GetAttackAssistDirection(assistTarget);

        player.SetInvincible(true);
        phase = UltimatePhase.Start;
        player.Animator.CrossFade(ultData.ultStartAnim, 0.05f);
        Debug.Log("Ultimate Enter");
    }

    public void Update()
    {
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (phase == UltimatePhase.Start)
        {
            UpdateStartPhase(info);
        }
        else if(phase == UltimatePhase.Hit)
        {
            UpdateHitPhase(info);
        }
        else if (phase == UltimatePhase.End)
        {
            UpdateEndPhase(info);
        }
    }

    private void UpdateStartPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(ultData.ultStartAnim))
            return;

        float t = info.normalizedTime;
        Vector3 moveDirection;

        if (ultAssistDirection.sqrMagnitude > 0.0001f)
            player.RotateToward(ultAssistDirection, ultData.autoAimRotationMultiplier);

        if (t >= ultData.moveStart && t < ultData.moveEnd)
        {
            if (ultAssistDirection.sqrMagnitude > 0.0001f && assistTarget != null)
                moveDirection = ultAssistDirection;
            else
                moveDirection = player.transform.forward;

            Vector3 forwardMove = moveDirection * ultData.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }
        // TODO : 나중에 컷씬 영상 넣을 것

        if (t >= 1f)
        {
            if (player.UltHitBox != null)
                player.UltHitBox.SetActive(false);

            phase = UltimatePhase.Hit;
            player.Animator.CrossFade(ultData.ultHitAnim, 0.05f);
        }
    }

    private void UpdateHitPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(ultData.ultHitAnim))
            return;

        float t = info.normalizedTime;

        int detectedWindowIndex = -1;

        for (int i = 0; i < ultData.hitWindows.Length; i++)
        {
            if (t >= ultData.hitWindows[i].start && t < ultData.hitWindows[i].end)
            {
                detectedWindowIndex = i;
                break;
            }
        }

        bool shouldHit = detectedWindowIndex != -1;

        if (detectedWindowIndex != currentHitWindowIndex)
        {
            currentHitWindowIndex = detectedWindowIndex;
            // 나중에 여기서 "새 윈도우 진입 시 히트 기록 초기화"
        }

        if (player.UltHitBox != null)
            player.UltHitBox.SetActive(shouldHit);

        if (t >= 1f)
        {
            if (player.UltHitBox != null)
                player.UltHitBox.SetActive(false);

            currentHitWindowIndex = -1;
            phase = UltimatePhase.End;
            player.Animator.CrossFade(ultData.ultEndAnim, 0.05f);
        }
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(ultData.ultEndAnim))
            return;

        if (info.normalizedTime >= 1f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    public void Exit()
    {
        player.SetInvincible(false);

        if (player.UltHitBox == null)
            return;
        
        player.UltHitBox.SetActive(false);
        Debug.Log("Ultimate Exit");
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
    #endregion
}



