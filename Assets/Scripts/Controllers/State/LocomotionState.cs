using UnityEngine;

public class LocomotionState : IPlayerState
{
    private enum LocomotionMode
    {
        Idle = 0,
        Walk = 1,
        Run = 2
    }

    private LocomotionMode currentMode;
    private float moveHoldTimer;
    private Vector3 lastMoveDirection;

    private const string LOCOMOTION_STATE = "Locomotion";
    private const string LOCOMOTION_PHASE_PARAM = "LocomotionPhase";

    private readonly PlayerController player;
    private CharacterData characterData;

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        characterData = player.CharacterData;

        currentMode = LocomotionMode.Idle;
        moveHoldTimer = 0f;
        lastMoveDirection = player.transform.forward;

        player.Animator.CrossFade(LOCOMOTION_STATE, 0.08f);
        player.Animator.SetFloat(LOCOMOTION_PHASE_PARAM, 0f);
        player.SetCurrentSpeed(0f);
    }

    public void Update()
    {

        // 임시로 이동 상태에서만 에너지가 자동 회복된다.
        player.RecoveryEnergyOverTime(player.EnergyRecoveryRate);

        Vector3 inputMoveDir = player.GetCameraRelativeMoveDirection();
        bool hasInput = player.MoveInput.sqrMagnitude > 0.0001f;

        if (hasInput)
        {
            // 속도가 아니라 입력 유지 시간으로 Walk에서 Run으로 넘어가 원작의 출발 템포를 만든다.
            moveHoldTimer += player.ActionDeltaTime;

            if (inputMoveDir.sqrMagnitude > 0.0001f)
            {
                lastMoveDirection = inputMoveDir;
                player.RotateToward(lastMoveDirection);
            }
        }
        else
        {
            moveHoldTimer = 0f;
        }

        if (!hasInput)
        {
            currentMode = LocomotionMode.Idle;
        }
        else if (moveHoldTimer < characterData.runEnterDelay)
        {
            currentMode = LocomotionMode.Walk;
        }
        else
        {
            currentMode = LocomotionMode.Run;
        }

        float targetSpeed = currentMode switch
        {
            LocomotionMode.Walk => characterData.walkSpeed,
            LocomotionMode.Run => characterData.runSpeed,
            _ => 0f
        };

        player.SetCurrentSpeed(targetSpeed);

        Vector3 move = currentMode == LocomotionMode.Idle
            ? Vector3.zero
            // 입력이 잠깐 흔들려도 마지막 유효 방향을 유지해 이동 방향이 튀지 않게 한다.
            : lastMoveDirection * targetSpeed;

        player.HandleGravity();
        move.y = player.YVelocity;

        player.Controller.Move(move * player.ActionDeltaTime);

        player.Animator.SetFloat(
            LOCOMOTION_PHASE_PARAM,
            (float)currentMode,
            0.08f,
            player.ActionDeltaTime
        );
    }

    public void Exit()
    {
    }

    #region Handle
    public void HandleAttack()
    {
        player.ChangeState(player.AttackState);
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
        player.ChangeState(player.SkillState);
    }
    public void HandleUltimate()
    {
        if (!player.CanUseUltimate) return;
        player.ChangeState(player.UltimateState);
    }
    public void HandleParry()
    {
        player.ChangeState(player.ParryState);
    }
    #endregion
}


