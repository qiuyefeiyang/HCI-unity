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

    [Header("输入设置")]
    public bool enableKeyboardInput = true;
    public bool enableMobileInput = true;

    [Header("调试设置")]
    public bool fixModelDirection = false;
    public bool enableDebug = false;

    private Rigidbody rb;
    private Animator animator;
    private Vector3 moveDirection;

    // 输入系统
    private Vector2 keyboardInput = Vector2.zero;
    private Vector2 mobileInput = Vector2.zero;
    private Vector2 currentInput = Vector2.zero; // 当前实际输入（用于平滑）
    private Vector2 finalInput = Vector2.zero;   // 最终输入（手机优先）

    private bool isMoving = false;
    private bool mobileInteract = false;
    private bool keyboardInteract = false;

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
        // 处理键盘输入
        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }

        // 合并输入（手机输入优先）
        CombineInputs();

        // 更新动画状态
        UpdateAnimation();

        // 处理交互输入
        HandleInteractInput();
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
            keyboardInput = Vector2.Lerp(keyboardInput, targetInput, acceleration * Time.deltaTime);
        }
        else
        {
            keyboardInput = Vector2.Lerp(keyboardInput, Vector2.zero, deceleration * Time.deltaTime);
        }

        // 键盘交互
        if (Input.GetKeyDown(KeyCode.Space))
        {
            keyboardInteract = true;
        }
    }

    void CombineInputs()
    {
        // 手机输入优先于键盘输入
        if (enableMobileInput && mobileInput.magnitude > 0.1f)
        {
            finalInput = mobileInput;
            Debug.Log("手机正在输入");
        }
        else
        {
            finalInput = keyboardInput;
        }

        // 平滑最终输入
        if (finalInput.magnitude > 0.1f)
        {
            currentInput = Vector2.Lerp(currentInput, finalInput, acceleration * Time.deltaTime);
        }
        else
        {
            currentInput = Vector2.Lerp(currentInput, Vector2.zero, deceleration * Time.deltaTime);
        }

        if (enableDebug && finalInput.magnitude > 0)
        {
            Debug.Log($"最终输入: {finalInput}, 当前输入: {currentInput}, 手机输入: {mobileInput}, 键盘输入: {keyboardInput}");
        }
    }

    void HandleInteractInput()
    {
        // 手机交互优先
        if (mobileInteract)
        {
            Interact();
            mobileInteract = false; // 重置
        }
        else if (keyboardInteract)
        {
            Interact();
            keyboardInteract = false; // 重置
        }
    }

    void UpdateAnimation()
    {
        if (!enableAnimation || animator == null) return;

        // 检测是否在移动
        bool wasMoving = isMoving;
        isMoving = currentInput.magnitude > 0.1f;

        // 设置IsWalking参数
        animator.SetBool("isWalk", isMoving);

        // 调试输出状态变化
        if (enableDebug && wasMoving != isMoving)
        {
            Debug.Log($"移动状态变化: {wasMoving} -> {isMoving}");
            Debug.Log($"输入大小: {currentInput.magnitude:F2}");
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        if (rb == null) return;

        moveDirection = new Vector3(currentInput.x, 0, currentInput.y).normalized;

        if (currentInput.magnitude >= 0.1f)
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

    // 手机控制调用这个方法
    public void SetMobileInput(Vector2 direction)
    {
        mobileInput = direction;
    }

    // 手机交互调用这个方法
    public void SetMobileInteract(bool interact)
    {
        mobileInteract = interact;
    }

    // 保持向后兼容
    public void SetMoveInput(Vector2 direction)
    {
        SetMobileInput(direction);
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

    // 获取当前输入状态（用于调试）
    public string GetInputStatus()
    {
        return $"键盘: {keyboardInput}, 手机: {mobileInput}, 最终: {finalInput}";
    }
}