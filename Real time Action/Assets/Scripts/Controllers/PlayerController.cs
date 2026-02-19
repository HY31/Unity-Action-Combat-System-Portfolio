using UnityEngine;
using UnityEngine.InputSystem;

enum PlayerState
{
    Idle,
    Move,
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
            case PlayerState.Move:
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

    #region Input
    void ChangeState(PlayerState newState)
    {
        Debug.Log($"State: {currentState} -> {newState}");
        currentState = newState;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value) // 임시
    {
        if (currentState != PlayerState.Idle && currentState != PlayerState.Move)
            return;

        ChangeState(PlayerState.Attack);

        attackCount = 1;
        Debug.Log("1타");
        Invoke(nameof(EndAction), 0.5f);

        if (comboTimer > 0)
        {
            attackCount++;
            Debug.Log(attackCount + "타");
            if (attackCount > 4) return;
        }

        comboTimer = comboResetTime;
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
        if (currentState != PlayerState.Idle && currentState != PlayerState.Move) return;

        ChangeState(PlayerState.Dodge);

        Debug.Log("회피!");

        Invoke(nameof(EndAction), 0.3f);
    }

    public void OnHitTest(InputValue value)
    {
        TakeHit();
    }

    void TakeHit()
    {
        if (currentState == PlayerState.Hit) return;

        ChangeState(PlayerState.Hit) ;
        Debug.Log("피격!!!");

        Invoke(nameof(EndAction), 0.4f);
    }

    void EndAction()
    {
        ChangeState(PlayerState.Idle);
    }
    #endregion
}
