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

        if (hitBox == null)
        {
            hitBox = player.GetComponentInChildren<HitBox>(true);
            Debug.Log($"HitBox is ready  = {hitBox}");
        }

        StartAttack(player.normalCombo[comboIndex]);
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

    public void HandleAttack()
    {
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

    private void StartAttack(AttackData attackData)
    {
        currentAttack = attackData;
        phase = AttackPhase.Attack;

        SetHitBoxActive(false);
        ResolveAttackAssist();

        player.Animator.CrossFade(currentAttack.attackAnim, 0.05f);
        Debug.Log($"Start Attack: {currentAttack.attackAnim}");
    }

    private void UpdateAttackPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        UpdateAttackAssist(t);

        // Drive the attack forward using the assisted direction when a target exists.
        if (t >= currentAttack.moveStart && t <= currentAttack.moveEnd)
        {
            Vector3 moveDirection = ResolveAttackMoveDirection();
            Vector3 forwardMove = moveDirection * currentAttack.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }

        // Sync hitbox timing with the active frames.
        bool shouldHitBoxBeActive = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        Debug.Log($"shouldHitBoxBeActive = {shouldHitBoxBeActive}");
        SetHitBoxActive(shouldHitBoxBeActive);

        // Preserve the existing combo buffer timing.
        if (t >= currentAttack.comboInputOpenTime)
        {
            TryChainCombo();
        }

        // Transition to the end animation after the attack clip finishes.
        if (t >= 1f)
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

        if (info.normalizedTime >= 1f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private void TryChainCombo()
    {
        if (!bufferedAttackInput)
            return;

        int nextIndex = currentAttack.nextComboIndex;

        if (nextIndex < 0 || nextIndex >= player.normalCombo.Length)
            return;

        bufferedAttackInput = false;
        bufferedAttackTimer = 0f;
        comboIndex = nextIndex;

        StartAttack(player.normalCombo[comboIndex]);
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
        Debug.Log($"skillHitboxActive = {hitboxActive}");
        hitBox.SetActive(active);
    }

    private void ResolveAttackAssist()
    {
        ClearAttackAssist();

        if (!currentAttack.useAutoAim)
            return;

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
