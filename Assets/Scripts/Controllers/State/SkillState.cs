using UnityEngine;

/// <summary>
/// 일반·강화 특수 스킬의 에너지 확정 시점, 무적, 판정과 후딜 캔슬을 처리한다.
/// </summary>
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

    // 현재 스킬이 에너지 커밋 지점을 통과했는지 표시한다.
    private bool energyCommitted;

    // 스킬 도중 입력된 평타를 캔슬 가능 시점까지 잠시 보관한다.
    private bool bufferedAttackInput;
    private float bufferedAttackTimer;

    private bool bufferedSkillInput;
    private float bufferedSkillTimer;
    private bool bufferedDodgeInput;
    private float bufferedDodgeTimer;
    private const float BufferDuration = 0.3f;

    private bool skillHitboxActive;
    private HitBox hitBox;

    private Transform assistTarget;
    private Vector3 attackAssistDirection;
    private bool hasAttackAssist;
    private float assistStopDistance;
    private float previousMovementTime;
    private bool skillSwingPlayed;
    private bool skillEndSoundPlayed;

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

        // 강화 특수 스킬은 선딜부터 종료 모션까지 SkillState에 머무는 전체 구간을 보호한다.
        bool isEnhancedSkill =
            entrySkill == player.CharacterData.enhancedSkillBranch;
        player.SetInvincible(isEnhancedSkill);
    }

    private void ResetRuntimeFlags()
    {
        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        bufferedSkillInput = false;
        energyCommitted = false;
        bufferedSkillTimer = 0f;
        bufferedDodgeInput = false;
        bufferedDodgeTimer = 0f;
        skillHitboxActive = false;
        previousMovementTime = 0f;
        skillSwingPlayed = false;
        skillEndSoundPlayed = false;

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
        player.SetInvincible(false);
        ResetRuntimeFlags();
    }

    #region Handle
    public void HandleAttack()
    {
        bufferedAttackInput = true;
        bufferedAttackTimer = BufferDuration;
    }

    public void HandleDodge()
    {
        if (CanCancelToDodge())
        {
            player.ChangeState(player.DodgeState);
            return;
        }

        // 강화 스킬의 보호 구간 끝에 가까운 회피 입력은 잠깐 보관해 입력 누락을 줄인다.
        bufferedDodgeInput = true;
        bufferedDodgeTimer = BufferDuration;
    }

    public void HandleHit()
    {
    }

    public void HandleSkill()
    {
        bufferedSkillInput = true;
        bufferedSkillTimer = BufferDuration;
    }
    public void HandleUltimate()
    {
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

        bool canExecuteSkill = TryCommitEnergy(t);

        // 커밋 실패로 LocomotionState로 전환됐다면
        // 이전 SkillState의 갱신을 즉시 종료한다.
        if (player.CurrentState != this)
            return;

        UpdateSkillSwing(t, canExecuteSkill);
        UpdateAttackAssist(t);

        // 루트 모션 대신 애니메이션 정규화 시간에 맞춰 결정론적으로 전진시킨다.
        ApplyForwardMovement(t);

        // 스킬 데이터의 활성 구간에만 선택된 슬롯의 HitBox를 켠다.
        bool shouldHitBoxBeActive =
            canExecuteSkill &&
            t >= currentSkill.hitStart &&
            t < currentSkill.hitEnd;
        SetHitBoxActive(shouldHitBoxBeActive);
        UpdateSkillEndSound(t, canExecuteSkill);

        // 회피는 스킬 연계와 평타보다 우선하는 방어 행동이다.
        if (TryCancelToDodge())
            return;

        // 스킬 연계를 가장 먼저 처리한다.
        if (t >= currentSkill.chainInputOpenTime &&
            TryChainSkill())
        {
            return;
        }

        // 예약된 평타가 있으면 평타로 연결한다.
        if (TryCancelToAttack(t))
            return;

        // 평타 예약이 없다면 이동 입력으로 후딜을 취소한다.
        if (TryCancelToLocomotion(t))
            return;

        if (t >= currentSkill.endTransitionTime)
        {
            SetHitBoxActive(false);
            phase = SkillPhase.End;
            player.Animator.CrossFade(currentSkill.endAnim, 0.05f);
        }
    }

    private void UpdateSkillSwing(float normalizedTime, bool canExecuteSkill)
    {
        if (skillSwingPlayed || currentSkill == null || !canExecuteSkill)
            return;

        float swingTime = Mathf.Max(0f, currentSkill.hitStart - 0.10f);
        if (normalizedTime < swingTime)
            return;

        skillSwingPlayed = true;
        bool isEnhancedSkill =
            currentSkill == player.CharacterData.enhancedSkillBranch;
        CombatAudio.PlaySkill(
            player,
            isEnhancedSkill,
            currentSkill.hitPayload.impactMultiplier);
    }

    /// <summary>
    /// 마지막 판정이 끝난 직후 캐릭터 전용 스킬 끝음을 한 번만 재생한다.
    /// 스킬이 판정 전에 취소되거나 에너지 확정에 실패하면 재생하지 않는다.
    /// </summary>
    private void UpdateSkillEndSound(float normalizedTime, bool canExecuteSkill)
    {
        if (skillEndSoundPlayed || currentSkill == null || !canExecuteSkill)
            return;

        if (normalizedTime < currentSkill.hitEnd)
            return;

        skillEndSoundPlayed = true;
        CombatAudio.PlaySkillEnd(player);
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        // 본 스킬 연출은 끝났으므로 모든 후속 입력 시점을 통과한 것으로 취급한다.
        if (TryCancelToDodge())
            return;

        if (TryChainSkill())
            return;

        if (TryCancelToAttack(1f))
            return;

        if (TryCancelToLocomotion(1f))
            return;

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

        hitBox = player.AttackHitBox;

        if (hitBox == null)
        {
            Debug.LogError("스킬 히트박스가 없습니다.");
            return false;
        }

        currentSkill = skill;
        phase = SkillPhase.Attack;
        previousMovementTime = 0f;
        skillSwingPlayed = false;
        skillEndSoundPlayed = false;

        // 일반 특수 스킬은 에너지 비용이 없으므로 시작 즉시 실행 확정.
        // 강화 특수 스킬은 energyCommitTime까지 선딜 상태로 둔다.
        energyCommitted = currentSkill.energyCost <= 0f;

        hitBox.SetRewardType(CombatHitRewardType.Skill);

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
            canTriggerChainSkill = false
        };

        hitBox.SetHitData(hitData);
        hitBox.SetFeedback(currentSkill.hitFeedback);
        hitBox.ConfigureShape(currentSkill.hitBoxShape);
        SetHitBoxActive(false);
        ResolveAttackAssist();
        player.Animator.CrossFade(currentSkill.skillAnim, 0.05f);

        return true;
    }

    private bool TryCommitEnergy(float normalizedTime)
    {
        // 일반 특수 스킬이거나 이미 비용을 지불했다면 실행 가능.
        if (energyCommitted)
            return true;

        // 아직 선딜 구간이면 비용을 지불하지 않는다.
        if (normalizedTime < currentSkill.energyCommitTime)
            return false;

        // 커밋 순간에 에너지를 다시 확인하고 실제로 소비한다.
        if (!player.TryUseEnergy(currentSkill.energyCost))
        {
            player.ChangeState(player.LocomotionState);
            return false;
        }

        // 이후 프레임에서 에너지가 중복 소비되지 않도록 기록한다.
        energyCommitted = true;
        return true;
    }

    private bool CanCancelToDodge()
    {
        if (currentSkill == null)
            return false;

        // 에너지를 사용하지 않는 일반 특수 스킬은 전 구간 회피 가능.
        if (currentSkill.energyCost <= 0f)
            return true;

        // 강화 특수 스킬의 종료 모션은 주요 공격 연출이 끝났으므로 회피 가능.
        if (phase == SkillPhase.End)
            return true;

        AnimatorStateInfo info =
            player.Animator.GetCurrentAnimatorStateInfo(0);

        // 스킬 진입 직후 CrossFade 중이고 아직 에너지를 쓰지 않았다면
        // 선딜 구간으로 취급하여 회피를 허용한다.
        if (!info.IsName(currentSkill.skillAnim))
            return !energyCommitted;

        float t = info.normalizedTime;

        // 입력 처리와 Update의 실행 순서가 바뀌어도
        // 애니메이션 시간이 커밋 지점을 넘었으면 선딜로 취급하지 않는다.
        bool isBeforeEnergyCommit =
            !energyCommitted &&
            t < currentSkill.energyCommitTime;

        if (isBeforeEnergyCommit)
            return true;

        // 데이터가 잘못 설정되더라도 피해 판정이 끝나기 전에는
        // 강화 특수 스킬을 회피로 취소하지 못하게 보호한다.
        float dodgeUnlockTime = Mathf.Max(
            currentSkill.enhancedDodgeUnlockTime,
            currentSkill.hitEnd);

        return t >= dodgeUnlockTime;
    }

    private bool TryCancelToDodge()
    {
        if (!bufferedDodgeInput || !CanCancelToDodge())
            return false;

        bufferedDodgeInput = false;
        bufferedDodgeTimer = 0f;
        player.ChangeState(player.DodgeState);
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

    private bool TryChainSkill()
    {
        if (!bufferedSkillInput)
            return false;

        SkillData nextSkill = currentSkill.nextSkill;

        if (!CanEnterSkill(nextSkill))
            return false;

        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;

        return TryStartSkill(nextSkill);
    }

    private void UpdateInputBuffer()
    {
        if (bufferedAttackInput)
        {
            bufferedAttackTimer -= player.ActionDeltaTime;

            if (bufferedAttackTimer <= 0f)
            {
                bufferedAttackInput = false;
                bufferedAttackTimer = 0f;
            }
        }

        if (bufferedSkillInput)
        {
            bufferedSkillTimer -= player.ActionDeltaTime;

            if (bufferedSkillTimer <= 0f)
            {
                bufferedSkillInput = false;
                bufferedSkillTimer = 0f;
            }
        }

        if (bufferedDodgeInput)
        {
            bufferedDodgeTimer -= player.ActionDeltaTime;

            if (bufferedDodgeTimer <= 0f)
            {
                bufferedDodgeInput = false;
                bufferedDodgeTimer = 0f;
            }
        }
    }

    private float ResolveActionCancelOpenTime(float configuredOpenTime)
    {
        // 일반 특수 스킬은 데이터에 설정된 캔슬 시점을 그대로 사용한다.
        if (currentSkill.energyCost <= 0f)
            return configuredOpenTime;

        // 강화 특수 스킬은 대미지와 주요 연출이 끝나기 전까지
        // 평타나 이동으로도 취소할 수 없게 보호한다.
        float enhancedLockEnd = Mathf.Max(
            currentSkill.enhancedDodgeUnlockTime,
            currentSkill.hitEnd);

        return Mathf.Max(configuredOpenTime, enhancedLockEnd);
    }

    private bool TryCancelToAttack(float normalizedTime)
    {
        if (!bufferedAttackInput)
            return false;

        float cancelOpenTime =
            ResolveActionCancelOpenTime(currentSkill.attackCancelOpenTime);

        if (normalizedTime < cancelOpenTime)
            return false;

        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;

        player.ChangeState(player.AttackState);
        return true;
    }

    private bool TryCancelToLocomotion(float normalizedTime)
    {
        if (player.MoveInput.sqrMagnitude <= 0.0001f)
            return false;

        float cancelOpenTime =
            ResolveActionCancelOpenTime(currentSkill.locomotionCancelOpenTime);

        if (normalizedTime < cancelOpenTime)
            return false;

        // 평타가 예약돼 있다면 이동보다 평타를 우선한다.
        if (bufferedAttackInput)
            return false;

        player.ChangeState(player.LocomotionState);
        return true;
    }

    private void SetHitBoxActive(bool active)
    {
        if (hitBox == null)
            return;

        // 스킬 데이터는 타격 구간이 하나이므로 이 구간이 해당 스킬의 마지막 타격이다.
        bool isFinishingHeavyHit =
            active &&
            currentSkill != null &&
            currentSkill.hitPayload.canTriggerChainSkill;
        hitBox.SetChainSkillTriggerEnabled(isFinishingHeavyHit);

        if (skillHitboxActive == active)
            return;

        skillHitboxActive = active;
        hitBox.SetActive(active);
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

        assistStopDistance = player.ResolveAttackStopDistance(
            assistTarget,
            currentSkill.autoAimStopDistance);

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
                currentSkill.forwardMoveSpeed * player.ActionDeltaTime,
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
        float remainingDistance = distanceAlongMove - assistStopDistance;

        return Mathf.Clamp(requestedDistance, 0f, Mathf.Max(0f, remainingDistance));
    }

    private void ClearAttackAssist()
    {
        assistTarget = null;
        attackAssistDirection = Vector3.zero;
        hasAttackAssist = false;
        assistStopDistance = 0f;
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
