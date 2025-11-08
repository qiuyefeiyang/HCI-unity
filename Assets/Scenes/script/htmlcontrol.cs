using UnityEngine;
using Firebase;
using Firebase.Database;
using System.Collections;

public class FirebaseController : MonoBehaviour
{
    [Header("Firebase配置")]
    public string databaseUrl = "https://hci-unity-default-rtdb.firebaseio.com/";

    [Header("玩家控制器")]
    public PlayerController playerController;

    [Header("调试设置")]
    public bool enableDebug = true;

    private DatabaseReference databaseRef;
    private bool isFirebaseInitialized = false;

    // 从Firebase接收的数据
    private Vector2 remoteJoystickInput = Vector2.zero;
    private bool remoteInteract = false;

    void Start()
    {
        // 延迟初始化
        Invoke("InitializeFirebase", 2f);
    }

    void InitializeFirebase()
    {
        if (enableDebug)
            Debug.Log("开始初始化Firebase...");

        // 方法1：直接尝试初始化
        bool success = TryInitializeFirebase();

        if (!success)
        {
            if (enableDebug)
                Debug.Log("方法1失败，将在1秒后重试...");
            Invoke("InitializeFirebaseRetry", 1f);
        }
    }

    void InitializeFirebaseRetry()
    {
        if (enableDebug)
            Debug.Log("重试初始化Firebase...");

        bool success = TryInitializeFirebase();

        if (!success)
        {
            Debug.LogError("Firebase初始化失败，请检查配置");
        }
    }

    bool TryInitializeFirebase()
    {
        try
        {
            // 检查Firebase依赖
            if (FirebaseApp.DefaultInstance == null)
            {
                if (enableDebug)
                    Debug.LogWarning("FirebaseApp.DefaultInstance为null，尝试创建新实例");

                // 创建带有数据库URL的FirebaseApp
                var options = new AppOptions();
                SetDatabaseUrl(options, databaseUrl);
                FirebaseApp.Create(options);
            }

            // 明确指定数据库URL获取实例
            databaseRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrl).RootReference;

            // 设置数据监听
            SetupDataListeners();

            if (enableDebug)
                Debug.Log("Firebase初始化成功");

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Firebase初始化失败: {ex.Message}");
            return false;
        }
    }

    void SetDatabaseUrl(AppOptions options, string url)
    {
        // 尝试通过反射设置DatabaseUrl
        var type = options.GetType();

        // 尝试属性
        var property = type.GetProperty("DatabaseUrl");
        if (property != null && property.CanWrite)
        {
            property.SetValue(options, url);
            return;
        }

        // 尝试字段
        var field = type.GetField("DatabaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(options, url);
            return;
        }

        // 如果都不行，尝试其他可能的字段名
        field = type.GetField("databaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(options, url);
            return;
        }

        Debug.LogWarning("无法设置DatabaseUrl，将使用默认URL");
    }

    void SetupDataListeners()
    {
        try
        {
            // 明确指定URL获取引用
            var joystickRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrl)
                .GetReference("controller/joystick");

            var interactRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrl)
                .GetReference("controller/interact");

            // 监听摇杆数据变化
            joystickRef.ValueChanged += HandleJoystickDataChanged;

            // 监听交互按钮数据变化
            interactRef.ValueChanged += HandleInteractDataChanged;

            isFirebaseInitialized = true;

            if (enableDebug)
            {
                Debug.Log("Firebase数据监听器设置完成");
                Debug.Log("等待手机控制信号...");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"设置数据监听器失败: {ex.Message}");
        }
    }

    void HandleJoystickDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            if (enableDebug)
                Debug.LogError($"摇杆数据错误: {args.DatabaseError.Message}");
            return;
        }

        if (args.Snapshot.Exists && playerController != null)
        {
            try
            {
                // 解析数据
                var dict = args.Snapshot.Value as System.Collections.Generic.Dictionary<string, object>;
                if (dict != null && dict.ContainsKey("x") && dict.ContainsKey("y"))
                {
                    float x = System.Convert.ToSingle(dict["x"]);
                    float y = System.Convert.ToSingle(dict["y"]);

                    remoteJoystickInput = new Vector2(x, y);
                    playerController.SetMobileInput(remoteJoystickInput);

                    if (enableDebug && remoteJoystickInput.magnitude > 0.1f)
                        Debug.Log($"收到摇杆输入: ({x:F2}, {y:F2})");
                }
            }
            catch (System.Exception ex)
            {
                if (enableDebug)
                    Debug.LogError($"解析摇杆数据失败: {ex.Message}");
            }
        }
    }

    void HandleInteractDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            if (enableDebug)
                Debug.LogError($"交互数据错误: {args.DatabaseError.Message}");
            return;
        }

        if (args.Snapshot.Exists && playerController != null)
        {
            try
            {
                bool interact = false;

                if (args.Snapshot.Value is bool)
                {
                    interact = (bool)args.Snapshot.Value;
                }
                else if (args.Snapshot.Value is long)
                {
                    interact = (long)args.Snapshot.Value != 0;
                }
                else if (args.Snapshot.Value is int)
                {
                    interact = (int)args.Snapshot.Value != 0;
                }

                if (interact)
                {
                    remoteInteract = true;
                    playerController.SetMobileInteract(true);

                    if (enableDebug)
                        Debug.Log("收到交互指令 - 执行交互动作");
                }
                else
                {
                    remoteInteract = false;
                }
            }
            catch (System.Exception ex)
            {
                if (enableDebug)
                    Debug.LogError($"解析交互数据失败: {ex.Message}, 值: {args.Snapshot.Value}");
            }
        }
    }

    void Update()
    {
        // 每10秒检查一次状态（用于调试）
        if (enableDebug && Time.frameCount % 600 == 0 && isFirebaseInitialized)
        {
            Debug.Log($"Firebase状态: 输入=({remoteJoystickInput.x:F2}, {remoteJoystickInput.y:F2})");
        }
    }

    void OnDestroy()
    {
        // 清理监听器
        if (isFirebaseInitialized)
        {
            try
            {
                var joystickRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrl)
                    .GetReference("controller/joystick");

                var interactRef = FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrl)
                    .GetReference("controller/interact");

                joystickRef.ValueChanged -= HandleJoystickDataChanged;
                interactRef.ValueChanged -= HandleInteractDataChanged;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"清理监听器时出错: {ex.Message}");
            }
        }
    }

    // 获取连接状态
    public bool IsConnected()
    {
        return isFirebaseInitialized;
    }

    // 获取当前输入状态
    public string GetRemoteInputStatus()
    {
        return $"远程输入: ({remoteJoystickInput.x:F2}, {remoteJoystickInput.y:F2}), 交互: {remoteInteract}";
    }
}