using UnityEngine;

public class SupportState : IPlayerState
{
    private enum ParryPhase
    {
        WindUp,
        Recoil
    }

    private const float ImpactFallbackDuration = 1.6f;

    private readonly PlayerController player;
    private EnemyController parryTarget;
    private ParryPhase phase;
    private float phaseElapsed;
    private Vector3 recoilDirection;
    private float appliedRecoilDistance;
    private float baseAnimatorSpeed = 1f;

    public SupportState(PlayerController player)
    {
        this.player = player;
    }

    public void SetParryTarget(EnemyController target)
    {
        parryTarget = target;
    }

    public void Enter()
    {
        // 준비, 충돌, 밀려남 후딜이 끝날 때까지 지원 캐릭터는 피격되지 않는다.
        player.SetInvincible(true);
        FaceParryTarget();

        phase = ParryPhase.WindUp;
        phaseElapsed = 0f;
        recoilDirection = Vector3.zero;
        appliedRecoilDistance = 0f;

        if (player.Animator != null)
        {
            baseAnimatorSpeed = player.Animator.speed;
            player.Animator.speed = baseAnimatorSpeed * Mathf.Max(
                0.01f,
                player.CharacterData.parryPlaybackSpeed);
        }

        string windUpAnimation = player.CharacterData.parrySupportStartAnim;
        if (!string.IsNullOrEmpty(windUpAnimation))
            player.Animator.CrossFade(windUpAnimation, 0.035f);

        Transform targetTransform = parryTarget != null ? parryTarget.transform : null;
        ThirdPersonCameraController.Active?.PlayParryCamera(player.transform, targetTransform);
    }

    public void Update()
    {
        phaseElapsed += Time.unscaledDeltaTime;

        if (phase == ParryPhase.WindUp)
        {
            if (phaseElapsed >= player.CharacterData.parryWindUpDuration)
                ResolveParryImpact();

            return;
        }

        UpdateRecoilMovement();

        string impactAnimation = player.CharacterData.parrySupportHeavyAnim;
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (!string.IsNullOrEmpty(impactAnimation)
            && info.IsName(impactAnimation)
            && info.normalizedTime >= 0.95f)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        // 클립 참조가 끊겼거나 상태 전환에 실패해도 패링 상태에 영구 고정되지 않는다.
        float fallbackDuration = Mathf.Max(
            ImpactFallbackDuration,
            player.CharacterData.parryRecoilDuration + 0.25f);
        if (phaseElapsed >= fallbackDuration)
            player.ChangeState(player.LocomotionState);
    }

    public void Exit()
    {
        // 준비 동작 중 취소된 경우에만 즉시 카메라를 복구한다.
        // 충돌까지 성립한 카메라는 상태 종료와 분리해 예정된 홀드/복귀 연출을 끝까지 재생한다.
        player.SetInvincible(false);
        if (player.Animator != null)
            player.Animator.speed = baseAnimatorSpeed;
        if (phase == ParryPhase.WindUp)
            ThirdPersonCameraController.Active?.EndParryCamera();

        parryTarget = null;
        recoilDirection = Vector3.zero;
        appliedRecoilDistance = 0f;
    }

    private void ResolveParryImpact()
    {
        phase = ParryPhase.Recoil;
        phaseElapsed = 0f;
        appliedRecoilDistance = 0f;

        FaceParryTarget();
        bool parrySucceeded =
            parryTarget != null &&
            parryTarget.TryApplyParryReaction();

        if (parrySucceeded)
            CombatOperationEvents.Report(CombatOperationType.DefensiveAssist, player);

        ResolveRecoilDirection();

        string impactAnimation = player.CharacterData.parrySupportHeavyAnim;
        if (!string.IsNullOrEmpty(impactAnimation))
            player.Animator.CrossFade(impactAnimation, 0.035f);

        if (parrySucceeded)
        {
            CombatPresentationEffects.PlayParry();
            ThirdPersonCameraController.Active?.ResolveParryCamera();
        }
        else
        {
            ThirdPersonCameraController.Active?.EndParryCamera();
        }
    }

    private void ResolveRecoilDirection()
    {
        recoilDirection = player.transform.forward * -1f;
        if (parryTarget == null)
            return;

        Vector3 awayFromEnemy = player.transform.position - parryTarget.transform.position;
        awayFromEnemy.y = 0f;

        if (awayFromEnemy.sqrMagnitude > 0.0001f)
            recoilDirection = awayFromEnemy.normalized;
    }

    private void UpdateRecoilMovement()
    {
        float duration = Mathf.Max(0.01f, player.CharacterData.parryRecoilDuration);
        float normalizedTime = Mathf.Clamp01(phaseElapsed / duration);

        // 충돌 직후 크게 밀리고 점차 감속하는 Ease-Out 이동을 사용한다.
        float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
        float targetDistance = player.CharacterData.parryRecoilDistance * easedTime;
        float moveDistance = targetDistance - appliedRecoilDistance;

        if (moveDistance <= 0f || recoilDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 displacement = recoilDirection * moveDistance;
        if (player.Controller != null && player.Controller.enabled)
            player.Controller.Move(displacement);
        else
            player.transform.position += displacement;

        appliedRecoilDistance = targetDistance;
    }

    private bool IsCounterWindowOpen()
    {
        if (phase != ParryPhase.Recoil)
            return false;

        float openTime = Mathf.Max(0f, player.CharacterData.parryCounterWindowOpenTime);
        float closeTime = Mathf.Max(openTime, player.CharacterData.parryCounterWindowCloseTime);
        return phaseElapsed >= openTime && phaseElapsed <= closeTime;
    }

    private void TryStartAssistFollowUp()
    {
        if (!IsCounterWindowOpen())
            return;

        // 세 캐릭터 모두 실제 타격까지 보장되도록 기존 평타 상태로 넘긴다.
        // 전용 AssaultAid 데이터가 준비되면 이 전환 대상만 전용 상태로 교체할 수 있다.
        FaceParryTarget();
        player.ChangeState(player.AttackState);
    }

    private bool CanDodgeFromRecoil()
    {
        if (phase != ParryPhase.Recoil)
            return false;

        float unlockTime = Mathf.Clamp01(player.CharacterData.parryDodgeUnlockTime);
        string impactAnimation = player.CharacterData.parrySupportHeavyAnim;
        AnimatorStateInfo info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (!string.IsNullOrEmpty(impactAnimation) && info.IsName(impactAnimation))
            return info.normalizedTime >= unlockTime;

        // Animator 전환 중 상태 이름을 아직 읽지 못한 경우에도 후딜 끝에서는 빠져나갈 수 있다.
        return phaseElapsed >= ImpactFallbackDuration * unlockTime;
    }

    private void FaceParryTarget()
    {
        if (parryTarget == null)
            return;

        Vector3 direction = parryTarget.transform.position - player.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
            player.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    #region Handle
    public void HandleAttack()
    {
        TryStartAssistFollowUp();
    }

    public void HandleDodge()
    {
        if (!CanDodgeFromRecoil())
            return;

        player.DodgeState.SetDodgeType(DodgeType.Normal);
        player.ChangeState(player.DodgeState);
    }

    public void HandleHit()
    {
    }

    public void HandleSkill()
    {
        TryStartAssistFollowUp();
    }

    public void HandleUltimate()
    {
    }

    public void HandleParry()
    {
    }
    #endregion
}