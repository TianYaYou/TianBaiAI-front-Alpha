using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Chat Completions 的一条消息。
/// role 通常为 system/user/assistant，content 是实际文本。
/// </summary>
[Serializable]
public class ChatMessage
{
    [JsonProperty("role")]
    public string Role;

    [JsonProperty("content")]
    public string Content;

    public ChatMessage() { }
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}

/// <summary>
/// 单次 AI 会话的运行参数。
/// AIConfig 负责从 JSON 保存配置；AISessionSettings 负责告诉 AISession 这次请求怎么发。
/// </summary>
[Serializable]
public class AISessionSettings
{
    public string Model = "gpt-4o";

    [Header("普通模型参数")]
    public float Temperature = 0.7f;

    [Header("推理模型参数 (o1/pro/reasoning)")]
    public string ReasoningEffort = "medium";
    public bool UploadReasoningEffort = false;

    [Header("高级兼容性")]
    public bool IsReasoningModel = false; // 勾选后强制按推理模型处理（不传 Temperature）

    [Header("对话设置")]
    public bool EnableJsonMode = false;
    public bool Stream = true;
    public bool PassHistory = true;
    public string SystemPrompt = "You are a helpful assistant.";
}

/// <summary>
/// OpenAI 兼容 API 会话。
/// 负责维护上下文历史、拼接请求 payload、发送 HTTP 请求，并把流式/非流式结果通过回调交回上层。
/// 它不直接操作 UI；UI 更新由 WebDialog 和 AIConversationController 负责。
/// </summary>
public class AISession
{
    public AIConfig Config { get; private set; }
    public AISessionSettings Settings { get; private set; }

    private List<ChatMessage> _history;
    public bool IsGenerating { get; private set; }

    public AISession(AIConfig config, AISessionSettings settings = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config), "AIConfig 不能为空！");
        Settings = settings ?? new AISessionSettings();
        _history = new List<ChatMessage>();

        // system prompt 作为第一条历史消息保存，后续 PassHistory=true 时会一起发给模型。
        if (!string.IsNullOrEmpty(Settings.SystemPrompt))
        {
            _history.Add(new ChatMessage("system", Settings.SystemPrompt));
        }
    }

    public IReadOnlyList<ChatMessage> GetHistory() => _history.AsReadOnly();

    public void SaveSession(string filePath)
    {
        var sessionData = new { Settings = this.Settings, History = this._history };
        string json = JsonConvert.SerializeObject(sessionData, Formatting.Indented);
        File.WriteAllText(filePath, json);
        Debug.Log($"会话已保存至: {filePath}");
    }

    public void LoadSession(string filePath)
    {
        if (!File.Exists(filePath)) return;
        string json = File.ReadAllText(filePath);
        JObject data = JObject.Parse(json);
        Settings = data["Settings"].ToObject<AISessionSettings>();
        _history = data["History"].ToObject<List<ChatMessage>>();
        Debug.Log("会话读取成功");
    }

    public async Task SendMessageAsync(
        string userText,
        Action<string> onStreamUpdate = null,
        Action<string> onComplete = null,
        Action<string> onError = null,
        CancellationToken cancellationToken = default)
    {
        // 防止同一个会话同时发起多个请求，避免历史记录顺序错乱。
        if (IsGenerating)
        {
            onError?.Invoke("当前有正在生成的对话，请稍后再试。");
            return;
        }

        IsGenerating = true;
        _history.Add(new ChatMessage("user", userText));

        // 1. 构建历史记录
        List<ChatMessage> messagesToSend = Settings.PassHistory
            ? new List<ChatMessage>(_history)
            : new List<ChatMessage>
              {
                  new ChatMessage("system", Settings.SystemPrompt),
                  new ChatMessage("user", userText)
              };

        // 2. 构建基础 Payload
        var requestData = new JObject
        {
            ["model"] = Settings.Model,
            ["messages"] = JToken.FromObject(messagesToSend),
            ["stream"] = Settings.Stream
        };

        // 3. 动态参数隔离 (核心修复)
        string modelName = Settings.Model.ToLower();
        bool isReasoningModel = Settings.IsReasoningModel || modelName.Contains("o1") || modelName.Contains("o3") || modelName.Contains("reason") || modelName.Contains("pro");

        if (isReasoningModel)
        {
            // 推理模型：严格禁止 Temperature
            if (Settings.UploadReasoningEffort)
            {
                requestData["reasoning_effort"] = Settings.ReasoningEffort;
            }
        }
        else
        {
            // 普通模型：支持 Temperature
            requestData["temperature"] = Settings.Temperature;
        }

        if (Settings.EnableJsonMode)
        {
            requestData["response_format"] = new JObject { ["type"] = "json_object" };
        }

        string jsonPayload = requestData.ToString();
        string url = Config.GetChatEndpoint(); // 使用智能拼接路由

        // 当前实现使用 Bearer Token，兼容多数 OpenAI-like 服务。
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.ApiKey.Trim()}");
        StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (Settings.Stream)
            {
                await HandleStreamResponse(response, onStreamUpdate, onComplete, cancellationToken);
            }
            else
            {
                await HandleStandardResponse(response, onComplete, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke($"API 请求失败: {ex.Message}");
            _history.RemoveAt(_history.Count - 1); // 发送失败回退记录
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task HandleStreamResponse(HttpResponseMessage response, Action<string> onStreamUpdate, Action<string> onComplete, CancellationToken cancellationToken)
    {
        // 处理 SSE 格式的 data: ... 流式返回。
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        StringBuilder fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data: "))
            {
                string data = line.Substring(6);
                if (data == "[DONE]") break;

                try
                {
                    JObject json = JObject.Parse(data);
                    var delta = json["choices"]?[0]?["delta"]?["content"];
                    if (delta != null)
                    {
                        fullResponse.Append(delta.ToString());
                        onStreamUpdate?.Invoke(fullResponse.ToString());
                    }
                }
                catch (JsonException) { /* 忽略不完整的 JSON 片段 */ }
            }
        }

        _history.Add(new ChatMessage("assistant", fullResponse.ToString()));
        onComplete?.Invoke(fullResponse.ToString());
    }

    private async Task HandleStandardResponse(HttpResponseMessage response, Action<string> onComplete, CancellationToken cancellationToken)
    {
        // 非流式返回：一次性取 choices[0].message.content。
        string jsonResponse = await response.Content.ReadAsStringAsync();
        JObject json = JObject.Parse(jsonResponse);
        string replyText = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

        _history.Add(new ChatMessage("assistant", replyText));
        onComplete?.Invoke(replyText);
    }
}
