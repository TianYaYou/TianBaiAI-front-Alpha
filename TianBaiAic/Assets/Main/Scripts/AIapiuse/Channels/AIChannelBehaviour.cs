using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// A/B API 通道的共享基类。
/// 负责读取 AIConfig、创建 AISession、把回调式 SendMessageAsync 包装成 Task。
/// </summary>
public abstract class AIChannelBehaviour : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("为空时使用 AIConfigLoader.GetConfigPath()。")]
    public string configPathOverride = "";

    [Tooltip("配置文件不存在时自动生成模板。")]
    public bool createTemplateIfMissing = true;

    [Header("Debug")]
    public bool logChannel = true;

    protected AIConfig Config { get; private set; }
    protected string LoadedConfigPath { get; private set; }

    protected bool TryLoadConfig(out string reason)
    {
        reason = null;
        if (Config != null) return true;

        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            if (createTemplateIfMissing)
            {
                CreateTemplateConfig(path);
            }

            reason = $"AI config file not found or template just created: {path}";
            return false;
        }

        if (!AIConfigLoader.TryLoadFromPath(path, out AIConfig config, out reason))
        {
            return false;
        }

        Config = config;
        LoadedConfigPath = path;
        if (logChannel) Debug.Log($"[{GetType().Name}] Loaded AI config: {LoadedConfigPath}");
        return true;
    }

    protected AISession CreateSession(string systemPrompt, bool jsonMode, bool passHistory, float temperature)
    {
        AISessionSettings settings = Config.BuildDefaultSessionSettings();
        settings.SystemPrompt = systemPrompt;
        settings.Stream = false;
        settings.EnableJsonMode = jsonMode;
        settings.PassHistory = passHistory;
        settings.Temperature = temperature;
        return new AISession(Config, settings);
    }

    protected Task<string> SendOnceAsync(AISession session, string userText, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();
        _ = session.SendMessageAsync(
            userText,
            onComplete: text => tcs.TrySetResult(text),
            onError: error => tcs.TrySetException(new InvalidOperationException(error)),
            cancellationToken: cancellationToken);
        return tcs.Task;
    }

    protected string GetConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(configPathOverride))
        {
            return configPathOverride;
        }

        return AIConfigLoader.GetConfigPath();
    }

    private static void CreateTemplateConfig(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonConvert.SerializeObject(AIConfig.CreateTemplate(), Formatting.Indented);
        File.WriteAllText(path, json);
        Debug.Log($"[AIChannel] Created AI config template: {path}");
    }
}
