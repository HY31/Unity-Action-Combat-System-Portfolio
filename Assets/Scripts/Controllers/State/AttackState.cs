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

    private bool bufferedAttackInput;
    private float bufferedAttackTimer;
    private const float BufferDuration = 0.2f;

    private bool hitboxActive;
    private HitBox hitBox;

    private Transform assistTarget;
    private Vector3 attackAssistDirection;
    private bool hasAttackAssist;


    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        comboIndex = 0;
        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        hitboxActive = false;
        ClearAttackAssist();

        if (player.CharacterData == null
            || player.CharacterData.normalCombo == null
            || player.CharacterData.normalCombo.Length == 0)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        if (hitBox == null)
        {
            hitBox = player.GetComponentInChildren<HitBox>(true);
        }

        StartAttack(player.CharacterData.normalCombo[comboIndex]);
    }

    public void Update()
    {
        UpdateInputBuffer();

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
        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        hitboxActive = false;
        ClearAttackAssist();
    }

    #region Handle
    public void HandleAttack()
    {
        // 연타 입력을 짧게 보관해 콤보 허용 구간에 들어오는 즉시 다음 공격으로 연결한다.
        bufferedAttackInput = true;
        bufferedAttackTimer = BufferDuration;
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
        // 실제 젠존제에서는 공격중에 스킬 누르면 바로 스킬 나감
        // player.ChangeState(player.SkillState);
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

    private void StartAttack(AttackData attackData)
    {
        currentAttack = attackData;
        phase = AttackPhase.Attack;

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
            resolvedElement = resolvedElement,
            anomalyBuildUp = currentAttack.hitPayload.anomalyBuildUp,
            canTriggerChainSkill = currentAttack.hitPayload.canTriggerChainSkill
        };

        hitBox.SetRewardType(DecibelRewardType.NormalAttack);
        hitBox.SetHitData(hitData);
        SetHitBoxActive(false);
        ResolveAttackAssist();

        player.Animator.CrossFade(currentAttack.attackAnim, 0.05f);
    }

    private void UpdateAttackPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        UpdateAttackAssist(t);

        // 보조 타겟이 있으면 공격 이동도 타겟 방향으로 유도한다.
        if (t >= currentAttack.moveStart && t <= currentAttack.moveEnd)
        {
            Vector3 moveDirection = ResolveAttackMoveDirection();
            Vector3 forwardMove = moveDirection * currentAttack.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }

        // 애니메이션 정규화 시간과 실제 타격 판정의 활성 프레임을 동기화한다.
        bool shouldHitBoxBeActive = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        SetHitBoxActive(shouldHitBoxBeActive);

        // 허용 시점 전에 들어온 버퍼 입력까지 포함해 콤보 전환을 시도한다.
        if (t >= currentAttack.comboInputOpenTime)
        {
            TryChainCombo();
        }

        // 본 공격이 끝나면 별도 회복 모션으로 넘어가고, 해당 모션의 설정 시점부터 이동을 허용한다.
        if (t >= currentAttack.endTransitionTime)
        {
            SetHitBoxActive(false);
            phase = AttackPhase.End;
            player.Animator.CrossFade(currentAttack.endAnim, 0.05f);
        }
    }

    private void UpdateEndPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.endAnim))
            return;

        if (info.normalizedTime >= currentAttack.locomotionRecoverTime)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private void TryChainCombo()
    {
        if (!bufferedAttackInput)
            return;

        int nextIndex = currentAttack.nextComboIndex;

        if (nextIndex < 0 || nextIndex >= player.CharacterData.normalCombo.Length)
            return;

        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        comboIndex = nextIndex;

        StartAttack(player.CharacterData.normalCombo[comboIndex]);
    }

    private void UpdateInputBuffer()
    {
        if (!bufferedAttackInput)
            return;

        bufferedAttackTimer -= Time.deltaTime;

        if (bufferedAttackTimer <= 0f)
        {
            bufferedAttackInput = false;
            bufferedAttackTimer = 0f;
        }
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

    private void ClearAttackAssist()
    {
        assistTarget = null;
        attackAssistDirection = Vector3.zero;
        hasAttackAssist = false;
    }
}


