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

    private void StartAttack(AttackData attackData)
    {
        currentAttack = attackData;
        phase = AttackPhase.Attack;

        SetHitBoxActive(false);

        player.Animator.CrossFade(currentAttack.attackAnim, 0.05f);
        Debug.Log($"Start Attack: {currentAttack.attackAnim}");
    }

    private void UpdateAttackPhase(AnimatorStateInfo info)
    {
        if (!info.IsName(currentAttack.attackAnim))
            return;

        float t = info.normalizedTime;

        // 공격 중 전진 이동
        if (t >= currentAttack.moveStart && t <= currentAttack.moveEnd)
        {
            Vector3 forwardMove = player.transform.forward * currentAttack.forwardMoveSpeed;
            player.Controller.Move(forwardMove * Time.deltaTime);
        }

        // 공격 판정 활성화
        bool shouldHitBoxBeActive = t >= currentAttack.startUpEnd && t < currentAttack.activeEnd;
        Debug.Log($"shouldHitBoxBeActive = {shouldHitBoxBeActive}");
        SetHitBoxActive(shouldHitBoxBeActive);

        // 콤보 입력 처리
        if (t >= currentAttack.comboInputOpenTime)
        {
            TryChainCombo();
        }

        // 공격 애니메이션 종료 -> End로 이동
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
        Debug.Log($"hitboxActive = {hitboxActive}");
        hitBox.SetActive(active);
    }
}