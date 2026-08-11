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
    private HitBox hitBox;

    private readonly PlayerController player;

    private int currentHitWindowIndex = -1;

    private Transform assistTarget;
    private Vector3 ultAssistDirection;
    private float previousMovementTime;

    private UltimatePhase phase;
    private bool chainSkillPending;
    private bool isChainSkillEntry;
    private bool ultimatePresentationStarted;
    private Transform requestedAssistTarget;

    public UltimateState(PlayerController player)
    {
        this.player = player;
    }

    public void PrepareChainSkill(Transform target)
    {
        chainSkillPending = true;
        requestedAssistTarget = target;
    }

    public void Enter()
    {
        if (player.CharacterData == null || player.CharacterData.ultimateData == null)
        {
            Debug.LogError("궁극기 상태: 캐릭터 데이터가 없습니다.");
            player.ChangeState(player.LocomotionState);
            return;
        }

        if (ultData == null)
        {
            ultData = player.CharacterData.ultimateData;
        }

        hitBox = player.AttackHitBox;
        if (hitBox == null)
        {
            Debug.LogError("궁극기 상태: 공용 공격 히트박스가 없습니다.", player);
            player.ChangeState(player.LocomotionState);
            return;
        }

        isChainSkillEntry = chainSkillPending;
        chainSkillPending = false;

        if (!isChainSkillEntry && !player.TryUseDecibel(ultData.decibelCost))
        {
            requestedAssistTarget = null;
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

        hitBox.SetRewardType(DecibelRewardType.None);
        hitBox.SetHitData(hitData);
        hitBox.SetFeedback(ultData.hitFeedback);
        hitBox.ConfigureShape(ultData.hitBoxShape);
        hitBox.SetActive(false);

        assistTarget = requestedAssistTarget != null
            ? requestedAssistTarget
            : player.FindAttackTarget(ultData.autoAimRadius, ultData.autoAimMaxAngle);
        requestedAssistTarget = null;
        ultAssistDirection = player.GetAttackAssistDirection(assistTarget);

        ultimatePresentationStarted = !isChainSkillEntry;
        if (ultimatePresentationStarted)
            CombatPresentationEffects.BeginUltimate(resolvedElement);
        player.SetInvincible(true);
        previousMovementTime = 0f;
        currentHitWindowIndex = -1;
        phase = UltimatePhase.Start;
        player.Animator.CrossFade(ultData.ultStartAnim, 0.05f);
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

        // 컷신 뒤에 순간 접근하지 않도록 시작 단계에서 대상 방향 접근을 끝낸다.
        float currentTime = Mathf.Clamp01(t);
        if (ultData.useDistanceBasedMovement)
        {
            if (ultAssistDirection.sqrMagnitude > 0.0001f && assistTarget != null)
                moveDirection = ultAssistDirection;
            else
                moveDirection = player.transform.forward;

            float previousProgress = EvaluateMovementProgress(
                previousMovementTime,
                ultData.moveStart,
                ultData.moveEnd);
            float currentProgress = EvaluateMovementProgress(
                currentTime,
                ultData.moveStart,
                ultData.moveEnd);
            float progressDelta = Mathf.Max(0f, currentProgress - previousProgress);

            if (progressDelta > 0f)
            {
                float moveDistance = ClampMoveDistanceToTarget(
                    ultData.forwardMoveDistance * progressDelta,
                    moveDirection);
                player.Controller.Move(moveDirection * moveDistance);
            }
        }
        else if (currentTime >= ultData.moveStart && currentTime < ultData.moveEnd)
        {
            if (ultAssistDirection.sqrMagnitude > 0.0001f && assistTarget != null)
                moveDirection = ultAssistDirection;
            else
                moveDirection = player.transform.forward;

            float moveDistance = ClampMoveDistanceToTarget(
                ultData.forwardMoveSpeed * Time.deltaTime,
                moveDirection);
            player.Controller.Move(moveDirection * moveDistance);
        }

        previousMovementTime = Mathf.Max(previousMovementTime, currentTime);
        // TODO: 컷신이 연결되면 시작 구간의 접근 이동과 함께 끝나도록 동기화한다.

        if (t >= 1f)
        {
            hitBox.SetActive(false);

            phase = UltimatePhase.Hit;
            player.Animator.CrossFade(ultData.ultHitAnim, 0.05f);
        }
    }

    private static float EvaluateMovementProgress(float normalizedTime, float start, float end)
    {
        if (end <= start)
            return normalizedTime >= end ? 1f : 0f;

        return Mathf.InverseLerp(start, end, normalizedTime);
    }

    private float ClampMoveDistanceToTarget(float requestedDistance, Vector3 moveDirection)
    {
        if (requestedDistance <= 0f || assistTarget == null)
            return Mathf.Max(0f, requestedDistance);

        Vector3 toTarget = assistTarget.position - player.transform.position;
        toTarget.y = 0f;

        float distanceAlongMove = Vector3.Dot(toTarget, moveDirection);
        float remainingDistance = distanceAlongMove - ultData.autoAimStopDistance;

        return Mathf.Clamp(requestedDistance, 0f, Mathf.Max(0f, remainingDistance));
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

            if (currentHitWindowIndex >= 0)
                CombatAudio.PlayAttackSwing(1.35f);

            // 이후 한 대상당 한 번만 맞히는 목록을 추가하면 새 윈도우 진입 시 여기서 초기화한다.
        }

        hitBox.SetActive(shouldHit);

        if (t >= 1f)
        {
            hitBox.SetActive(false);

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
        if (ultimatePresentationStarted)
            CombatPresentationEffects.EndUltimate();

        ultimatePresentationStarted = false;
        isChainSkillEntry = false;
        chainSkillPending = false;
        requestedAssistTarget = null;
        player.SetInvincible(false);

        hitBox?.SetActive(false);
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



