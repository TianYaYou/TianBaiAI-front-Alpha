using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// B 通道输出的统一命令。
/// Live2D、记忆写入、好感度、工具队列、未来 mod 都通过这个结构交给 CommandRouter。
/// </summary>
[Serializable]
public class AICommand
{
    [JsonProperty("command_id")]
    public string CommandId;

    [JsonProperty("turn_id")]
    public string TurnId;

    [JsonProperty("type")]
    public string Type;

    [JsonProperty("target")]
    public string Target;

    [JsonProperty("timing")]
    public string Timing = "immediate";

    [JsonProperty("priority")]
    public int Priority = 50;

    [JsonProperty("payload")]
    public JObject Payload = new JObject();

    [JsonProperty("source_channel")]
    public string SourceChannel = "B";

    [JsonProperty("conflict_key")]
    public string ConflictKey;

    public void EnsureIds(string turnId)
    {
        if (string.IsNullOrWhiteSpace(CommandId)) CommandId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(TurnId)) TurnId = turnId;
    }

    public bool MatchesTiming(string timing)
    {
        if (string.IsNullOrWhiteSpace(timing)) return true;
        return string.Equals(Timing, timing, StringComparison.OrdinalIgnoreCase);
    }

    public string GetPayloadString(string key, string fallback = "")
    {
        if (Payload == null || string.IsNullOrWhiteSpace(key)) return fallback;
        JToken token = Payload[key];
        return token != null && token.Type != JTokenType.Null ? token.ToString() : fallback;
    }

    public int GetPayloadInt(string key, int fallback = 0)
    {
        if (Payload == null || string.IsNullOrWhiteSpace(key)) return fallback;
        JToken token = Payload[key];
        return token != null && int.TryParse(token.ToString(), out int value) ? value : fallback;
    }

    public List<string> GetPayloadStringList(string key)
    {
        var results = new List<string>();
        if (Payload == null || string.IsNullOrWhiteSpace(key)) return results;

        JToken token = Payload[key];
        if (token == null || token.Type == JTokenType.Null) return results;
        if (token.Type == JTokenType.Array)
        {
            foreach (JToken item in token)
            {
                string value = item?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) results.Add(value);
            }
        }
        else
        {
            string value = token.ToString();
            if (!string.IsNullOrWhiteSpace(value)) results.Add(value);
        }

        return results;
    }
}

/// <summary>
/// 命令执行结果。第一版主要用于日志；后续工具队列可以用它做状态回传。
/// </summary>
public class AICommandExecutionResult
{
    public bool Success;
    public string Message;

    public static AICommandExecutionResult Ok(string message = "")
    {
        return new AICommandExecutionResult { Success = true, Message = message };
    }

    public static AICommandExecutionResult Fail(string message)
    {
        return new AICommandExecutionResult { Success = false, Message = message };
    }
}

/// <summary>
/// 所有插件、工具、Live2D 控制器都实现这个接口，CommandRouter 只依赖接口分发。
/// </summary>
public interface IAICommandHandler
{
    IEnumerable<string> SupportedCommandTypes { get; }
    bool CanHandle(AICommand command);
    Task<AICommandExecutionResult> ExecuteAsync(AICommand command, AITurnContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Handler 基类。Unity Inspector 不能直接拖 interface，所以插件脚本可以继承这个 MonoBehaviour 基类。
/// </summary>
public abstract class AICommandHandlerBehaviour : MonoBehaviour, IAICommandHandler
{
    public abstract IEnumerable<string> SupportedCommandTypes { get; }

    public virtual bool CanHandle(AICommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Type)) return false;
        foreach (string type in SupportedCommandTypes)
        {
            if (string.Equals(type, command.Type, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public abstract Task<AICommandExecutionResult> ExecuteAsync(
        AICommand command,
        AITurnContext context,
        CancellationToken cancellationToken);
}
