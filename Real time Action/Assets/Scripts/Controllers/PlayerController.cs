using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private CharacterController controller;
    [Header("Move")]
    private float moveSpeed = 6f;
    private Vector2 moveInput;

    [Header("State")]
    public bool isAttacking;
    public bool isDodging;

    [Header("Attack Combo")]
    private int attackCount = 0;
    private float comboTimer;
    public float comboResetTime = 1f;

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
        HandleMove();
        HandleComboTimer();
    }

    #region Movement

    void HandleMove()
    {
        if (isAttacking || isDodging) return;

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
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value) // 임시
    {
        if (isDodging) return;

        if (!isAttacking)
        {
            isAttacking = true;
            attackCount = 1;
            Debug.Log("1타");
            Invoke(nameof(EndAttack), 0.5f);
        }
        else
        {
            if (comboTimer > 0)
            {
                attackCount++;
                Debug.Log(attackCount + "타");
                if (attackCount > 3) return;
            }
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

    void EndAttack()
    {
        isAttacking = false;
    }

    public void OnDodge(InputValue value) // 임시
    {
        if (isAttacking) return;
        if (isDodging) return;

        isDodging = true;
        Debug.Log("회피!");

        Invoke(nameof(EndDodge), 0.3f); 
    }

    void EndDodge()
    {
        isDodging = false;
    }
    #endregion
}
