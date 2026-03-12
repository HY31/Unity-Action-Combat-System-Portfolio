using UnityEngine;

public interface IPlayerState
{
    void Enter();
    void Update();
    void Exit();

    void HandleAttack();
    void HandleDodge();
    void HandleHit();
}
