using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// A/B 通道共用的 AI 配置读取工具。
/// 这里不改 AIConfigLoader，只在新多通道架构里复用同一套配置文件。
/// </summary>
public static class AIChannelConfigUtility
{
    private const string AiStreamingFolder = "AI";

    public static bool TryLoadConfig(string overridePath, bool createTemplateIfMissing, out AIConfig config, out string reason)
    {
        config = null;
        reason = null;

        string path = string.IsNullOrWhiteSpace(overridePath) ? AIConfigLoader.GetConfigPath() : overridePath;
        if (!File.Exists(path))
        {
            if (createTemplateIfMissing)
            {
                CreateTemplate(path);
            }

            reason = $"AI config file not found or template just created: {path}";
            return false;
        }

        return AIConfigLoader.TryLoadFromPath(path, out config, out reason);
    }

    /// <summary>
    /// 从 StreamingAssets/AI 读取指定通道自己的 prompt 文件。
    /// 注意：这里故意不读取 system_prompt.txt，避免历史 prompt 和新多通道 prompt 混在一起。
    /// </summary>
    public static string LoadPromptFromStreamingAssets(string promptFileName, string fallbackPrompt, bool logResult, string channelName)
    {
        if (string.IsNullOrWhiteSpace(promptFileName))
        {
            return fallbackPrompt;
        }

        string path = Path.Combine(Application.streamingAssetsPath, AiStreamingFolder, promptFileName);
        if (!File.Exists(path))
        {
            if (logResult)
            {
                Debug.LogWarning($"[AIChannelConfig] {channelName} prompt file not found, use fallback: {path}");
            }

            return fallbackPrompt;
        }

        string prompt = File.ReadAllText(path).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            if (logResult)
            {
                Debug.LogWarning($"[AIChannelConfig] {channelName} prompt file is empty, use fallback: {path}");
            }

            return fallbackPrompt;
        }

        if (logResult)
        {
            Debug.Log($"[AIChannelConfig] {channelName} prompt loaded: {path}");
        }

        return prompt;
    }

    public static AISessionSettings BuildSettings(AIConfig config, string systemPrompt, bool jsonMode, bool passHistory, float temperatureOffset = 0f)
    {
        AISessionSettings settings = config.BuildDefaultSessionSettings();
        settings.SystemPrompt = systemPrompt;
        settings.Stream = false;
        settings.EnableJsonMode = jsonMode;
        settings.PassHistory = passHistory;
        settings.Temperature = Mathf.Clamp01(settings.Temperature + temperatureOffset);
        return settings;
    }

    private static void CreateTemplate(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string json = JsonConvert.SerializeObject(AIConfig.CreateTemplate(), Formatting.Indented);
        File.WriteAllText(path, json);
        Debug.Log($"[AIChannelConfig] Created AI config template: {path}");
    }
}
