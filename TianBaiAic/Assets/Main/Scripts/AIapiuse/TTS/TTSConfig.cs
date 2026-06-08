using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// TTS 运行模式。
/// 之后 WPF 启动器只需要改配置文件里的 Mode，就能在远端 API 和本地 sherpa-onnx 之间切换。
/// </summary>
public enum TTSRunMode
{
    RemoteUrl = 0,
    LocalSherpaOnnx = 1
}

/// <summary>
/// 远端 TTS API 的协议类型。
/// 现在主要跑 MIMO；其他枚举先保留，方便以后接 OpenAI 兼容或自定义 URL。
/// </summary>
public enum TTSRemoteProtocol
{
    MIMO = 0,
    OpenAICompatible = 1,
    Custom = 2
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

    [Tooltip("RemoteUrl 使用远端 API；LocalSherpaOnnx 使用 StreamingAssets 里的本地 sherpa-onnx 模型。")]
    public TTSRunMode Mode = TTSRunMode.RemoteUrl;

    [Header("Remote API")]
    public TTSRemoteProtocol RemoteProtocol = TTSRemoteProtocol.MIMO;
    public string ApiKey = "";
    public string ApiHost = "https://token-plan-cn.xiaomimimo.com/v1";
    public string ApiPath = "/chat/completions";
    public string Model = "mimo-v2.5-tts-voicedesign";
    public string AudioFormat = "wav";
    public int FallbackSampleRate = 24000;
    public int FallbackChannels = 1;

    [Header("Local sherpa-onnx")]
    [Tooltip("相对于 Application.streamingAssetsPath 的模型目录。启动器也可以改成绝对路径。")]
    public string SherpaModelRoot = "sherpa-onnx/models/speech-synthesis/vits-melo-tts-zh_en";
    public string SherpaModelFile = "model.onnx";
    public string SherpaTokensFile = "tokens.txt";
    public string SherpaLexiconFile = "lexicon.txt";
    public string SherpaDictDir = "dict";
    public string[] SherpaRuleFsts = { "phone.fst", "date.fst", "number.fst", "new_heteronym.fst" };
    public string SherpaProvider = "cpu";
    public int SherpaNumThreads = 2;
    public bool SherpaDebug = false;
    public int SherpaSpeakerId = 0;
    public float SherpaSpeed = 1f;
    public int SherpaMaxNumSentences = 2;
    public float SherpaSilenceScale = 0.2f;
    public float SherpaNoiseScale = 0.667f;
    public float SherpaNoiseScaleW = 0.8f;
    public float SherpaLengthScale = 1f;

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
            Mode = TTSRunMode.RemoteUrl,
            RemoteProtocol = TTSRemoteProtocol.MIMO,
            ApiKey = "PUT_MIMO_API_KEY_HERE",
            ApiHost = "https://token-plan-cn.xiaomimimo.com/v1",
            ApiPath = "/chat/completions",
            Model = "mimo-v2.5-tts-voicedesign",
            AudioFormat = "wav",
            FallbackSampleRate = 24000,
            FallbackChannels = 1,
            SherpaModelRoot = "sherpa-onnx/models/speech-synthesis/vits-melo-tts-zh_en",
            SherpaModelFile = "model.onnx",
            SherpaTokensFile = "tokens.txt",
            SherpaLexiconFile = "lexicon.txt",
            SherpaDictDir = "dict",
            SherpaRuleFsts = new[] { "phone.fst", "date.fst", "number.fst", "new_heteronym.fst" },
            SherpaProvider = "cpu",
            SherpaNumThreads = 2,
            SherpaSpeakerId = 0,
            SherpaSpeed = 1f,
            SherpaMaxNumSentences = 2,
            SherpaSilenceScale = 0.2f,
            SherpaNoiseScale = 0.667f,
            SherpaNoiseScaleW = 0.8f,
            SherpaLengthScale = 1f,
            BaseVoiceDescription = "一个温柔、清澈、略带亲近感的少女声音，语速自然，语气像正在陪伴用户的桌宠。",
            DefaultEmotion = "自然",
            AppendEmotionToDescription = true
        };
    }

    public bool IsReady()
    {
        if (!Enabled)
        {
            return false;
        }

        // 本地模式不需要 API Key，只需要最基础的模型路径字段。
        if (Mode == TTSRunMode.LocalSherpaOnnx)
        {
            return !string.IsNullOrWhiteSpace(SherpaModelRoot)
                   && !string.IsNullOrWhiteSpace(SherpaModelFile)
                   && !string.IsNullOrWhiteSpace(SherpaTokensFile);
        }

        // 远端模式至少要有地址、模型和 Key；具体协议差异留给 TTSAiConnectApi 处理。
        return !string.IsNullOrWhiteSpace(ApiHost)
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

    public string ResolveSherpaPath(string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return string.Empty;
        }

        // 启动器未来如果给绝对路径，就不再拼 StreamingAssets。
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        return Path.Combine(Application.streamingAssetsPath, relativeOrAbsolutePath);
    }

    public string ResolveSherpaModelFile(string fileOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileOrPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(fileOrPath))
        {
            return fileOrPath;
        }

        return Path.Combine(ResolveSherpaPath(SherpaModelRoot), fileOrPath);
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
                reason = $"TTS config is not ready. Enabled={config.Enabled}, Mode={config.Mode}.";
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
