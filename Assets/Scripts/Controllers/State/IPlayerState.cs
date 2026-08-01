using UnityEngine;

// 피격·사망·컷신처럼 궁극기로도 취소할 수 없는 강제 행동 불능 상태를 표시한다.
public interface IUltimateBlockingState
{

}
public interface IPlayerState
{
    void Enter();
    void Update();
    void Exit();

    void HandleAttack();
    void HandleDodge();
    void HandleHit();
    void HandleSkill();
    void HandleUltimate();
    void HandleParry();
}


