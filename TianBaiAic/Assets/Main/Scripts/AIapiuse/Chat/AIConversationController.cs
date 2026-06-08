using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 场景中的 AI 对话控制器。
/// 负责读取配置文件、加载历史 system_prompt、调用 AISession，并把 AI 返回内容送到 Output。
/// </summary>
public class AIConversationController : MonoBehaviour
{
    public static AIConversationController Instance { get; private set; }

    [Header("Config")]
    [Tooltip("为空时使用 AIConfigLoader.GetConfigPath()。之后启动器可以写这个路径，或用环境变量指定新路径。")]
    public string configPathOverride = "";

    [Tooltip("配置文件不存在时自动生成模板，方便第一次运行时看到应该填写哪些字段。")]
    public bool createTemplateIfMissing = true;

    [Header("Debug")]
    public bool logConversation = true;

    private AISession _session;
    private AIConfig _config;
    private string _loadedConfigPath;

    /// <summary>
    /// 最近一次完整解析后的历史 JSON 响应。
    /// 后续要接 emotion、movement、actions、memory 时，可以直接从这里取。
    /// </summary>
    public LegacyDialogEnvelope LastResponse { get; private set; }

    void Awake()
    {
        // Unity 进入 Play 或脚本重载后，静态字段可能丢失；Awake 里重新登记当前实例。
        Instance = this;
    }

    /// <summary>
    /// 启动器修改配置后调用这个方法，让下一次对话重新读取配置和 prompt。
    /// </summary>
    public void ReloadConfig()
    {
        _session = null;
        _config = null;
        _loadedConfigPath = null;
        LastResponse = null;
    }

    /// <summary>
    /// 对外统一入口：传入用户文本，返回值表示请求是否成功开始。
    /// </summary>
    public static bool TryAsk(
        string userText,
        Action<string> onStreamUpdate = null,
        Action<string> onComplete = null,
        Action<string> onError = null)
    {
        var controller = GetOrCreateInstance();
        if (controller == null)
        {
            Debug.LogError("[AIConversation] No controller available.");
            return false;
        }

        return controller.TryAskInternal(userText, onStreamUpdate, onComplete, onError);
    }

    private static AIConversationController GetOrCreateInstance()
    {
        // 场景里已经有控制器时直接复用；没有时临时创建一个，避免按钮/语音调用空引用。
        if (Instance != null) return Instance;

        Instance = FindObjectOfType<AIConversationController>();
        if (Instance != null) return Instance;

        var go = new GameObject("AIConversationController");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AIConversationController>();
        return Instance;
    }

    private bool TryAskInternal(
        string userText,
        Action<string> onStreamUpdate,
        Action<string> onComplete,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            Debug.LogWarning("[AIConversation] Empty user text, request skipped.");
            return false;
        }

        if (!TryEnsureSession(out string reason))
        {
            Debug.LogWarning($"[AIConversation] Config not ready: {reason}");
            onError?.Invoke(reason);
            return false;
        }

        string requestText = BuildUserInput(userText);
        if (logConversation) Debug.Log($"[AIConversation] User payload: {requestText}");

        _ = AskAsync(requestText, onStreamUpdate, onComplete, onError);
        return true;
    }

    private bool TryEnsureSession(out string reason)
    {
        // 懒加载：第一次真正对话时才读取配置和 prompt，启动器改完配置后可以 ReloadConfig 再重建。
        reason = null;
        if (_session != null) return true;

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

        if (!AIConfigLoader.TryLoadFromPath(path, out _config, out reason))
        {
            return false;
        }

        AISessionSettings settings = _config.BuildDefaultSessionSettings();
        ApplyLegacyPromptSettings(_config, settings);

        // AISession 只负责请求本身；历史 prompt、JSON 模式这些项目需求在这里装配。
        _session = new AISession(_config, settings);
        _loadedConfigPath = path;
        Debug.Log($"[AIConversation] Loaded config: {_loadedConfigPath}");
        return true;
    }

    private async Task AskAsync(
        string requestText,
        Action<string> onStreamUpdate,
        Action<string> onComplete,
        Action<string> onError)
    {
        await _session.SendMessageAsync(
            requestText,
            onStreamUpdate: text =>
            {
                // 历史 prompt 要求完整 JSON，流式片段通常无法反序列化；非 JSON 模式才把流式文本送 UI。
                if (_config == null || !_config.UseLegacyJsonResponse)
                {
                    if (logConversation) Debug.Log($"[AIConversation] Stream: {text}");
                    onStreamUpdate?.Invoke(text);
                }
            },
            onComplete: text =>
            {
                string reply = ExtractOutputText(text);
                if (logConversation) Debug.Log($"[AIConversation] Output: {reply}");
                onComplete?.Invoke(reply);
            },
            onError: error =>
            {
                Debug.LogError($"[AIConversation] Request failed: {error}");
                onError?.Invoke(error);
            });
    }

    private void ApplyLegacyPromptSettings(AIConfig config, AISessionSettings settings)
    {
        // 沿用旧 Python 后端的 system_prompt.txt：它会要求模型返回固定 JSON 结构。
        if (!config.UseSystemPromptFile) return;

        string promptPath = Path.Combine(Application.streamingAssetsPath, config.SystemPromptFile);
        if (!File.Exists(promptPath))
        {
            Debug.LogWarning($"[AIConversation] System prompt file not found: {promptPath}");
            return;
        }

        settings.SystemPrompt = File.ReadAllText(promptPath);

        // 历史 prompt 要求返回 JSON。为了稳定解析，这里关闭流式输出并打开 JSON 模式。
        if (config.UseLegacyJsonResponse)
        {
            settings.Stream = false;
            settings.EnableJsonMode = true;
        }

        Debug.Log($"[AIConversation] Loaded system prompt: {promptPath}");
    }

    private string BuildUserInput(string userText)
    {
        // 旧 prompt 里用户消息带时间和用户名，这里保持原格式，减少 prompt 迁移风险。
        if (_config == null || !_config.UseLegacyJsonResponse)
        {
            return userText;
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string userName = string.IsNullOrWhiteSpace(_config.UserDisplayName) ? "Ink_bai" : _config.UserDisplayName;
        return $"[当前时间{timestamp}]{userName}: {userText}";
    }

    private string ExtractOutputText(string rawText)
    {
        // 旧逻辑只把 response.content 显示给用户；其它字段保存在 LastResponse，后续给表情/动作/记忆系统用。
        if (_config == null || !_config.UseLegacyJsonResponse)
        {
            return string.IsNullOrWhiteSpace(rawText) ? "(empty response)" : rawText;
        }

        if (TryParseLegacyResponse(rawText, out LegacyDialogEnvelope parsed))
        {
            LastResponse = parsed;
            AIResponseDispatcher.Dispatch(parsed.response);
            string content = parsed.response != null ? parsed.response.content : null;
            return string.IsNullOrWhiteSpace(content) ? "(empty response content)" : content;
        }

        Debug.LogWarning($"[AIConversation] Failed to parse legacy JSON, raw response: {rawText}");
        return string.IsNullOrWhiteSpace(rawText) ? "(empty response)" : rawText;
    }

    private static bool TryParseLegacyResponse(string rawText, out LegacyDialogEnvelope parsed)
    {
        parsed = null;
        string json = ExtractJsonObject(rawText);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            parsed = JsonConvert.DeserializeObject<LegacyDialogEnvelope>(json);
            return parsed != null && parsed.response != null;
        }
        catch (JsonException e)
        {
            Debug.LogWarning($"[AIConversation] Legacy JSON parse failed: {e.Message}");
            return false;
        }
    }

    private static string ExtractJsonObject(string rawText)
    {
        // 模型偶尔会把 JSON 包在 ```json ... ``` 中，这里尽量把真正的 JSON 对象截出来。
        if (string.IsNullOrWhiteSpace(rawText)) return null;

        string text = rawText.Trim();
        if (text.StartsWith("```"))
        {
            int firstBraceInFence = text.IndexOf('{');
            int lastBraceInFence = text.LastIndexOf('}');
            if (firstBraceInFence >= 0 && lastBraceInFence > firstBraceInFence)
            {
                return text.Substring(firstBraceInFence, lastBraceInFence - firstBraceInFence + 1);
            }
        }

        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace) return null;
        return text.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private string GetConfigPath()
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

        var config = AIConfig.CreateTemplate();
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
        Debug.Log($"[AIConversation] Created AI config template: {path}");
    }
}

/// <summary>
/// 历史 prompt 规定的最外层返回结构：{"response": {...}}。
/// </summary>
[Serializable]
public class LegacyDialogEnvelope
{
    public LegacyDialogResponse response;
}

/// <summary>
/// 历史后端使用的 AI 回复结构。
/// 目前 UI 只使用 content；emotion、movement、memory、actions 会保留给后续动作/记忆系统。
/// </summary>
[Serializable]
public class LegacyDialogResponse
{
    public string content;
    public string emotion;
    public string movement;
    public float? favorability;
    public LegacyReadMemory readmemory;
    public LegacyWriteMemory writememory;
    public List<string> actions;
}

[Serializable]
public class LegacyReadMemory
{
    public string time;
    public string key;
    public string content_key;
}

[Serializable]
public class LegacyWriteMemory
{
    public string time;
    public List<string> key;
    public string content;
}
