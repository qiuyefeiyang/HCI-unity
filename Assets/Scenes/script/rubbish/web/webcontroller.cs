using System;
using System.Net;
using System.Threading;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;

public class WebServer : MonoBehaviour
{
    [Header("服务器设置")]
    public int port = 8080;
    public bool autoStart = true;

    [Header("UI引用")]
    public DynamicQRGenerator qrGenerator;

    private HttpListener listener;
    private Thread serverThread;
    private bool isRunning = false;
    private string localIP = "127.0.0.1";
    private int connectedClients = 0;

    void Start()
    {
        // 自动获取QRGenerator引用（如果没手动设置）
        if (qrGenerator == null)
        {
            qrGenerator = GetComponent<DynamicQRGenerator>();
        }

        if (autoStart)
        {
            StartServer();
        }
    }

    public void StartServer()
    {
        try
        {
            // 使用更可靠的IP获取方法
            localIP = GetLocalIPAddress();
            Debug.Log($"获取到本地IP: {localIP}");

            listener = new HttpListener();

            // 使用安全的URL构建方式
            string prefix1 = "http://" + localIP + ":" + port + "/";
            string prefix2 = "http://localhost:" + port + "/";

            listener.Prefixes.Add(prefix1);
            listener.Prefixes.Add(prefix2);

            serverThread = new Thread(new ThreadStart(StartListener));
            serverThread.IsBackground = true;
            serverThread.Start();

            isRunning = true;
            Debug.Log($"Web服务器已启动: {prefix1}");

            // 更新UI状态
            if (qrGenerator != null)
            {
                qrGenerator.UpdateStatus($"服务器已启动: http://{localIP}:{port}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"启动服务器失败: {e.Message}");
            // 尝试备用方案
            StartServerFallback();
        }
    }

    void StartServerFallback()
    {
        try
        {
            Debug.Log("尝试备用启动方案...");

            listener = new HttpListener();
            // 只使用localhost
            listener.Prefixes.Add("http://localhost:" + port + "/");

            serverThread = new Thread(new ThreadStart(StartListener));
            serverThread.IsBackground = true;
            serverThread.Start();

            isRunning = true;
            localIP = "localhost";

            if (qrGenerator != null)
            {
                qrGenerator.UpdateStatus($"服务器已启动 (localhost): http://localhost:{port}");
            }

            Debug.Log("Web服务器已通过备用方案启动!");
        }
        catch (Exception e)
        {
            Debug.LogError($"备用启动方案也失败: {e.Message}");
            if (qrGenerator != null)
            {
                qrGenerator.UpdateStatus($"服务器启动失败: {e.Message}");
            }
        }
    }

    void StartListener()
    {
        try
        {
            listener.Start();
            Debug.Log("监听器已开始接收请求");

            while (isRunning)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ProcessRequest(context);
                }
                catch (Exception e)
                {
                    if (isRunning)
                        Debug.LogWarning($"处理请求时出错: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"监听器启动失败: {e.Message}");
        }
    }

    void ProcessRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            string responseString = "";
            string path = request.Url.LocalPath;

            if (path == "/" || path == "/index.html")
            {
                responseString = GetControlPageHTML();
                response.ContentType = "text/html; charset=utf-8";
            }
            else if (path == "/control")
            {
                // 处理控制指令
                if (request.HttpMethod == "POST")
                {
                    HandleControlCommand(request, response);
                    return;
                }
                else
                {
                    responseString = "{\"status\":\"error\",\"message\":\"只支持POST请求\"}";
                    response.ContentType = "application/json";
                }
            }
            else
            {
                responseString = "{\"status\":\"error\",\"message\":\"路径不存在\"}";
                response.ContentType = "application/json";
                response.StatusCode = 404;
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"处理请求异常: {e.Message}");
            response.StatusCode = 500;
            response.Close();
        }
    }

    void HandleControlCommand(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            // 读取POST数据
            System.IO.Stream body = request.InputStream;
            System.Text.Encoding encoding = request.ContentEncoding;
            System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
            string data = reader.ReadToEnd();
            body.Close();
            reader.Close();

            // 解析JSON数据
            ControlData controlData = JsonUtility.FromJson<ControlData>(data);

            // 处理控制指令
            MainThreadDispatcher.ExecuteOnMainThread(() => {
                ProcessControlData(controlData);
            });

            string responseString = "{\"status\":\"success\"}";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"处理控制命令失败: {e.Message}");
            response.StatusCode = 400;
            response.Close();
        }
    }

    void ProcessControlData(ControlData data)
    {
        // 查找玩家控制器并更新输入
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetMobileInput(new Vector2(data.joystickX, data.joystickY));

            if (data.interact)
            {
                player.SetMobileInteract(true);
            }

            Debug.Log($"手机控制: X={data.joystickX:F2}, Y={data.joystickY:F2}, 交互={data.interact}");

            // 更新连接状态
            connectedClients = Math.Max(1, connectedClients);
            if (qrGenerator != null)
            {
                qrGenerator.UpdateConnectionStatus(connectedClients);
            }
        }
        else
        {
            Debug.LogWarning("未找到PlayerController!");
        }
    }

    // 更可靠的获取IP地址方法
    public string GetLocalIPAddress()
    {
        try
        {
            // 方法1: 优先获取WiFi或以太网的IP
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                             ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                            !ni.Description.ToLower().Contains("virtual") &&
                            !ni.Description.ToLower().Contains("vpn") &&
                            !ni.Description.ToLower().Contains("radmin"))
                .ToList();

            foreach (var ni in networkInterfaces)
            {
                var ipInfo = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                         !IPAddress.IsLoopback(ip.Address) &&
                                         !ip.Address.ToString().StartsWith("169.254."));

                if (ipInfo != null)
                {
                    string ip = ipInfo.Address.ToString();
                    Debug.Log($"找到本地IP: {ip} (来自 {ni.Description})");
                    return ip;
                }
            }

            // 方法2: 备用方案 - 连接外部服务获取本地IP
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    string ip = endPoint.Address.ToString();
                    Debug.Log($"通过Socket获取到IP: {ip}");
                    return ip;
                }
            }
            catch
            {
                // 方法3: 最后备选
                string localHost = Dns.GetHostName();
                var hostEntry = Dns.GetHostEntry(localHost);
                var ipAddress = hostEntry.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork &&
                                         !IPAddress.IsLoopback(ip) &&
                                         !ip.ToString().StartsWith("169.254."));

                if (ipAddress != null)
                {
                    Debug.Log($"通过DNS获取到IP: {ipAddress}");
                    return ipAddress.ToString();
                }
            }

            Debug.LogWarning("无法获取本地IP，使用localhost");
            return "localhost";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取本地IP失败: {e.Message}");
            return "localhost";
        }
    }

    string GetControlPageHTML()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <title>古建筑展示控制器</title>
    <style>
        body {
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            font-family: Arial, sans-serif;
            touch-action: none;
            user-select: none;
            height: 100vh;
            overflow: hidden;
        }
        
        .container {
            max-width: 400px;
            margin: 0 auto;
            text-align: center;
        }
        
        h1 {
            font-size: 24px;
            margin-bottom: 30px;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }
        
        .status {
            background: rgba(255,255,255,0.2);
            padding: 10px;
            border-radius: 10px;
            margin-bottom: 20px;
            font-size: 14px;
        }
        
        .control-area {
            display: flex;
            justify-content: space-between;
            align-items: center;
            height: 200px;
        }
        
        .joystick-container {
            width: 150px;
            height: 150px;
            background: rgba(255,255,255,0.1);
            border-radius: 50%;
            position: relative;
            border: 2px solid rgba(255,255,255,0.3);
        }
        
        .joystick {
            width: 60px;
            height: 60px;
            background: rgba(255,255,255,0.9);
            border-radius: 50%;
            position: absolute;
            top: 45px;
            left: 45px;
            transition: all 0.1s;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .button-container {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }
        
        .action-button {
            width: 80px;
            height: 80px;
            background: rgba(255,255,255,0.2);
            border: 2px solid rgba(255,255,255,0.5);
            border-radius: 50%;
            color: white;
            font-size: 14px;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            transition: all 0.2s;
        }
        
        .action-button:active {
            background: rgba(255,255,255,0.4);
            transform: scale(0.95);
        }
        
        .active {
            background: rgba(76, 175, 80, 0.6) !important;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🏯 古建筑展示控制器</h1>
        <div class='status' id='status'>连接中...</div>
        
        <div class='control-area'>
            <div class='joystick-container' id='joystickContainer'>
                <div class='joystick' id='joystick'></div>
            </div>
            
            <div class='button-container'>
                <div class='action-button' id='interactBtn'>交互</div>
            </div>
        </div>
    </div>

    <script>
        class MobileController {
            constructor() {
                this.joystickContainer = document.getElementById('joystickContainer');
                this.joystick = document.getElementById('joystick');
                this.interactBtn = document.getElementById('interactBtn');
                this.status = document.getElementById('status');
                
                this.joystickX = 0;
                this.joystickY = 0;
                this.isInteracting = false;
                this.isConnected = false;
                
                this.serverUrl = window.location.origin;
                this.setupEventListeners();
                this.startHeartbeat();
            }
            
            setupEventListeners() {
                // 摇杆触摸事件
                this.joystickContainer.addEventListener('touchstart', this.handleTouchStart.bind(this));
                this.joystickContainer.addEventListener('touchmove', this.handleTouchMove.bind(this));
                this.joystickContainer.addEventListener('touchend', this.handleTouchEnd.bind(this));
                
                // 交互按钮事件
                this.interactBtn.addEventListener('touchstart', () => {
                    this.isInteracting = true;
                    this.interactBtn.classList.add('active');
                    this.sendControlData();
                });
                
                this.interactBtn.addEventListener('touchend', () => {
                    this.isInteracting = false;
                    this.interactBtn.classList.remove('active');
                    this.sendControlData();
                });
            }
            
            handleTouchStart(e) {
                e.preventDefault();
                this.updateJoystickPosition(e.touches[0]);
            }
            
            handleTouchMove(e) {
                e.preventDefault();
                this.updateJoystickPosition(e.touches[0]);
            }
            
            handleTouchEnd(e) {
                e.preventDefault();
                this.joystickX = 0;
                this.joystickY = 0;
                this.joystick.style.transform = 'translate(0px, 0px)';
                this.sendControlData();
            }
            
            updateJoystickPosition(touch) {
                const rect = this.joystickContainer.getBoundingClientRect();
                const centerX = rect.left + rect.width / 2;
                const centerY = rect.top + rect.height / 2;
                
                let deltaX = touch.clientX - centerX;
                let deltaY = touch.clientY - centerY;
                
                // 限制在圆形范围内
                const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
                const maxDistance = rect.width / 2 - 30;
                
                if (distance > maxDistance) {
                    deltaX = (deltaX / distance) * maxDistance;
                    deltaY = (deltaY / distance) * maxDistance;
                }
                
                // 更新摇杆位置
                this.joystick.style.transform = `translate(${deltaX}px, ${deltaY}px)`;
                
                // 计算标准化向量
                this.joystickX = deltaX / maxDistance;
                this.joystickY = -deltaY / maxDistance; // 反转Y轴
                
                this.sendControlData();
            }
            
            async sendControlData() {
                const controlData = {
                    joystickX: this.joystickX,
                    joystickY: this.joystickY,
                    interact: this.isInteracting
                };
                
                try {
                    const response = await fetch(this.serverUrl + '/control', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify(controlData)
                    });
                    
                    if (response.ok) {
                        this.isConnected = true;
                        this.status.textContent = '已连接 ✓';
                        this.status.style.background = 'rgba(76, 175, 80, 0.3)';
                    }
                } catch (error) {
                    this.isConnected = false;
                    this.status.textContent = '连接断开 ✗';
                    this.status.style.background = 'rgba(244, 67, 54, 0.3)';
                }
            }
            
            startHeartbeat() {
                setInterval(() => {
                    if (this.joystickX !== 0 || this.joystickY !== 0 || this.isInteracting) {
                        this.sendControlData();
                    }
                }, 100);
            }
        }
        
        // 初始化控制器
        document.addEventListener('DOMContentLoaded', () => {
            new MobileController();
        });
    </script>
</body>
</html>";
    }

    void OnDestroy()
    {
        isRunning = false;
        listener?.Stop();
        serverThread?.Abort();
    }
}

[System.Serializable]
public class ControlData
{
    public float joystickX;
    public float joystickY;
    public bool interact;
}