using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A 通道默认实现：只负责聊天文本，不输出 JSON，不直接控制动作/工具/记忆。
/// </summary>
public class ChatDialogueChannel : MonoBehaviour, IAIDialogueChannel
{
    [Header("Config")]
    public string configPathOverride = "";
    public bool createTemplateIfMissing = true;

    [Header("Prompt")]
    [Tooltip("A 通道 prompt 文件名，位于 StreamingAssets/AI。不要和历史 system_prompt.txt 混用。")]
    public string promptFileName = "dialogue_prompt.txt";

    [TextArea(8, 16)]
    public string dialogueSystemPrompt =
        "你是天白，一个温柔、亲近、会陪伴用户做项目的 Unity 桌宠。\n" +
        "你的任务只有一个：自然聊天。\n" +
        "不要输出 JSON，不要输出 XML，不要输出 control_tags，不要输出工具命令。\n" +
        "如果用户的问题明显依赖过去记忆，而当前没有提供记忆，你可以先用自然语气说正在想/正在找相关记忆，不要编造过去发生过的事。\n" +
        "如果提供了【相关记忆】，请自然地结合记忆回答。\n" +
        "回复尽量简洁、口语化、有天白的陪伴感。";

    [Header("Debug")]
    public bool logDialogue = true;

    private AIConfig _config;
    private AISession _session;

    public async Task<AIDialogueResult> GenerateDialogueAsync(AITurnContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (!TryEnsureSession(out string reason)) throw new InvalidOperationException(reason);

        string prompt = BuildUserPayload(context);
        if (logDialogue) Debug.Log($"[ChatDialogueChannel] Start A channel. phase={context.Phase}, input={context.UserInput}");

        string text = await SendOnceAsync(prompt, cancellationToken);
        text = CleanDialogueText(text);

        return new AIDialogueResult
        {
            TurnId = context.TurnId,
            Phase = context.Phase,
            Text = text,
            EmotionHint = "自然",
            IsFinal = context.Phase == AITurnPhase.Refine || context.MemoryResult == null || !context.MemoryResult.HasItems
        };
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
            AIChannelConfigUtility.LoadPromptFromStreamingAssets(promptFileName, dialogueSystemPrompt, logDialogue, "A/dialogue"),
            jsonMode: false,
            passHistory: true,
            temperatureOffset: 0f);

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

    private static string BuildUserPayload(AITurnContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"当前时间：{context.CreatedAt}");
        builder.AppendLine($"用户输入：{context.UserInput}");

        string memoryText = context.BuildMemoryText();
        if (!string.IsNullOrWhiteSpace(memoryText))
        {
            builder.AppendLine("\n相关记忆：");
            builder.AppendLine(memoryText);
            builder.AppendLine("请结合这些记忆自然回答。");
        }
        else
        {
            builder.AppendLine("\n当前没有可用记忆。不要编造过去记忆。");
        }

        if (context.Phase == AITurnPhase.Refine && context.FastDialogueResult != null)
        {
            builder.AppendLine("\n你之前的快速回复：");
            builder.AppendLine(context.FastDialogueResult.Text);
            builder.AppendLine("现在请基于相关记忆给出最终回复，可以自然替换之前的快速回复。");
        }

        return builder.ToString();
    }

    private static string CleanDialogueText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "天白有点没想好，要不要再说一遍？";
        string cleaned = text.Trim();
        if (cleaned.StartsWith("```") && cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Trim('`').Trim();
        }

        return cleaned;
    }
}
