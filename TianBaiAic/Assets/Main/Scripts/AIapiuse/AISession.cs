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
        string jsonResponse = await response.Content.ReadAsStringAsync();
        JObject json = JObject.Parse(jsonResponse);
        string replyText = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

        _history.Add(new ChatMessage("assistant", replyText));
        onComplete?.Invoke(replyText);
    }
}