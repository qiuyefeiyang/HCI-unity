using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class NetworkManager : MonoBehaviour
{
    [Header("网络设置")]
    public int port = 8888;

    private TcpListener tcpListener;
    private Thread tcpThread;
    private TcpClient connectedClient;
    private NetworkStream clientStream;

    private PlayerController playerController;
    private bool isConnected = false;

    void Start()
    {
        // 查找玩家对象
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerController = playerObject.GetComponent<PlayerController>();
        }

        if (playerController == null)
        {
            // 如果没找到，尝试查找任何有playerController的对象
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogError("未找到PlayerController! 请确保场景中有玩家对象。");
            return;
        }

        StartServer();
    }

    void StartServer()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            Debug.Log($"服务器启动，监听端口: {port}");

            tcpThread = new Thread(new ThreadStart(ListenForClients));
            tcpThread.IsBackground = true;
            tcpThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"启动服务器失败: {e.Message}");
        }
    }

    void ListenForClients()
    {
        while (true)
        {
            try
            {
                connectedClient = tcpListener.AcceptTcpClient();
                clientStream = connectedClient.GetStream();
                isConnected = true;
                Debug.Log("手机客户端已连接!");

                // 开始接收数据
                byte[] buffer = new byte[1024];
                while (connectedClient.Connected)
                {
                    int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        ProcessReceivedData(receivedData);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"客户端连接异常: {e.Message}");
                isConnected = false;
            }
        }
    }

    void ProcessReceivedData(string data)
    {
        try
        {
            // 数据格式: "move,x,y" 或 "interact"
            string[] parts = data.Split(',');

            if (parts[0] == "move" && parts.Length == 3)
            {
                float x = float.Parse(parts[1]);
                float y = float.Parse(parts[2]);

                // 在主线程中更新移动输入
                MainThreadDispatcher.ExecuteOnMainThread(() => {
                    if (playerController != null)
                        playerController.SetMoveInput(new Vector2(x, y));
                    else
                        Debug.LogWarning("PlayerController未找到!");
                });
            }
            else if (parts[0] == "interact")
            {
                // 处理交互
                MainThreadDispatcher.ExecuteOnMainThread(() => {
                    if (playerController != null)
                        playerController.Interact();
                    else
                        Debug.LogWarning("PlayerController未找到!");
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"处理数据失败: {e.Message}, 数据: {data}");
        }
    }

    void OnDestroy()
    {
        tcpListener?.Stop();
        tcpThread?.Abort();
        connectedClient?.Close();
    }
}