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

    // 엘렌의 강화 특수 스킬은 연계 공격이 있기 때문에 콤보를 넣음
    private int comboIndex;

    private bool bufferedSkillInput;
    private float bufferedSkillTimer;
    private const float BufferDuration = 0.2f;

    private bool skillHitboxActive;
    private HitBox skillHitBox;

    private Transform assistTarget;
    private Vector3 attackAssistDirection;
    private bool hasAttackAssist;

    public SkillState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        comboIndex = 0;
        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
        skillHitboxActive = false;
        
        ClearAttackAssist();

        if (player.skillCombo == null)
        {
            player.ChangeState(player.LocomotionState);
            return;
        }

        if (player.skillCombo != null && player.skillCombo.Length > 0)
        {
            currentSkill = player.skillCombo[comboIndex];
            Debug.Log($"Current Skill is ready  = {currentSkill}");
        }

        if (skillHitBox == null)
        {
            skillHitBox = player.GetComponentInChildren<HitBox>(true);
            Debug.Log($"HitBox is ready  = {skillHitBox}");
        }

        if (player.TryUseEnergy(currentSkill.energyCost))
        {
            StartSkill(player.skillCombo[comboIndex]);
        }
        else
        {
            player.ChangeState(player.LocomotionState);
        }
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
        ClearAttackAssist();
    }

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

    private void StartSkill(SkillData skillData)
    {
        currentSkill = skillData;
        phase = SkillPhase.Attack;

        SetHitBoxActive(false);
        ResolveAttackAssist();

        player.Animator.CrossFade(currentSkill.skillAnim, 0.05f);
        Debug.Log($"Start Attack: {currentSkill.skillAnim}");
    }

    private void UpdateSkilllPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentSkill.skillAnim))
            return;

        float t = info.normalizedTime;

        UpdateAttackAssist(t);

        // Drive the attack forward using the assisted direction when a target exists.
        if (t >= currentSkill.moveStart && t <= currentSkill.moveEnd)
        {
            Vector3 moveDirection = ResolveAttackMoveDirection();
            Vector3 forwardMove = moveDirection * currentSkill.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }

        // Sync hitbox timing with the active frames.
        bool shouldHitBoxBeActive = t >= currentSkill.hitStart && t < currentSkill.hitEnd;
        Debug.Log($"shouldHitBoxBeActive = {shouldHitBoxBeActive}");
        SetHitBoxActive(shouldHitBoxBeActive);

        // Preserve the existing combo buffer timing.
        if (t >= currentSkill.skillComboInputOpenTime)
        {
            TryChainCombo();
        }

        // Transition to the end animation after the attack clip finishes.
        if (t >= 1f)
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

        if (info.normalizedTime >= 1f)
        {
            player.ChangeState(player.LocomotionState);
        }
    }

    private void TryChainCombo()
    {
        if (!bufferedSkillInput)
            return;

        int nextIndex = currentSkill.nextComboIndex;

        if (nextIndex < 0 || nextIndex >= player.skillCombo.Length)
            return;

        bufferedSkillInput = false;
        bufferedSkillTimer = 0f;
        comboIndex = nextIndex;

        StartSkill(player.skillCombo[comboIndex]);
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
        Debug.Log($"skillHitboxActive = {skillHitboxActive}");
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

    private void ClearAttackAssist()
    {
        assistTarget = null;
        attackAssistDirection = Vector3.zero;
        hasAttackAssist = false;
    }
}
