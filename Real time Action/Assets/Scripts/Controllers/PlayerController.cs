using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

enum PlayerState
{
    Idle,
    Attack,
    Dodge,
    Hit
}
public class PlayerController : MonoBehaviour
{
    private CharacterController controller;

    [Header("Move")]
    private float moveSpeed = 6f;
    private Vector2 moveInput;

    [Header("State")]
    private PlayerState currentState;

    [Header("Attack Combo")]
    private int attackCount = 0;
    private float comboTimer;
    public float comboResetTime = 2f;

    [Header("Gravity")]
    private float yVelocity;
    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {

    }

    void Update()
    {
        switch (currentState)
        {
            case PlayerState.Idle:
                HandleMove();
                break;

            case PlayerState.Attack:
                break;

            case PlayerState.Dodge:
                break;

            case PlayerState.Hit:
                break;
        }
        HandleComboTimer();
    }

    #region Movement

    void HandleMove()
    {
        if (currentState != PlayerState.Idle) return;

        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        HandleGravity();
        move.y = yVelocity;

        controller.Move(move * moveSpeed * Time.deltaTime);
    }

    void HandleGravity()
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

    #endregion

    #region State
    void ChangeState(PlayerState newState)
    {
        if (currentState == newState)
            return;

        ExitState(currentState);

        Debug.Log($"State: {currentState} -> {newState}");
        currentState = newState;

        EnterState(newState);
    }

    private void EnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
                break;

            case PlayerState.Attack:
                attackCount = 1;
                Debug.Log("1타");
                Invoke(nameof(EndAttack), 0.5f);
                break;

            case PlayerState.Dodge:
                Debug.Log("회피!");
                Invoke(nameof(EndDodge), 0.3f);
                break;

            case PlayerState.Hit:
                Debug.Log("피격!!!");
                Invoke(nameof(EndHit), 0.4f);
                break;
        }
    }

    private void ExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attack:
                CancelInvoke(nameof(EndAttack));
                break;
            case PlayerState.Dodge:
                CancelInvoke(nameof(EndDodge));
                break;
            case PlayerState.Hit:
                CancelInvoke(nameof(EndHit));
                break;
        }
    }
    #endregion

    #region Input
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value) // 임시
    {
        if (currentState != PlayerState.Idle)
            return;

        ChangeState(PlayerState.Attack);
    }
    void HandleComboTimer()
    {
        if (comboTimer > 0)
        {
            comboTimer -= Time.deltaTime;
        }
        else
        {
            attackCount = 0;
        }
    }

    public void OnDodge(InputValue value) // 임시
    {
        if (currentState != PlayerState.Idle)
            return;

        ChangeState(PlayerState.Dodge);
    }

    public void OnHitTest(InputValue value)
    {
        TakeHit();
    }

    void TakeHit()
    {
        ChangeState(PlayerState.Hit);
    }

    void EndAttack()
    {
        ChangeState(PlayerState.Idle);
    }

    void EndDodge()
    {
        ChangeState(PlayerState.Idle);
    }

    void EndHit()
    {
        ChangeState(PlayerState.Idle);
    }
    #endregion
}
