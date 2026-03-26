using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    public CharacterController Controller => controller;

    [Header("Reference")]
    [SerializeField] private Transform cameraYawPivot;
    public Transform CameraYawPivot => cameraYawPivot;

    [Header("Move")]
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float runThreshold = 4f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    public float MaxSpeed => maxSpeed;
    public float RunThreshold => runThreshold;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float RotationSpeed => rotationSpeed;

    public Vector2 MoveInput { get; private set; }
    public float CurrentSpeed { get; private set; }

    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundedGravity = -2f;
    private float yVelocity;
    public float YVelocity => yVelocity;

    [Header("State")]
    private IPlayerState currentState;

    public LocomotionState LocomotionState { get; private set; }
    public AttackState AttackState { get; private set; }
    public DodgeState DodgeState { get; private set; }
    public HitState HitState { get; private set; }

    [Header("Attack Combo")]
    public AttackData[] normalCombo;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 8f;
    public float DodgeSpeed => dodgeSpeed;

    [Header("Animation")]
    public Animator Animator { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Animator = GetComponent<Animator>();

        LocomotionState = new LocomotionState(this);
        AttackState = new AttackState(this);
        DodgeState = new DodgeState(this);
        HitState = new HitState(this);
    }

    private void Start()
    {
        ChangeState(LocomotionState);
    }

    private void Update()
    {
        currentState?.Update();
    }

    public void ChangeState(IPlayerState newState)
    {
        if (currentState == newState) return;

        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void HandleGravity()
    {
        if (controller.isGrounded)
        {
            if (yVelocity < 0f)
                yVelocity = groundedGravity;
        }
        else
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    public void UpdateSpeed(bool hasInput)
    {
        float targetSpeed = hasInput ? maxSpeed : 0f;
        float speedChangeRate = hasInput ? acceleration : deceleration;

        CurrentSpeed = Mathf.MoveTowards(
            CurrentSpeed,
            targetSpeed,
            speedChangeRate * Time.deltaTime
        );
    }

    public Vector3 GetCameraRelativeMoveDirection()
    {
        if (cameraYawPivot == null)
            return Vector3.zero;

        Vector3 forward = cameraYawPivot.forward;
        Vector3 right = cameraYawPivot.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * MoveInput.y + right * MoveInput.x;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        return moveDir;
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

    public void SetCurrentSpeed(float speed)
    {
        CurrentSpeed = speed;
    }

    #region Input
    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleAttack();
    }

    public void OnDodge(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleDodge();
    }

    public void OnHitTest(InputValue value)
    {
        if (value.isPressed)
            currentState?.HandleHit();
    }
    #endregion
}