// 피격·사망·컷신처럼 궁극기로도 취소할 수 없는 강제 행동 불능 상태를 표시한다.
public interface IUltimateBlockingState
{
}
/// <summary>
/// 플레이어 행동 상태의 생명주기와 입력 진입점을 정의한다.
/// 현재 상태에서 허용하지 않는 입력 핸들러는 의도적으로 비워 둔다.
/// </summary>
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


