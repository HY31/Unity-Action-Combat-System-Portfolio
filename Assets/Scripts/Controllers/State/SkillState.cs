using UnityEngine;

public class SkillState : IPlayerState
{
    private enum SkillPhase
    {
        Attack,
        End
    }

    private readonly PlayerController player;

    private SkillData currentSkill;
    private SkillPhase phase;

    private bool bufferedSkillInput;
    private float bufferedSkillTimer;
    private const float BufferDuration = 0.2f;

    private bool skillHitboxActive;
    private HitBox skillHitBox;

    private Transform assistTarget;
    private Vector3 attackAssistDirection;
    private bool hasAttackAssist;
    private float previousMovementTime;

    public SkillState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        ResetRuntimeFlags();

        SkillData entrySkill = ResolveEntrySkill();
        if (entrySkill == null)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        if(!TryStartSkill(entrySkill))
        {
            player.ChangeState(player.LocomotionState);
            return;
        }
    }

    private void ResetRuntimeFlags()
    {
        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
        skillHitboxActive = false;
        previousMovementTime = 0f;

        ClearAttackAssist();
    }

    public void Update()
    {
        UpdateInputBuffer();

        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (phase == SkillPhase.Attack)
        {
            UpdateSkilllPhase(info);
        }
        else if (phase == SkillPhase.End)
        {
            UpdateEndPhase(info);
        }
    }

    public void Exit()
    {
        SetHitBoxActive(false);
        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
        skillHitboxActive = false;
        previousMovementTime = 0f;
        ClearAttackAssist();
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
        bufferedSkillInput = true;
        bufferedSkillTimer = BufferDuration;
    }
    public void HandleUltimate()
    {
        // player.ChangeState(player.UltimateState);
    }
    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion

    private void UpdateSkilllPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentSkill.skillAnim))
            return;

        float t = info.normalizedTime;

        UpdateAttackAssist(t);

        // 루트 모션 대신 애니메이션 정규화 시간에 맞춰 결정론적으로 전진시킨다.
        ApplyForwardMovement(t);

        // 스킬 데이터의 활성 구간에만 선택된 슬롯의 HitBox를 켠다.
        bool shouldHitBoxBeActive = t >= currentSkill.hitStart && t < currentSkill.hitEnd;
        SetHitBoxActive(shouldHitBoxBeActive);

        // 입력 버퍼와 체인 허용 시점을 함께 만족할 때만 다음 스킬을 시도한다.
        if (t >= currentSkill.chainInputOpenTime)
        {
            TryChainSkill();
        }

        // 공격 모션과 회복 모션을 분리해 모션 종료 전에도 설정된 시점부터 이동으로 복귀시킨다.
        if (t >= currentSkill.endTransitionTime)
        {
            SetHitBoxActive(false);
            phase = SkillPhase.End;
            player.Animator.CrossFade(currentSkill.endAnim, 0.05f);
        }
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentSkill.endAnim))
            return;

        if (info.normalizedTime >= currentSkill.locomotionRecoverTime)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private bool TryStartSkill(SkillData skill)
    {
        if (skill == null)
            return false;

        if (!HasAnimatorState(skill.skillAnim))
        {
            Debug.LogError(
                $"스킬 애니메이션 상태 '{skill.skillAnim}'을(를) " +
                $"'{player.Animator.runtimeAnimatorController?.name}'에서 찾을 수 없습니다.",
                player);
            return false;
        }

        if (!HasAnimatorState(skill.endAnim))
        {
            Debug.LogError(
                $"스킬 종료 애니메이션 상태 '{skill.endAnim}'을(를) " +
                $"'{player.Animator.runtimeAnimatorController?.name}'에서 찾을 수 없습니다.",
                player);
            return false;
        }

        skillHitBox = player.GetSkillHitBox(skill.hitBoxSlotIndex);

        if (skillHitBox == null)
        {
            Debug.LogError("스킬 히트박스가 없습니다.");
            return false;
        }

        // HitBox와 비용을 모두 검증한 실제 실행 시점에만 캐릭터 소유 에너지를 소비한다.
        if (!player.TryUseEnergy(skill.energyCost))
            return false;

        currentSkill = skill;
        phase = SkillPhase.Attack;
        previousMovementTime = 0f;


        skillHitBox.SetRewardType(DecibelRewardType.Skill);

        CombatElement resolvedElement =
            currentSkill.hitPayload.elementOverride == CombatElement.None
            ? player.CharacterData.Element
            : currentSkill.hitPayload.elementOverride;

        CombatHitData hitData = new CombatHitData
        {
            attacker = player,
            damageMultiplier = currentSkill.hitPayload.damageMultiplier,
            impactMultiplier = currentSkill.hitPayload.impactMultiplier,
            hitReactionBuildUp = currentSkill.hitPayload.hitReactionBuildUp,
            resolvedElement = resolvedElement,
            anomalyBuildUp = currentSkill.hitPayload.anomalyBuildUp,
            canTriggerChainSkill = currentSkill.hitPayload.canTriggerChainSkill
        };

        skillHitBox.SetHitData(hitData);
        SetHitBoxActive(false);
        ResolveAttackAssist();
        player.Animator.CrossFade(currentSkill.skillAnim, 0.05f);

        return true;
    }

    private bool HasAnimatorState(string stateName)
    {
        if (player.Animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int shortNameHash = Animator.StringToHash(stateName);
        if (player.Animator.HasState(0, shortNameHash))
            return true;

        string layerName = player.Animator.GetLayerName(0);
        int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
        return player.Animator.HasState(0, fullPathHash);
    }

    private void TryChainSkill()
    {
        if (!bufferedSkillInput)
            return;

        SkillData nextSkill = currentSkill.nextSkill;

        if (!CanEnterSkill(nextSkill))
            return;

        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;

        if (!TryStartSkill(nextSkill))
            return;
    }

    private void UpdateInputBuffer()
    {
        if (!bufferedSkillInput)
            return;

        bufferedSkillTimer -= Time.deltaTime;

        if (bufferedSkillTimer <= 0f)
        {
            bufferedSkillInput = false;
            bufferedSkillTimer = 0f;
        }
    }

    private void SetHitBoxActive(bool active)
    {
        if (skillHitBox == null)
            return;

        if (skillHitboxActive == active)
            return;

        skillHitboxActive = active;
        skillHitBox.SetActive(active);
    }

    private void ResolveAttackAssist()
    {
        ClearAttackAssist();

        if (!currentSkill.useAutoAim)
            return;

        assistTarget = player.FindAttackTarget(
            currentSkill.autoAimRadius,
            currentSkill.autoAimMaxAngle);

        if (assistTarget == null)
            return;

        attackAssistDirection = player.GetAttackAssistDirection(assistTarget);

        if (attackAssistDirection.sqrMagnitude < 0.0001f)
        {
            ClearAttackAssist();
            return;
        }

        hasAttackAssist = true;
        player.RotateToward(attackAssistDirection, currentSkill.autoAimRotationMultiplier);
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

        if (normalizedTime <= currentSkill.autoAimRotateUntil)
            player.RotateToward(attackAssistDirection, currentSkill.autoAimRotationMultiplier);
    }

    private Vector3 ResolveAttackMoveDirection()
    {
        if (!currentSkill.steerMoveToTarget || !hasAttackAssist)
            return player.transform.forward;

        if (attackAssistDirection.sqrMagnitude < 0.0001f)
            return player.transform.forward;

        return attackAssistDirection;
    }

    private void ApplyForwardMovement(float normalizedTime)
    {
        float currentTime = Mathf.Clamp01(normalizedTime);
        Vector3 moveDirection = ResolveAttackMoveDirection();

        if (currentSkill.useDistanceBasedMovement)
        {
            float previousProgress = EvaluateMovementProgress(
                previousMovementTime,
                currentSkill.moveStart,
                currentSkill.moveEnd);
            float currentProgress = EvaluateMovementProgress(
                currentTime,
                currentSkill.moveStart,
                currentSkill.moveEnd);
            float progressDelta = Mathf.Max(0f, currentProgress - previousProgress);

            if (progressDelta > 0f)
            {
                float moveDistance = ClampMoveDistanceToTarget(
                    currentSkill.forwardMoveDistance * progressDelta,
                    moveDirection);

                player.Controller.Move(
                    moveDirection * moveDistance);
            }
        }
        else if (currentTime >= currentSkill.moveStart && currentTime <= currentSkill.moveEnd)
        {
            float moveDistance = ClampMoveDistanceToTarget(
                currentSkill.forwardMoveSpeed * Time.deltaTime,
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
        float remainingDistance = distanceAlongMove - currentSkill.autoAimStopDistance;

        return Mathf.Clamp(requestedDistance, 0f, Mathf.Max(0f, remainingDistance));
    }

    private void ClearAttackAssist()
    {
        assistTarget = null;
        attackAssistDirection = Vector3.zero;
        hasAttackAssist = false;
    }

    private bool CanEnterSkill(SkillData skill)
    {
        if(skill == null) return false;

        return player.CurrentEnergy >= skill.requiredEntryEnergy &&
            player.CurrentEnergy >= skill.energyCost;
    }

    private SkillData ResolveEntrySkill()
    {
        // 강화 분기를 우선 검사하고, 진입 조건을 만족하지 못하면 일반 분기로 폴백한다.
        if (CanEnterSkill(player.CharacterData.enhancedSkillBranch))
            return player.CharacterData.enhancedSkillBranch;

        if(CanEnterSkill(player.CharacterData.normalSkillBranch))
            return player.CharacterData.normalSkillBranch;

        return null;
    }
}
