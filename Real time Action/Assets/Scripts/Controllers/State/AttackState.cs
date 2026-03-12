using System;
using UnityEngine;

public class AttackState : IPlayerState
{
    private PlayerController player;

    private AttackData currentAttack;
    private bool attackInput;
    private bool comboTriggered;

    private int comboIndex;

    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        comboIndex = 0;
        StartAttack(player.normalCombo[comboIndex]);
    }

    public void Update()
    {
        var info = player.Animator.GetCurrentAnimatorStateInfo(0);

        if (!info.IsName(currentAttack.animationName))
            return;

        float t = info.normalizedTime;

        // 공격 판정 구간
        if (t >= currentAttack.startUpEnd && t < currentAttack.activeEnd)
        {
            OnAttackActive();
        }

        // 콤보 입력 처리
        if (t >= currentAttack.activeEnd)
        {
            TryCombo();
        }

        // 애니 끝
        if (t >= 1f)
        {
            player.ChangeState(player.IdleState);
        }
    }

    private void TryCombo()
    {
        if (!attackInput || comboTriggered)
            return;

        int nextIndex = currentAttack.nextComboIndex;

        if (nextIndex < 0 || nextIndex >= player.normalCombo.Length)
            return;

        comboTriggered = true;

        comboIndex = nextIndex;

        StartAttack(player.normalCombo[comboIndex]);
    }

    private void StartAttack(AttackData data)
    {
        currentAttack = data;

        attackInput = false;
        comboTriggered = false;
        player.Animator.CrossFade(data.animationName, 0.05f);

        Debug.Log($"Start Attack : {data.animationName}");
    }

    private void OnAttackActive()
    {
        Debug.Log("판정 발생!");
        // TODO : 히트박스 활성화
    }

    public void Exit()
    {
        Debug.Log("Attack Exit");
    }

    public void HandleAttack()
    {
        attackInput = true;
    }

    public void HandleDodge()
    {
        player.ChangeState(player.DodgeState);
    }

    public void HandleHit()
    {
        player.ChangeState(player.HitState);
    }
}
