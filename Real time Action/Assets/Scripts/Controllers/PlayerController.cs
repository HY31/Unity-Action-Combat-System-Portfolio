using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    public CharacterController Controller => controller;

    [Header("Move")]
    float maxSpeed = 10f;
    float currentSpeed = 0f;
    float acceleration = 15f;
    float deceleration = 20f;
    public Vector2 MoveInput { get; private set; }
    public float MaxSpeed => maxSpeed;
    public float CurrentSpeed => currentSpeed;
    public float Deceleration => deceleration;
    public float Acceleration => acceleration;
    

    [Header("Gravity")]
    private float yVelocity;
    public float YVelocity => yVelocity;

    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    [Header("Reference")]
    [SerializeField] private Transform cameraTransform;
    public Transform CameraTransform => cameraTransform;

    [Header("Rotate")]
    [SerializeField] private float rotationSpeed = 12f;
    public float RotationSpeed => rotationSpeed;

    [Header("State")]
    private IPlayerState currentState;
    
    // 상태 인스턴스 캐싱
    public LocomotionState IdleState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }

    [Header("Attack Combo")]
    public AttackData[] normalCombo;

    [Header("Dodge")]
    private float dodgeSpeed = 2f;
    public float DodgeSpeed => dodgeSpeed;

    [Header("Animate")]
    public Animator Animator { get; private set; }
    

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Animator = GetComponent<Animator>();

        IdleState = new LocomotionState(this);
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

    public void UpdateSpeed(bool hasInput)
    {
        if (hasInput)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                maxSpeed,
                acceleration * Time.deltaTime
            );
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                0,
                deceleration * Time.deltaTime
            );
        }
    }

    public Vector3 GetCameraRelativeMoveDirection()
    {
        if (cameraTransform == null)
            return Vector3.zero;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * MoveInput.y + right * MoveInput.x;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        return move;
    }

    public void RotateToward(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
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
        if(value.isPressed)
        {
            currentState?.HandleAttack();
        }
    }

    public void OnDodge(InputValue value) // 임시
    {
        if (value.isPressed)
        {
            currentState?.HandleDodge();
        }
    }

    public void OnHitTest(InputValue value)
    {
        if (value.isPressed)
        {
            currentState?.HandleHit();
        }
    }

    void TakeHit()
    {
        ChangeState(HitState);
    }
    #endregion
}