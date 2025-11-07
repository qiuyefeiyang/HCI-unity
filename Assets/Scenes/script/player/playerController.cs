using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 180f;
    public float acceleration = 10f; // 加速度
    public float deceleration = 15f; // 减速度

    [Header("动画设置")]
    public bool enableAnimation = true;

    [Header("调试设置")]
    public bool enableKeyboardTest = true;
    public bool fixModelDirection = false;
    public bool enableDebug = false;

    private Rigidbody rb;
    private Animator animator;
    private Vector3 moveDirection;
    private Vector2 inputDirection = Vector2.zero;
    private Vector2 currentInput = Vector2.zero; // 当前实际输入（用于平滑）
    private bool isMoving = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (rb != null)
        {
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (enableDebug)
        {
            Debug.Log($"PlayerController初始化 - Animator: {animator != null}");
        }
    }

    void Update()
    {
        if (!enableKeyboardTest) return;

        HandleKeyboardInput();

        // 更新动画状态
        UpdateAnimation();

        if (Input.GetKeyDown(KeyCode.Space))
            Interact();
    }

    void HandleKeyboardInput()
    {
        Vector2 targetInput = Vector2.zero;

        // 水平输入
        if (Input.GetKey(KeyCode.A))
            targetInput.x = -1;
        else if (Input.GetKey(KeyCode.D))
            targetInput.x = 1;

        // 垂直输入
        if (Input.GetKey(KeyCode.W))
            targetInput.y = 1;
        else if (Input.GetKey(KeyCode.S))
            targetInput.y = -1;

        // 对角线方向处理
        if (targetInput.x != 0 && targetInput.y != 0)
        {
            targetInput = targetInput.normalized;
        }

        // 平滑输入过渡
        if (targetInput.magnitude > 0.1f)
        {
            currentInput = Vector2.Lerp(currentInput, targetInput, acceleration * Time.deltaTime);
        }
        else
        {
            currentInput = Vector2.Lerp(currentInput, Vector2.zero, deceleration * Time.deltaTime);
        }

        SetMoveInput(currentInput);
    }

    void UpdateAnimation()
    {
        if (!enableAnimation || animator == null) return;

        // 检测是否在移动 - 使用输入方向和物理速度双重检测
        bool wasMoving = isMoving;
        isMoving = inputDirection.magnitude > 0.1f && rb.velocity.magnitude > 0.1f;

        // 设置IsWalking参数
        animator.SetBool("isWalk", isMoving);

        // 可选：设置移动速度参数，可用于控制动画播放速度
        // animator.SetFloat("MoveSpeed", inputDirection.magnitude);

        // 调试输出状态变化
        if (enableDebug && wasMoving != isMoving)
        {
            Debug.Log($"移动状态变化: {wasMoving} -> {isMoving}");
            Debug.Log($"输入大小: {inputDirection.magnitude:F2}, 速度大小: {rb.velocity.magnitude:F2}");
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        if (rb == null) return;

        moveDirection = new Vector3(inputDirection.x, 0, inputDirection.y).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 moveVelocity = moveDirection * moveSpeed;
            rb.velocity = new Vector3(moveVelocity.x, rb.velocity.y, moveVelocity.z);

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation;

                if (fixModelDirection)
                {
                    targetRotation = Quaternion.LookRotation(-moveDirection);
                }
                else
                {
                    targetRotation = Quaternion.LookRotation(moveDirection);
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
        }
    }

    public void SetMoveInput(Vector2 direction)
    {
        inputDirection = direction;
    }

    public void Interact()
    {
        Debug.Log("执行交互动作");

        // 触发交互动画（如果Animator中有Interact触发器）
        if (animator != null)
        {
            animator.SetTrigger("Interact");
        }
    }
}