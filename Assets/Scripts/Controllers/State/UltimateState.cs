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
        if (player.CharacterData == null || player.CharacterData.ultimateData == null)
        {
            Debug.LogError("ultState : character data is missing.");
            player.ChangeState(player.LocomotionState);
            return;
        }

        if (ultData == null)
        {
            ultData = player.CharacterData.ultimateData;
        }

        if (!player.TryUseDecibel(ultData.decibelCost))
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        // 궁극기 시작 시 공격자와 최종 속성을 묶어 모든 다단 히트 구간에서 같은 데이터를 사용한다.
        CombatElement resolvedElement =
            ultData.hitPayload.elementOverride == CombatElement.None
            ? player.CharacterData.Element
            : ultData.hitPayload.elementOverride;

        CombatHitData hitData = new CombatHitData
        {
            attacker = player,
            damageMultiplier = ultData.hitPayload.damageMultiplier,
            impactMultiplier = ultData.hitPayload.impactMultiplier,
            hitReactionBuildUp = ultData.hitPayload.hitReactionBuildUp,
            resolvedElement = resolvedElement,
            anomalyBuildUp = ultData.hitPayload.anomalyBuildUp,
            canTriggerChainSkill = ultData.hitPayload.canTriggerChainSkill

        };

        if (player.UltHitBox != null)
        {
            player.UltHitBox.SetHitData(hitData);
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

        // 컷신 뒤에 순간 접근하지 않도록 Start 단계에서 타겟 방향 접근을 끝낸다.
        if (t >= ultData.moveStart && t < ultData.moveEnd)
        {
            if (ultAssistDirection.sqrMagnitude > 0.0001f && assistTarget != null)
                moveDirection = ultAssistDirection;
            else
                moveDirection = player.transform.forward;

            Vector3 forwardMove = moveDirection * ultData.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }
        // TODO: 컷신이 연결되면 Start 구간의 접근 이동과 함께 끝나도록 동기화한다.

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

        // 현재 시간이 속한 윈도우를 찾아 구간 사이마다 HitBox가 꺼졌다 다시 켜지게 한다.
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
            // 이후 한 대상당 한 번만 맞히는 목록을 추가하면 새 윈도우 진입 시 여기서 초기화한다.
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
        // 어떤 경로로 상태를 빠져나가도 무적과 타격 판정이 남지 않게 정리한다.
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
    public void HandleParry()
    {
        // player.ChangeState(player.SupportState);
    }
    #endregion
}



