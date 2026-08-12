using UnityEngine;

public class AttackState : IPlayerState
{
    private enum AttackPhase
    {
        Attack,
        End
    }

    private readonly PlayerController player;

    private AttackData currentAttack;
    private AttackPhase phase;

    private int comboIndex;

    // 현재 공격 중 입력된 다음 평타를 콤보 허용 시점까지 한 번만 예약한다.
    private bool bufferedAttackInput;

    // 평타 중 들어온 스킬 입력을 해당 공격의 스킬 캔슬 허용 시점까지 보관한다.
    private bool bufferedSkillInput;

    // 평타 중 들어온 회피 입력을 해당 공격의 회피 캔슬 허용 시점까지 보관한다.
    private bool bufferedDodgeInput;

    private bool hitboxActive;
    private HitBox hitBox;

    private Transform assistTarget;
    private Vector3 attackAssistDirection;
    private bool hasAttackAssist;
    private float previousMovementTime;
    private float baseAnimatorSpeed = 1f;
    private bool attackSwingPlayed;

    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        baseAnimatorSpeed = player.Animator != null ? player.Animator.speed : 1f;
        comboIndex = 0;
        bufferedAttackInput = false;
        bufferedSkillInput = false;
        bufferedDodgeInput = false;
        hitboxActive = false;
        previousMovementTime = 0f;
        ClearAttackAssist();

        if (player.CharacterData == null
            || player.CharacterData.normalCombo == null
            || player.CharacterData.normalCombo.Length == 0)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        hitBox = player.AttackHitBox;

        StartAttack(player.CharacterData.normalCombo[comboIndex]);
    }

    public void Update()
    {
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (phase == AttackPhase.Attack)
        {
            UpdateAttackPhase(info);
        }
        else if (phase == AttackPhase.End)
        {
            UpdateEndPhase(info);
        }
    }

    public void Exit()
    {
        SetHitBoxActive(false);
        if (player.Animator != null)
            player.Animator.speed = baseAnimatorSpeed;

        bufferedAttackInput = false;
        bufferedSkillInput = false;
        bufferedDodgeInput = false;
        hitboxActive = false;
        previousMovementTime = 0f;
        ClearAttackAssist();
    }

    #region Handle
    public void HandleAttack()
    {
        // 현재 공격이 끝나기 전에 들어온 평타를 다음 콤보 1회로 예약한다.
        bufferedAttackInput = true;
    }

    public void HandleDodge()
    {
        // 입력 순간 PlayerController가 결정한 일반/극한 회피 타입과 함께 회피 실행을 예약한다.
        bufferedDodgeInput = true;
    }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }

    public void HandleSkill()
    {
        // 캔슬 허용 시점보다 일찍 들어온 스킬 입력도 한 번 예약한다.
        bufferedSkillInput = true;
    }
    public void HandleUltimate()
    {
    }

    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion

    private void StartAttack(AttackData attackData)
    {
        currentAttack = attackData;
        phase = AttackPhase.Attack;
        previousMovementTime = 0f;
        attackSwingPlayed = false;

        if (hitBox == null)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        // 캐릭터 기본 속성과 공격별 override를 여기서 확정해 이후 충돌 계층은 계산 없이 전달만 한다.
        CombatElement resolvedElement =
            currentAttack.hitPayload.elementOverride == CombatElement.None
            ? player.CharacterData.Element
            : currentAttack.hitPayload.elementOverride;

        CombatHitData hitData = new CombatHitData
        {
            attacker = player,
            damageMultiplier = currentAttack.hitPayload.damageMultiplier,
            impactMultiplier = currentAttack.hitPayload.impactMultiplier,
            hitReactionBuildUp = currentAttack.hitPayload.hitReactionBuildUp,
            resolvedElement = resolvedElement,
            anomalyBuildUp = currentAttack.hitPayload.anomalyBuildUp,
            canTriggerChainSkill = currentAttack.hitPayload.canTriggerChainSkill
        };

        hitBox.SetRewardType(DecibelRewardType.NormalAttack);
        hitBox.SetHitData(hitData);
        hitBox.SetFeedback(currentAttack.hitFeedback);
        hitBox.ConfigureShape(currentAttack.hitBoxShape);
        SetHitBoxActive(false);
        ResolveAttackAssist();

        float playbackSpeed = currentAttack.playbackSpeed > 0f
            ? currentAttack.playbackSpeed
            : 1f;
        player.Animator.speed = baseAnimatorSpeed * playbackSpeed;
        player.Animator.CrossFade(currentAttack.attackAnim, 0.05f);
    }

    private void UpdateAttackPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        UpdateAttackSwing(t);
        UpdateAttackAssist(t);

        // 루트 모션 대신 애니메이션 정규화 시간에 맞춰 결정론적으로 전진시킨다.
        ApplyForwardMovement(t);

        // 애니메이션 정규화 시간과 실제 타격 판정의 활성 프레임을 동기화한다.
        bool shouldHitBoxBeActive = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        SetHitBoxActive(shouldHitBoxBeActive);

        // 회피는 AttackState 내부에서 가장 우선순위가 높은 캔슬 행동이다.
        if (TryCancelToDodge(t))
            return;

        // 스킬 예약이 실행되면 현재 AttackState가 종료되므로 이전 공격의 후속 처리를 중단한다.
        if (TryCancelToSkill(t))
            return;

        // 다음 공격이 시작됐다면 이전 공격의 시간축 처리를 같은 프레임에 계속하지 않는다.
        if (t >= currentAttack.comboInputOpenTime && TryChainCombo())
            return;

        // 콤보 예약이 없고 이동 허용 시점에 도달했다면 후속 모션을 이동으로 취소한다.
        if (TryCancelToLocomotion(t))
            return;

        // 본 공격이 끝나면 별도 회복 모션으로 넘어간다.
        if (t >= currentAttack.endTransitionTime)
        {
            SetHitBoxActive(false);
            phase = AttackPhase.End;
            player.Animator.CrossFade(currentAttack.endAnim, 0.05f);
        }
    }

    private void UpdateAttackSwing(float normalizedTime)
    {
        if (attackSwingPlayed || currentAttack == null)
            return;

        float swingTime = Mathf.Max(0f, currentAttack.startUpEnd - 0.12f);
        if (normalizedTime < swingTime)
            return;

        attackSwingPlayed = true;
        CombatAudio.PlayAttackSwing(currentAttack.hitPayload.impactMultiplier);
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        // 공격 본체가 끝났으므로 모든 일반 캔슬 시간은 통과한 것으로 취급한다.
        if (TryCancelToDodge(1f))
            return;

        if (TryCancelToSkill(1f))
            return;

        if (TryCancelToLocomotion(1f))
            return;

        if (!info.IsName(currentAttack.endAnim))
            return;

        if (info.normalizedTime >= currentAttack.locomotionRecoverTime)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private bool TryCancelToDodge(float normalizedTime)
    {
        if (!bufferedDodgeInput)
            return false;

        if (normalizedTime < currentAttack.dodgeCancelOpenTime)
            return false;

        bufferedDodgeInput = false;

        player.ChangeState(player.DodgeState);
        return true;

    }

    private bool TryCancelToSkill(float normalizedTime)
    {
        if (!bufferedSkillInput)
            return false;

        if (normalizedTime < currentAttack.skillCancelOpenTime)
            return false;

        // 같은 예약이 이후 프레임에서 다시 실행되지 않도록 상태 전환 전에 소비한다.
        bufferedSkillInput = false;

        player.ChangeState(player.SkillState);
        return true;
    }

    private bool HasValidBufferedCombo()
    {
        if (!bufferedAttackInput)
            return false;

        if (player.CharacterData.normalCombo == null)
            return false;

        int nextIndex = currentAttack.nextComboIndex;

        return nextIndex >= 0 &&
            nextIndex < player.CharacterData.normalCombo.Length;
    }

    private bool TryCancelToLocomotion(float normalizedTime)
    {
        if (normalizedTime < currentAttack.locomotionCancelOpenTime)
            return false;

        // 다음 평타가 유효하게 예약되어 있다면 이동보다 콤보를 우선한다.
        if (HasValidBufferedCombo())
            return false;

        // 이동은 지속 입력이므로 별도의 버튼 버퍼를 만들지 않고 현재 입력을 확인한다.
        if (player.MoveInput.sqrMagnitude <= 0.0001f)
            return false;

        player.ChangeState(player.LocomotionState);
        return true;
    }

    private bool TryChainCombo()
    {
        if (!bufferedAttackInput)
            return false;

        // 예약은 다음 콤보의 존재 여부와 관계없이 한 번만 소비한다.
        bufferedAttackInput = false;

        int nextIndex = currentAttack.nextComboIndex;

        if (nextIndex < 0 || nextIndex >= player.CharacterData.normalCombo.Length)
            return false;

        comboIndex = nextIndex;
        StartAttack(player.CharacterData.normalCombo[comboIndex]);
        return true;
    }

    private void SetHitBoxActive(bool active)
    {
        if (hitBox == null)
            return;

        if (hitboxActive == active)
            return;

        hitboxActive = active;
        hitBox.SetActive(active);
    }

    private void ResolveAttackAssist()
    {
        ClearAttackAssist();

        if (!currentAttack.useAutoAim)
            return;

        // 공격 시작 시 고른 대상을 상태가 보유해 프레임마다 다른 적으로 튀지 않게 한다.
        assistTarget = player.FindAttackTarget(
            currentAttack.autoAimRadius,
            currentAttack.autoAimMaxAngle);

        if (assistTarget == null)
            return;

        attackAssistDirection = player.GetAttackAssistDirection(assistTarget);

        if (attackAssistDirection.sqrMagnitude < 0.0001f)
        {
            ClearAttackAssist();
            return;
        }

        hasAttackAssist = true;
        player.RotateToward(attackAssistDirection, currentAttack.autoAimRotationMultiplier);
    }

    private void UpdateAttackAssist(float normalizedTime)
    {
        if (!hasAttackAssist)
            return;

        if (assistTarget == null)
        {
            ClearAttackAssist();
            return;
        }

        Vector3 direction = player.GetAttackAssistDirection(assistTarget);

        if (direction.sqrMagnitude > 0.0001f)
            attackAssistDirection = direction;

        if (normalizedTime <= currentAttack.autoAimRotateUntil)
            player.RotateToward(attackAssistDirection, currentAttack.autoAimRotationMultiplier);
    }

    private Vector3 ResolveAttackMoveDirection()
    {
        if (!currentAttack.steerMoveToTarget || !hasAttackAssist)
            return player.transform.forward;

        if (attackAssistDirection.sqrMagnitude < 0.0001f)
            return player.transform.forward;

        return attackAssistDirection;
    }

    private void ApplyForwardMovement(float normalizedTime)
    {
        float currentTime = Mathf.Clamp01(normalizedTime);
        Vector3 moveDirection = ResolveAttackMoveDirection();

        if (currentAttack.useDistanceBasedMovement)
        {
            float previousProgress = EvaluateMovementProgress(
                previousMovementTime,
                currentAttack.moveStart,
                currentAttack.moveEnd);
            float currentProgress = EvaluateMovementProgress(
                currentTime,
                currentAttack.moveStart,
                currentAttack.moveEnd);
            float progressDelta = Mathf.Max(0f, currentProgress - previousProgress);

            if (progressDelta > 0f)
            {
                float moveDistance = ClampMoveDistanceToTarget(
                    currentAttack.forwardMoveDistance * progressDelta,
                    moveDirection);

                player.Controller.Move(
                    moveDirection * moveDistance);
            }
        }
        else if (currentTime >= currentAttack.moveStart && currentTime <= currentAttack.moveEnd)
        {
            float moveDistance = ClampMoveDistanceToTarget(
                currentAttack.forwardMoveSpeed * player.ActionDeltaTime,
                moveDirection);

            player.Controller.Move(
                moveDirection * moveDistance);
        }

        previousMovementTime = Mathf.Max(previousMovementTime, currentTime);
    }

    private static float EvaluateMovementProgress(float normalizedTime, float start, float end)
    {
        if (end <= start)
            return normalizedTime >= end ? 1f : 0f;

        return Mathf.InverseLerp(start, end, normalizedTime);
    }

    private float ClampMoveDistanceToTarget(float requestedDistance, Vector3 moveDirection)
    {
        if (requestedDistance <= 0f || !hasAttackAssist || assistTarget == null)
            return Mathf.Max(0f, requestedDistance);

        Vector3 toTarget = assistTarget.position - player.transform.position;
        toTarget.y = 0f;

        float distanceAlongMove = Vector3.Dot(toTarget, moveDirection);
        float remainingDistance = distanceAlongMove - currentAttack.autoAimStopDistance;

        return Mathf.Clamp(requestedDistance, 0f, Mathf.Max(0f, remainingDistance));
    }

    private void ClearAttackAssist()
    {
        assistTarget = null;
        attackAssistDirection = Vector3.zero;
        hasAttackAssist = false;
    }
}


