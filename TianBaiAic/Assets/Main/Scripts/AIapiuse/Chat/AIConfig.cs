using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// API 兼容模式。
/// 目前主链路按 OpenAI Chat Completions 格式实现；Custom/Anthropic 先保留给后续扩展。
/// </summary>
public enum AICompatibilityMode
{
    OpenAI,
    Anthropic,
    Custom
}

/// <summary>
/// AI 配置数据模型。
/// 这个类只描述“配置文件里有哪些字段”，真正的读取路径和校验逻辑在 AIConfigLoader。
/// 之后 WPF 启动器可以直接修改 StreamingAssets/AI/ai_config.json，Unity 侧重新加载即可生效。
/// </summary>
[Serializable]
public class AIConfig
{
    [Header("Base")]
    [Tooltip("启动器未配置完成前保持 false，避免 Unity 一启动就误发请求。")]
    public bool Enabled = false;
    public string Name = "Launcher_AI";
    public AICompatibilityMode Mode = AICompatibilityMode.OpenAI;
    public string ApiKey = "";

    [Header("Endpoints")]
    public string ApiHost = "https://api.openai.com/v1";
    public string ApiPath = "/chat/completions";
    public string ModelsPath = "/models";

    [Header("Session Defaults")]
    public string DefaultModel = "gpt-4o-mini";
    public float DefaultTemperature = 0.7f;
    public bool DefaultStream = false;
    public bool DefaultPassHistory = true;
    [TextArea(2, 6)] public string DefaultSystemPrompt = "You are a helpful assistant.";

    [Header("Legacy Prompt")]
    [Tooltip("开启后会从 StreamingAssets 读取历史后端 prompt，而不是只使用 DefaultSystemPrompt。")]
    public bool UseSystemPromptFile = true;

    [Tooltip("相对于 Application.streamingAssetsPath 的 prompt 路径。")]
    public string SystemPromptFile = "AI/system_prompt.txt";

    [Tooltip("开启后要求模型返回旧后端 JSON 结构，并只把 response.content 显示到 Output。")]
    public bool UseLegacyJsonResponse = true;

    [Tooltip("包装用户输入时使用的名字，会组成：[当前时间...]名字: 用户内容。")]
    public string UserDisplayName = "Ink_bai";

    public List<string> ModelList = new List<string> { "gpt-4o", "gpt-3.5-turbo" };

    /// <summary>
    /// 创建默认配置模板。
    /// 启动器之后可以改同一个 JSON 文件；这里先给 Unity 一个可见、可编辑的文件结构。
    /// </summary>
    public static AIConfig CreateTemplate()
    {
        return new AIConfig
        {
            Enabled = false,
            Name = "Launcher_AI",
            Mode = AICompatibilityMode.OpenAI,
            ApiKey = "PUT_API_KEY_HERE",
            ApiHost = "https://api.openai.com/v1",
            ApiPath = "/chat/completions",
            ModelsPath = "/models",
            DefaultModel = "gpt-4o-mini",
            DefaultTemperature = 0.7f,
            DefaultStream = false,
            DefaultPassHistory = true,
            DefaultSystemPrompt = "You are TianBai, a gentle desktop companion. Reply briefly and warmly.",
            UseSystemPromptFile = true,
            SystemPromptFile = "AI/system_prompt.txt",
            UseLegacyJsonResponse = true,
            UserDisplayName = "Ink_bai"
        };
    }

    public bool IsReady()
    {
        // 最低限度校验：启动器/配置文件必须明确启用，并填好地址和 Key。
        return Enabled
               && !string.IsNullOrWhiteSpace(ApiHost)
               && !string.IsNullOrWhiteSpace(ApiPath)
               && !string.IsNullOrWhiteSpace(ApiKey);
    }

    public string GetChatEndpoint()
    {
        return BuildUrl(ApiHost, ApiPath);
    }

    public string GetModelsEndpoint()
    {
        return BuildUrl(ApiHost, ModelsPath);
    }

    public AISessionSettings BuildDefaultSessionSettings()
    {
        // 把配置文件转换成 AISession 能直接使用的运行参数。
        return new AISessionSettings
        {
            Model = string.IsNullOrWhiteSpace(DefaultModel) ? "gpt-4o-mini" : DefaultModel,
            Temperature = DefaultTemperature,
            Stream = DefaultStream,
            PassHistory = DefaultPassHistory,
            SystemPrompt = DefaultSystemPrompt
        };
    }

    private string BuildUrl(string host, string path)
    {
        if (string.IsNullOrEmpty(host)) return path;
        if (string.IsNullOrEmpty(path)) return host;
        return $"{host.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    public async Task<List<string>> TryFetchModelsAsync()
    {
        string url = GetModelsEndpoint();
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey.Trim()}");

        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();

            JObject data = JObject.Parse(jsonResponse);
            List<string> fetchedModels = new List<string>();
            foreach (var item in data["data"])
            {
                string modelId = item["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    fetchedModels.Add(modelId);
                }
            }

            ModelList = fetchedModels;
            Debug.Log($"Loaded {ModelList.Count} models from {url}");
            return ModelList;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to fetch models: {e.Message}");
            return null;
        }
    }
}

/// <summary>
/// AI 配置读取工具。
/// 默认读取 StreamingAssets/AI/ai_config.json；如果设置了 TIANBAI_AI_CONFIG_PATH 环境变量，则优先读环境变量路径。
/// </summary>
public static class AIConfigLoader
{
    private const string DefaultConfigFileName = "AI/ai_config.json";
    private const string EnvVarName = "TIANBAI_AI_CONFIG_PATH";

    public static bool TryLoad(out AIConfig config, out string reason)
    {
        config = null;
        reason = null;

        return TryLoadFromPath(GetConfigPath(), out config, out reason);
    }

    /// <summary>
    /// 从指定路径读取 AI 配置。
    /// 这个方法给 Unity 内部和未来启动器共用，避免重复写解析逻辑。
    /// </summary>
    public static bool TryLoadFromPath(string path, out AIConfig config, out string reason)
    {
        config = null;
        reason = null;

        if (!File.Exists(path))
        {
            reason = $"AI config file not found: {path}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            config = JsonConvert.DeserializeObject<AIConfig>(json);
            if (config == null)
            {
                reason = $"Failed to parse config: {path}";
                return false;
            }

            if (!config.IsReady())
            {
                reason = "AI config is not ready. Enable and fill values in launcher first.";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            reason = $"Failed to read AI config: {e.Message}";
            return false;
        }
    }

    public static string GetConfigPath()
    {
        // 预留给 WPF 启动器：启动器可以通过环境变量把配置文件放到统一的数据目录。
        string envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        return Path.Combine(Application.streamingAssetsPath, DefaultConfigFileName);
    }
}
