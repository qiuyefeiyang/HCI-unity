using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;

public class DynamicQRGenerator : MonoBehaviour
{
    [Header("UI引用")]
    public RawImage qrCodeImage;
    public Text statusText;
    public Text urlText;

    [Header("服务器引用")]
    public WebServer webServer;
    [Header("连接状态UI")]
    public Text connectionText;
    private string serverURL;

    void Start()
    {
        if (webServer == null)
            webServer = GetComponent<WebServer>();

        StartCoroutine(InitializeQRCode());
    }

    IEnumerator InitializeQRCode()
    {
        // 等待服务器启动
        yield return new WaitForSeconds(2f);

        if (webServer != null)
        {
            // 获取正确的本地IP
            string localIP = webServer.GetLocalIPAddress();
            serverURL = $"http://{localIP}:{webServer.port}";

            UpdateStatus($"服务器地址: {serverURL}");

            // 生成二维码
            yield return StartCoroutine(GenerateQRCode(serverURL));

            if (urlText != null)
            {
                urlText.text = $"扫描二维码连接\n或访问: {serverURL}";
            }
        }
    }

    IEnumerator GenerateQRCode(string url)
    {
        UpdateStatus("生成二维码中...");

        // 使用可靠的在线二维码API
        string[] qrAPIs = {
            $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={UnityWebRequest.EscapeURL(url)}",
            $"https://quickchart.io/qr?text={UnityWebRequest.EscapeURL(url)}&size=200",
            //$"http://api.qrserver.com/v1/create-qr-code/?data={UnityWebRequest.EscapeURL(url)}&size=200x200"
        };

        foreach (string apiUrl in qrAPIs)
        {
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(apiUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    if (qrCodeImage != null)
                    {
                        qrCodeImage.texture = texture;
                        UpdateStatus("✅ 二维码已生成！扫描连接");
                        yield break; // 成功则退出
                    }
                }
                else
                {
                    Debug.LogWarning($"二维码API失败: {apiUrl}, 错误: {www.error}");
                }
            }

            // 等待一下再尝试下一个API
            yield return new WaitForSeconds(0.5f);
        }

        // 所有API都失败，显示备用方案
        UpdateStatus("⚠️ 二维码生成失败，请手动输入URL");
        if (urlText != null)
        {
            urlText.text = $"请手动访问:\n{serverURL}\n(确保同一WiFi)";
        }

        // 创建简单的备用二维码
        CreateFallbackQRTexture();
    }

    void CreateFallbackQRTexture()
    {
        // 创建一个简单的纹理，至少有个视觉反馈
        Texture2D texture = new Texture2D(200, 200);
        Color[] pixels = new Color[200 * 200];

        // 创建简单的棋盘格图案
        for (int y = 0; y < 200; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                bool isBlack = (x / 20 + y / 20) % 2 == 0;
                pixels[y * 200 + x] = isBlack ? Color.black : Color.white;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        if (qrCodeImage != null)
        {
            qrCodeImage.texture = texture;
        }
    }

    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"QR状态: {message}");
    }

    public void OpenInBrowser()
    {
        if (!string.IsNullOrEmpty(serverURL))
        {
            Application.OpenURL(serverURL);
            UpdateStatus("已在浏览器中打开");
        }
    }
    public void UpdateConnectionStatus(int connectedClients)
    {
        string statusMessage = connectedClients > 0
            ? $"✅ {connectedClients} 个设备已连接"
            : "⚠️ 等待设备连接...";

        // 更新主状态文本
        if (statusText != null)
        {
            // 保持原有服务器状态，添加连接信息
            string baseStatus = statusText.text.Split('\n')[0]; // 获取第一行
            statusText.text = $"{baseStatus}\n{statusMessage}";
        }

        // 如果有专门的连接状态文本，更新它
        if (connectionText != null)
        {
            connectionText.text = statusMessage;
        }

        Debug.Log($"连接状态更新: {statusMessage}");
    }

    // 在DynamicQRGenerator中添加这个方法
    IEnumerator GenerateQRCodeLocal(string url)
    {
        UpdateStatus("使用本地二维码生成...");

        // 使用UnityWebRequest下载一个二维码生成服务
        string localQRGenerator = $"http://localhost:{webServer.port}/qrcode?text={UnityWebRequest.EscapeURL(url)}";

        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(localQRGenerator))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                if (qrCodeImage != null)
                {
                    qrCodeImage.texture = texture;
                    UpdateStatus("✅ 本地二维码已生成");
                }
            }
            else
            {
                // 最终备用：显示URL
                UpdateStatus("⚠️ 请手动输入URL访问");
                if (urlText != null)
                {
                    urlText.text = $"访问地址:\n{serverURL}";
                }
            }
        }
    }
}