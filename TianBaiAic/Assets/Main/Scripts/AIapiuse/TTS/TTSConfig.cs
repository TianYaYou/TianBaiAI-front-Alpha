using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// TTS API 兼容模式。
/// 当前只真正实现 MIMO；OpenAICompatible/Custom 先作为后续扩展位保留。
/// </summary>
public enum TTSCompatibilityMode
{
    MIMO,
    OpenAICompatible,
    Custom
}

/// <summary>
/// TTS 配置数据模型。
/// 这个类只描述“配置文件里有哪些字段”，读取路径和校验入口放在 TTSConfigLoader。
/// 后续 WPF 启动器可以直接修改 StreamingAssets/AI/tts_config.json。
/// </summary>
[Serializable]
public class TTSConfig
{
    [Header("Base")]
    [Tooltip("启动器未配置完成前保持 false，避免 Unity 一启动就误触发真实 TTS 请求。")]
    public bool Enabled = false;
    public string Name = "Launcher_TTS";
    public TTSCompatibilityMode Mode = TTSCompatibilityMode.MIMO;
    public string ApiKey = "";

    [Header("Endpoints")]
    public string ApiHost = "https://token-plan-cn.xiaomimimo.com/v1";
    public string ApiPath = "/chat/completions";

    [Header("MIMO TTS")]
    public string Model = "mimo-v2.5-tts-voicedesign";
    public string AudioFormat = "wav";
    public int FallbackSampleRate = 24000;
    public int FallbackChannels = 1;

    [Header("Voice Design")]
    [TextArea(2, 5)]
    public string BaseVoiceDescription = "一个温柔、清澈、略带亲近感的少女声音，语速自然，语气像正在陪伴用户的桌宠。";
    public string DefaultEmotion = "自然";
    public bool AppendEmotionToDescription = true;

    public static TTSConfig CreateTemplate()
    {
        return new TTSConfig
        {
            Enabled = false,
            Name = "Launcher_TTS",
            Mode = TTSCompatibilityMode.MIMO,
            ApiKey = "PUT_MIMO_API_KEY_HERE",
            ApiHost = "https://token-plan-cn.xiaomimimo.com/v1",
            ApiPath = "/chat/completions",
            Model = "mimo-v2.5-tts-voicedesign",
            AudioFormat = "wav",
            FallbackSampleRate = 24000,
            FallbackChannels = 1,
            BaseVoiceDescription = "一个温柔、清澈、略带亲近感的少女声音，语速自然，语气像正在陪伴用户的桌宠。",
            DefaultEmotion = "自然",
            AppendEmotionToDescription = true
        };
    }

    public bool IsReady()
    {
        // 最低限度校验：启动器/配置文件必须明确启用，并填好地址、模型和 Key。
        return Enabled
               && !string.IsNullOrWhiteSpace(ApiHost)
               && !string.IsNullOrWhiteSpace(ApiPath)
               && !string.IsNullOrWhiteSpace(ApiKey)
               && ApiKey.IndexOf("PUT_", StringComparison.OrdinalIgnoreCase) < 0
               && !string.IsNullOrWhiteSpace(Model);
    }

    public string GetTtsEndpoint()
    {
        return BuildUrl(ApiHost, ApiPath);
    }

    public TTSSessionSettings BuildDefaultSessionSettings()
    {
        // 把配置文件转换成 TTSAiConnectApi 能直接使用的运行参数。
        return new TTSSessionSettings
        {
            Model = string.IsNullOrWhiteSpace(Model) ? "mimo-v2.5-tts-voicedesign" : Model,
            AudioFormat = string.IsNullOrWhiteSpace(AudioFormat) ? "wav" : AudioFormat,
            FallbackSampleRate = Mathf.Max(8000, FallbackSampleRate),
            FallbackChannels = Mathf.Max(1, FallbackChannels)
        };
    }

    private string BuildUrl(string host, string path)
    {
        if (string.IsNullOrEmpty(host)) return path;
        if (string.IsNullOrEmpty(path)) return host;
        return $"{host.TrimEnd('/')}/{path.TrimStart('/')}";
    }
}

/// <summary>
/// TTS 配置读取工具。
/// 默认读取 StreamingAssets/AI/tts_config.json；如果设置了 TIANBAI_TTS_CONFIG_PATH 环境变量，则优先读取环境变量路径。
/// </summary>
public static class TTSConfigLoader
{
    private const string DefaultConfigFileName = "AI/tts_config.json";
    private const string EnvVarName = "TIANBAI_TTS_CONFIG_PATH";

    public static bool TryLoad(out TTSConfig config, out string reason)
    {
        return TryLoadFromPath(GetConfigPath(), out config, out reason);
    }

    /// <summary>
    /// 从指定路径读取 TTS 配置。
    /// 这个方法给 Unity 内部和未来启动器共用，避免重复写解析逻辑。
    /// </summary>
    public static bool TryLoadFromPath(string path, out TTSConfig config, out string reason)
    {
        config = null;
        reason = null;

        if (!File.Exists(path))
        {
            reason = $"TTS config file not found: {path}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            config = JsonConvert.DeserializeObject<TTSConfig>(json);
            if (config == null)
            {
                reason = $"Failed to parse TTS config: {path}";
                return false;
            }

            if (!config.IsReady())
            {
                reason = "TTS config is not ready. Enable it and fill values in launcher/config first.";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            reason = $"Failed to read TTS config: {e.Message}";
            return false;
        }
    }

    public static string GetConfigPath()
    {
        string envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        return Path.Combine(Application.streamingAssetsPath, DefaultConfigFileName);
    }
}
