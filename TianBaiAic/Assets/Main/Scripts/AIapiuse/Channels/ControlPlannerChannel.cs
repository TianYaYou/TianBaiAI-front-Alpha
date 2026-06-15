using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// B 通道默认实现：分析用户输入和上下文，输出 control_tags + commands JSON。
/// 它不负责聊天文本，避免拖慢 A 通道。
/// </summary>
public class ControlPlannerChannel : MonoBehaviour, IAIControlPlannerChannel
{
    [Header("Config")]
    public string configPathOverride = "";
    public bool createTemplateIfMissing = true;

    [Header("Prompt")]
    [Tooltip("B 通道 prompt 文件名，位于 StreamingAssets/AI。不要和历史 system_prompt.txt 混用。")]
    public string promptFileName = "control_planner_prompt.txt";

    [TextArea(10, 20)]
    public string plannerSystemPrompt =
        "你是天白桌宠系统的控制规划器 B。\n" +
        "你不负责聊天，不要输出自然语言解释。\n" +
        "你只分析用户输入、A通道回复、记忆和可用命令，然后输出严格 JSON。\n" +
        "如果用户提到'以前'、'之前'、'还记得'、'我喜欢的'、'上次那个'、长期偏好或历史项目，请设置 requires_memory=true。\n" +
        "如果需要动作或表情，请输出 commands。\n" +
        "可用 command type 包括：live2d_expression, live2d_motion, memory_write。\n" +
        "timing 可用：immediate, after_dialogue, background。\n" +
        "只输出如下 JSON 对象，不要 markdown：\n" +
        "{\"control_tags\":[\"memory\",\"live2d_expression\"],\"requires_memory\":false,\"memory_query\":\"\",\"should_refine_dialogue\":false,\"should_refine_actions\":false,\"commands\":[]}";

    [Header("Debug")]
    public bool logPlanner = true;

    private AIConfig _config;
    private AISession _session;

    public async Task<AIControlPlan> GenerateControlPlanAsync(AITurnContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (!TryEnsureSession(out string reason)) throw new InvalidOperationException(reason);

        string payload = BuildPlannerPayload(context);
        if (logPlanner) Debug.Log($"[ControlPlannerChannel] Start B channel. phase={context.Phase}");

        string raw = await SendOnceAsync(payload, cancellationToken);
        AIControlPlan plan = ParsePlan(raw, context);
        plan.Phase = context.Phase;
        plan.TurnId = context.TurnId;
        NormalizeCommands(plan, context);

        if (logPlanner)
        {
            Debug.Log($"[ControlPlannerChannel] tags={string.Join(",", plan.ControlTags ?? new System.Collections.Generic.List<string>())}, commands={plan.Commands?.Count ?? 0}, requiresMemory={plan.RequiresMemory}");
        }

        return plan;
    }

    private bool TryEnsureSession(out string reason)
    {
        reason = null;
        if (_session != null) return true;

        if (!AIChannelConfigUtility.TryLoadConfig(configPathOverride, createTemplateIfMissing, out _config, out reason))
        {
            return false;
        }

        AISessionSettings settings = AIChannelConfigUtility.BuildSettings(
            _config,
            AIChannelConfigUtility.LoadPromptFromStreamingAssets(promptFileName, plannerSystemPrompt, logPlanner, "B/control_planner"),
            jsonMode: true,
            passHistory: false,
            temperatureOffset: -0.4f);

        _session = new AISession(_config, settings);
        return true;
    }

    private Task<string> SendOnceAsync(string userText, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();
        _ = _session.SendMessageAsync(
            userText,
            onComplete: text => tcs.TrySetResult(text),
            onError: error => tcs.TrySetException(new InvalidOperationException(error)),
            cancellationToken: cancellationToken);
        return tcs.Task;
    }

    private static string BuildPlannerPayload(AITurnContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"turn_id: {context.TurnId}");
        builder.AppendLine($"phase: {context.Phase}");
        builder.AppendLine($"user_input: {context.UserInput}");

        if (context.FastDialogueResult != null && !string.IsNullOrWhiteSpace(context.FastDialogueResult.Text))
        {
            builder.AppendLine($"a_dialogue_text: {context.FastDialogueResult.Text}");
        }

        string memoryText = context.BuildMemoryText();
        if (!string.IsNullOrWhiteSpace(memoryText))
        {
            builder.AppendLine("memory_items:");
            builder.AppendLine(memoryText);
        }

        if (context.AvailableCommandTypes != null && context.AvailableCommandTypes.Count > 0)
        {
            builder.AppendLine($"available_command_types: {string.Join(", ", context.AvailableCommandTypes)}");
        }

        builder.AppendLine("输出要求：只输出 JSON 对象。commands 的 payload 必须是对象。");
        return builder.ToString();
    }

    private static AIControlPlan ParsePlan(string raw, AITurnContext context)
    {
        string json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[ControlPlannerChannel] Empty/invalid JSON, raw={raw}");
            return CreateFallbackPlan(context);
        }

        try
        {
            return JsonConvert.DeserializeObject<AIControlPlan>(json) ?? CreateFallbackPlan(context);
        }
        catch (JsonException e)
        {
            Debug.LogWarning($"[ControlPlannerChannel] JSON parse failed: {e.Message}, raw={raw}");
            return CreateFallbackPlan(context);
        }
    }

    private static AIControlPlan CreateFallbackPlan(AITurnContext context)
    {
        return new AIControlPlan
        {
            TurnId = context.TurnId,
            ControlTags = new System.Collections.Generic.List<string>(),
            Commands = new System.Collections.Generic.List<AICommand>()
        };
    }

    private static void NormalizeCommands(AIControlPlan plan, AITurnContext context)
    {
        if (plan.ControlTags == null) plan.ControlTags = new System.Collections.Generic.List<string>();
        if (plan.Commands == null) plan.Commands = new System.Collections.Generic.List<AICommand>();

        if (plan.RequiresMemory && !plan.ControlTags.Exists(tag => string.Equals(tag, "memory", StringComparison.OrdinalIgnoreCase)))
        {
            plan.ControlTags.Add("memory");
        }

        foreach (AICommand command in plan.Commands)
        {
            command?.EnsureIds(context.TurnId);
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string text = raw.Trim();
        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace) return null;
        return text.Substring(firstBrace, lastBrace - firstBrace + 1);
    }
}
