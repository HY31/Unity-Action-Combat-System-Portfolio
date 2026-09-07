using UnityEngine;

/// <summary>
/// 궁극기와 콤보 스킬이 공유하는 접근·다단 타격·종료 단계를 처리한다.
/// 실제 궁극기는 영상의 재생 시작과 시작 애니메이션을 맞추고 두 연출이 끝난 뒤 타격을 허용한다.
/// </summary>
public class UltimateState : IPlayerState
{
    private const float RecoveryInputBufferDuration = 0.35f;
    private const float RecoveryCancelDelay = 0.15f;
    private const float RecoveryAutoExitDelay = 0.45f;

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
    private float assistStopDistance;
    private float previousMovementTime;
    private float endPhaseElapsed;
    private float lastHitEndNormalized;
    private float hitEndTransitionNormalized;

    private UltimatePhase phase;
    private bool chainSkillPending;
    private bool isChainSkillEntry;
    private bool ultimatePresentationStarted;
    private bool cinematicRequested;
    private bool cinematicActionTimeStarted;
    private bool ultimateActionStarted;
    private Transform requestedAssistTarget;
    private EnemyController chainSkillEnemy;
    private RuntimeAnimatorController originalAnimatorController;
    private AnimatorOverrideController chainSkillAnimatorOverride;

    private bool bufferedAttackInput;
    private float bufferedAttackTimer;
    private bool bufferedSkillInput;
    private float bufferedSkillTimer;
    private bool bufferedDodgeInput;
    private float bufferedDodgeTimer;

    public UltimateState(PlayerController player)
    {
        this.player = player;
    }

    public void PrepareChainSkill(Transform target)
    {
        chainSkillPending = true;
        requestedAssistTarget = target;
        chainSkillEnemy = target != null
            ? target.GetComponentInParent<EnemyController>()
            : null;
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

        // 콤보 스킬은 궁극기 상태 구조를 재사용하되 재생 클립만 일시적으로 바꾼다.
        if (isChainSkillEntry && !TryApplyChainSkillAnimationOverride())
        {
            requestedAssistTarget = null;
            player.ChangeState(player.LocomotionState);
            return;
        }

        // 콤보 스킬과 궁극기가 실제로 시작된 경우 지원 포인트를 한 칸 회복한다.
        player.SupportPointManager?.GainSupportPoint();

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
            canTriggerChainSkill = false,
            isChainSkill = isChainSkillEntry
        };

        hitBox.SetRewardType(CombatHitRewardType.None);
        hitBox.SetHitData(hitData);
        hitBox.SetFeedback(ultData.hitFeedback);
        hitBox.ConfigureShape(ultData.hitBoxShape);
        hitBox.SetActive(false);

        lastHitEndNormalized = ResolveLastHitEndNormalized();
        hitEndTransitionNormalized = Mathf.Clamp01(Mathf.Max(
            lastHitEndNormalized,
            ultData.hitEndTransitionTime));

        assistTarget = requestedAssistTarget != null
            ? requestedAssistTarget
            : ultData.useAutoAim
                ? player.FindAttackTarget(ultData.autoAimRadius, ultData.autoAimMaxAngle)
                : null;
        requestedAssistTarget = null;
        assistStopDistance = player.ResolveAttackStopDistance(
            assistTarget,
            ultData.autoAimStopDistance);
        ultAssistDirection = player.GetAttackAssistDirection(assistTarget);
        if (ultAssistDirection.sqrMagnitude > 0.0001f)
            player.RotateToward(ultAssistDirection, ultData.autoAimRotationMultiplier);

        ultimatePresentationStarted = !isChainSkillEntry;
        cinematicRequested = false;
        cinematicActionTimeStarted = false;
        ultimateActionStarted = isChainSkillEntry;
        if (ultimatePresentationStarted)
        {
            CombatPresentationEffects.BeginUltimate(resolvedElement);
            cinematicRequested = UltimateCinematicPlayer.TryPlay(ultData.cinematicClip);
        }

        player.SetInvincible(true);
        previousMovementTime = 0f;
        endPhaseElapsed = 0f;
        currentHitWindowIndex = -1;
        ResetRecoveryFollowUpInputs();
        // 콤보 스킬은 별도 시작 동작 없이 전용 공격 클립부터 재생한다.
        if (isChainSkillEntry)
        {
            phase = UltimatePhase.Hit;
            player.Animator.CrossFade(ultData.ultHitAnim, 0.05f);
        }
        else
        {
            phase = UltimatePhase.Start;

            if (!cinematicRequested)
                StartUltimateAction(false);
        }
    }

    public void Update()
    {
        UpdateRecoveryFollowUpInputs();

        if (phase == UltimatePhase.Start && !ultimateActionStarted)
        {
            if (!TryStartUltimateAction())
                return;
        }

        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (phase == UltimatePhase.Start)
        {
            UpdateStartPhase(info);
        }
        else if (phase == UltimatePhase.Hit)
        {
            UpdateHitPhase(info);
        }
        else if (phase == UltimatePhase.End)
        {
            UpdateEndPhase(info);
        }
    }

    private bool TryStartUltimateAction()
    {
        if (cinematicRequested && UltimateCinematicPlayer.IsPlaying)
        {
            if (!UltimateCinematicPlayer.HasStartedPlayback)
                return false;

            StartUltimateAction(true);
            return true;
        }

        // 영상 준비 실패 시 검은 화면에 머무르지 않고 기존 인게임 궁극기로 이어간다.
        StartUltimateAction(false);
        return true;
    }

    private void StartUltimateAction(bool useUnscaledTime)
    {
        if (ultimateActionStarted)
            return;

        ultimateActionStarted = true;
        cinematicActionTimeStarted = useUnscaledTime;

        if (useUnscaledTime)
        {
            float actionDuration = (float)ultData.cinematicClip.length + 0.25f;
            player.BeginUnscaledActionTime(actionDuration);
        }

        player.Animator.CrossFade(ultData.ultStartAnim, 0.05f);
    }

    private void UpdateStartPhase(AnimatorStateInfo info)
    {
        // 영상 프레임이 지연돼도 시작 동작은 외부 일시정지의 영향을 받지 않게 짧은 시간을 계속 연장한다.
        if (cinematicActionTimeStarted && UltimateCinematicPlayer.IsPlaying)
            player.BeginUnscaledActionTime(0.5f);
        if (!info.IsName(ultData.ultStartAnim))
            return;

        float t = info.normalizedTime;
        Vector3 moveDirection;

        UpdateAttackAssist(t);

        // 컷신 뒤에 순간 접근하지 않도록 시작 단계에서 대상 방향 접근을 끝낸다.
        float currentTime = Mathf.Clamp01(t);
        if (ultData.useDistanceBasedMovement)
        {
            if (ultData.steerMoveToTarget &&
                ultAssistDirection.sqrMagnitude > 0.0001f &&
                assistTarget != null)
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
            if (ultData.steerMoveToTarget &&
                ultAssistDirection.sqrMagnitude > 0.0001f &&
                assistTarget != null)
                moveDirection = ultAssistDirection;
            else
                moveDirection = player.transform.forward;

            float moveDistance = ClampMoveDistanceToTarget(
                ultData.forwardMoveSpeed * player.ActionDeltaTime,
                moveDirection);
            player.Controller.Move(moveDirection * moveDistance);
        }

        previousMovementTime = Mathf.Max(previousMovementTime, currentTime);

        // 영상과 같은 길이의 시작 동작을 함께 진행하고 둘 다 끝난 뒤 실제 타격을 시작한다.
        if (t >= 1f && !UltimateCinematicPlayer.IsPlaying)
        {
            hitBox.SetActive(false);

            if (cinematicActionTimeStarted)
            {
                player.EndUnscaledActionTime();
                cinematicActionTimeStarted = false;
            }

            phase = UltimatePhase.Hit;
            player.Animator.CrossFade(ultData.ultHitAnim, 0.05f);
        }
    }

    private void UpdateAttackAssist(float normalizedTime)
    {
        if (assistTarget == null)
            return;

        Vector3 direction = player.GetAttackAssistDirection(assistTarget);
        if (direction.sqrMagnitude > 0.0001f)
            ultAssistDirection = direction;

        if (normalizedTime <= ultData.autoAimRotateUntil)
            player.RotateToward(ultAssistDirection, ultData.autoAimRotationMultiplier);
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
        float remainingDistance = distanceAlongMove - assistStopDistance;

        return Mathf.Clamp(requestedDistance, 0f, Mathf.Max(0f, remainingDistance));
    }
    private void UpdateHitPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(ultData.ultHitAnim))
            return;

        float t = info.normalizedTime;

        int detectedWindowIndex = -1;

        if (isChainSkillEntry)
            UpdateChainSkillMovement(t);

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
                CombatAudio.PlayUltimate(
                    player,
                    currentHitWindowIndex,
                    isChainSkillEntry);

            // 이후 한 대상당 한 번만 맞히는 목록을 추가하면 새 윈도우 진입 시 여기서 초기화한다.
        }

        // 궁극기와 콤보 스킬은 여러 타격 구간 중 마지막 구간만 강공격으로 판정한다.
        bool isFinishingHeavyHit =
            shouldHit &&
            detectedWindowIndex == ultData.hitWindows.Length - 1 &&
            ultData.hitPayload.canTriggerChainSkill;
        hitBox.SetChainSkillTriggerEnabled(isFinishingHeavyHit);
        hitBox.SetActive(shouldHit);


        // 마지막 타격을 보장한 뒤에는 남은 후속 동작을 기다리지 않고 입력으로 취소할 수 있다.
        if (t >= lastHitEndNormalized &&
            !UltimateCinematicPlayer.IsPlaying &&
            TryExitToRecoveryAction())
        {
            return;
        }

        if (t >= hitEndTransitionNormalized)
            BeginEndPhase();
    }

    private float ResolveLastHitEndNormalized()
    {
        if (ultData.hitWindows == null || ultData.hitWindows.Length == 0)
            return 1f;

        float lastHitEnd = 0f;
        for (int i = 0; i < ultData.hitWindows.Length; i++)
            lastHitEnd = Mathf.Max(lastHitEnd, ultData.hitWindows[i].end);

        return Mathf.Clamp01(lastHitEnd);
    }

    private void BeginEndPhase()
    {
        hitBox.SetActive(false);
        currentHitWindowIndex = -1;
        phase = UltimatePhase.End;
        endPhaseElapsed = 0f;
        player.Animator.CrossFade(ultData.ultEndAnim, 0.05f);
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(ultData.ultEndAnim))
            return;

        endPhaseElapsed += player.ActionDeltaTime;

        // 마지막 타격이 끝나고 영상까지 닫혔다면 짧은 안정 시간 뒤 궁극기와 콤보 스킬의 후딜을 취소한다.
        if (!UltimateCinematicPlayer.IsPlaying &&
            endPhaseElapsed >= RecoveryCancelDelay)
        {
            if (TryExitToRecoveryAction())
                return;
        }

        // 종료 클립 자체가 길어도 짧은 후딜까지만 보여주고 조작권을 자동으로 돌려준다.
        if ((!UltimateCinematicPlayer.IsPlaying &&
             endPhaseElapsed >= RecoveryAutoExitDelay) ||
            info.normalizedTime >= 1f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private bool TryExitToRecoveryAction()
    {
        if (TryExecuteRecoveryFollowUp())
            return true;

        // 이동은 버튼 이벤트가 아닌 지속 입력이므로 별도 버퍼 없이 현재 값을 사용한다.
        if (player.MoveInput.sqrMagnitude <= 0.0001f)
            return false;

        player.ChangeState(player.LocomotionState);
        return true;
    }

    private void UpdateRecoveryFollowUpInputs()
    {
        float deltaTime = player.ActionDeltaTime;
        UpdateBufferedInput(ref bufferedAttackInput, ref bufferedAttackTimer, deltaTime);
        UpdateBufferedInput(ref bufferedSkillInput, ref bufferedSkillTimer, deltaTime);
        UpdateBufferedInput(ref bufferedDodgeInput, ref bufferedDodgeTimer, deltaTime);
    }

    private static void UpdateBufferedInput(
        ref bool bufferedInput,
        ref float remaining,
        float deltaTime)
    {
        if (!bufferedInput)
            return;

        remaining -= deltaTime;
        if (remaining > 0f)
            return;

        bufferedInput = false;
        remaining = 0f;
    }

    private bool TryExecuteRecoveryFollowUp()
    {
        // 회피를 가장 먼저 처리하고, 스킬과 평타 순서로 후속 행동의 우선순위를 둔다.
        if (bufferedDodgeInput)
        {
            bufferedDodgeInput = false;
            player.ChangeState(player.DodgeState);
            return true;
        }

        if (bufferedSkillInput)
        {
            bufferedSkillInput = false;
            player.ChangeState(player.SkillState);
            return true;
        }

        if (bufferedAttackInput)
        {
            bufferedAttackInput = false;
            player.ChangeState(player.AttackState);
            return true;
        }

        return false;
    }

    private void ResetRecoveryFollowUpInputs()
    {
        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
        bufferedDodgeInput = false;
        bufferedDodgeTimer = 0f;
    }

    private bool TryApplyChainSkillAnimationOverride()
    {
        if (ultData.chainSkillAnim == null || ultData.chainSkillEndAnim == null)
        {
            Debug.LogError(
                "콤보 스킬 상태: 공격 또는 종료 애니메이션이 연결되지 않았습니다.",
                player);
            return false;
        }

        RuntimeAnimatorController runtimeController = player.Animator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            Debug.LogError("콤보 스킬 상태: Animator Controller가 없습니다.", player);
            return false;
        }

        bool hasHitClip = false;
        bool hasEndClip = false;
        AnimationClip[] controllerClips = runtimeController.animationClips;

        for (int i = 0; i < controllerClips.Length; i++)
        {
            AnimationClip clip = controllerClips[i];
            if (clip == null)
                continue;

            if (clip.name == ultData.ultHitAnim)
                hasHitClip = true;
            else if (clip.name == ultData.ultEndAnim)
                hasEndClip = true;
        }

        if (!hasHitClip || !hasEndClip)
        {
            Debug.LogError(
                $"콤보 스킬 상태: 교체할 궁극기 상태 클립을 찾지 못했습니다. " +
                $"공격={ultData.ultHitAnim}, 종료={ultData.ultEndAnim}",
                player);
            return false;
        }

        originalAnimatorController = runtimeController;
        chainSkillAnimatorOverride = new AnimatorOverrideController(runtimeController);
        chainSkillAnimatorOverride[ultData.ultHitAnim] = ultData.chainSkillAnim;
        chainSkillAnimatorOverride[ultData.ultEndAnim] = ultData.chainSkillEndAnim;
        player.Animator.runtimeAnimatorController = chainSkillAnimatorOverride;
        return true;
    }

    private void RestoreChainSkillAnimationOverride()
    {
        if (chainSkillAnimatorOverride == null)
            return;

        if (player.Animator.runtimeAnimatorController == chainSkillAnimatorOverride)
            player.Animator.runtimeAnimatorController = originalAnimatorController;

        Object.Destroy(chainSkillAnimatorOverride);
        chainSkillAnimatorOverride = null;
        originalAnimatorController = null;
    }

    private void UpdateChainSkillMovement(float normalizedTime)
    {
        UpdateAttackAssist(normalizedTime);

        float currentTime = Mathf.Clamp01(normalizedTime);
        Vector3 moveDirection =
            ultData.steerMoveToTarget &&
            ultAssistDirection.sqrMagnitude > 0.0001f &&
            assistTarget != null
            ? ultAssistDirection
            : player.transform.forward;

        if (ultData.useDistanceBasedMovement)
        {
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
            float moveDistance = ClampMoveDistanceToTarget(
                ultData.forwardMoveSpeed * player.ActionDeltaTime,
                moveDirection);
            player.Controller.Move(moveDirection * moveDistance);
        }

        previousMovementTime = Mathf.Max(previousMovementTime, currentTime);
    }

    public void Exit()
    {
        if (isChainSkillEntry)
            chainSkillEnemy?.NotifyChainSkillFinished();

        RestoreChainSkillAnimationOverride();
        // 어떤 경로로 상태를 빠져나가도 무적과 타격 판정이 남지 않게 정리한다.
        if (cinematicRequested && UltimateCinematicPlayer.IsPlaying)
            UltimateCinematicPlayer.StopPlayback();

        if (cinematicActionTimeStarted)
            player.EndUnscaledActionTime();

        if (ultimatePresentationStarted)
            CombatPresentationEffects.EndUltimate();

        ultimatePresentationStarted = false;
        cinematicRequested = false;
        cinematicActionTimeStarted = false;
        ultimateActionStarted = false;
        isChainSkillEntry = false;
        chainSkillPending = false;
        chainSkillEnemy = null;
        requestedAssistTarget = null;
        assistTarget = null;
        assistStopDistance = 0f;
        endPhaseElapsed = 0f;
        ResetRecoveryFollowUpInputs();
        player.SetInvincible(false);

        hitBox?.SetActive(false);
    }

    #region Handle
    public void HandleAttack()
    {
        bufferedAttackInput = true;
        bufferedAttackTimer = RecoveryInputBufferDuration;
    }
    public void HandleDodge()
    {
        bufferedDodgeInput = true;
        bufferedDodgeTimer = RecoveryInputBufferDuration;
    }
    public void HandleHit()
    {
    }
    public void HandleSkill()
    {
        bufferedSkillInput = true;
        bufferedSkillTimer = RecoveryInputBufferDuration;
    }
    public void HandleUltimate()
    {
    }
    public void HandleParry()
    {
    }
    #endregion
}



