using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    public CharacterController Controller => controller;

    [Header("Move")]
    private float moveSpeed = 6f;
    public Vector2 MoveInput { get; private set; }
    public float MoveSpeed => moveSpeed;

    [Header("Gravity")]
    private float yVelocity;
    public float YVelocity => yVelocity;

    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    [Header("State")]
    private IPlayerState currentState;
    
    // 상태 인스턴스 캐싱
    public IdleState IdleState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }

    //[Header("Attack Combo")]
    //private int attackCount = 0;
    //private float comboTimer;
    //public float comboResetTime = 2f;

    [Header("Dodge")]
    private float dodgeSpeed = 2f;
    public float DodgeSpeed => dodgeSpeed;
    

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        IdleState = new IdleState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);
    }

    void Start()
    {
        ChangeState(IdleState);
    }

    void Update()
    {
        currentState?.Update();
    }

    public void HandleGravity()
    {
        if (controller.isGrounded)
        {
            if (yVelocity < 0)
                yVelocity = groundedGravity;
        }
        else
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    public void ChangeState(IPlayerState newState)
    {
        if (currentState == newState) return;

        currentState?.Exit();
        currentState = newState;
        Debug.Log($"Current state = {currentState}");
        currentState.Enter();
    }
    #region Input
    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value) // 임시
    {
        ChangeState(AttackState);
    }

    public void OnDodge(InputValue value) // 임시
    {
        ChangeState(DodgeState);
    }

    public void OnHitTest(InputValue value)
    {
        TakeHit();
    }

    void TakeHit()
    {
        ChangeState(HitState);
    }

    void EndAttack()
    {
        ChangeState(IdleState);
    }

    void EndDodge()
    {
        ChangeState(IdleState);
    }

    void EndHit()
    {
        ChangeState(IdleState);
    }
    #endregion
}
