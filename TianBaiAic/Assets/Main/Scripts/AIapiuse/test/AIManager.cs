using System.Collections.Generic;
using UnityEngine;

public class AIManager : MonoBehaviour
{
    // 全局持有
    private AIConfig _config;
    private AISession _currentSession;

    async void Start()
    {
        // ==========================================
        // 1. 初始化配置 (完美还原你的截图设置)
        // ==========================================
        _config = new AIConfig
        {
            Name = "Mimo2",
            Mode = AICompatibilityMode.OpenAI,

            ApiKey = "tp-ss6pmliaikgischcnxx6tvppf8az6ao6r7quwm0pqidzmyiv",

            // 主机和路径分离
            ApiHost = "https://token-plan-sgp.xiaomimimo.com/v1",
            ApiPath = "/chat/completions",

            ModelList = new List<string> { "mimo-v2.5-pro", "mimo-v2.5" }
        };

        // 尝试获取模型列表 (如果这行报错 401 且你确定密钥没错，可以直接注释掉这行)
        // await _config.TryFetchModelsAsync(); 


        // ==========================================
        // 2. 初始化会话设置
        // ==========================================
        var settings = new AISessionSettings
        {
            Model = "mimo-v2.5", // 填写你要使用的模型
            Stream = false,
            PassHistory = false,
            SystemPrompt = "你是一个强大的 Unity 游戏开发助手。",
            IsReasoningModel = true

            // 因为名字带 pro，内部会自动识别为推理模型，拦截 Temperature 发送。
            // 如果你确信它需要被当做普通模型，可以在代码里或日后的可视化面板中处理。
        };

        // 建立实例即为一个新对话开始
        _currentSession = new AISession(_config, settings);


        // ==========================================
        // 3. 发送消息测试
        // ==========================================
        Debug.Log("开始发送请求...");
        await _currentSession.SendMessageAsync(
            userText: "你好，请用 C# 写一个简单的单例模式。",
            onStreamUpdate: (currentText) =>
            {
                // 流式输出中... (如果要在 Text UI 上显示，请确保在主线程更新)
                Debug.Log($"[流式接收中] {currentText}");
            },
            onComplete: (finalText) =>
            {
                Debug.Log($"[对话完成] {finalText}");

                // 测试保存功能
                _currentSession.SaveSession(Application.persistentDataPath + "/mimo_chat.json");
            },
            onError: (error) =>
            {
                Debug.LogError(error);
            }
        );
    }
}