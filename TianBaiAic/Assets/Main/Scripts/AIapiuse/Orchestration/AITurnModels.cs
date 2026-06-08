using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 一轮 AI 调度的阶段。
/// Fast 表示第一轮快速响应；Refine 表示带记忆后的二次修正；Final 表示本轮结束。
/// </summary>
public enum AITurnPhase
{
    Fast,
    Refine,
    Final
}

/// <summary>
/// 一轮用户输入的共享上下文。
/// A/B/C 通道都读取这个对象，避免通道之间互相硬引用。
/// </summary>
[Serializable]
public class AITurnContext
{
    public string TurnId;
    public string ConversationId;
    public string UserInput;
    public string CreatedAt;
    public AITurnPhase Phase = AITurnPhase.Fast;
    public List<ChatMessage> RecentMessages = new List<ChatMessage>();
    public List<string> AvailableCommandTypes = new List<string>();
    public AIDialogueResult FastDialogueResult;
    public AIDialogueResult FinalDialogueResult;
    public AIControlPlan FastControlPlan;
    public AIControlPlan FinalControlPlan;
    public AIMemoryResult MemoryResult;

    [JsonIgnore]
    public CancellationToken CancellationToken;

    public static AITurnContext Create(string userInput, string conversationId = "default")
    {
        return new AITurnContext
        {
            TurnId = Guid.NewGuid().ToString("N"),
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? "default" : conversationId,
            UserInput = userInput,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    public string BuildMemoryText(int maxItems = 6)
    {
        if (MemoryResult == null || MemoryResult.Items == null || MemoryResult.Items.Count == 0)
        {
            return "";
        }

        int count = Mathf.Min(maxItems, MemoryResult.Items.Count);
        var lines = new List<string>();
        for (int i = 0; i < count; i++)
        {
            AIMemoryItem item = MemoryResult.Items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Content)) continue;
            lines.Add($"- {item.Content}");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// A 通道返回值：只描述天白要说的话。
/// </summary>
[Serializable]
public class AIDialogueResult
{
    public string TurnId;
    public AITurnPhase Phase;
    public string Text;
    public string EmotionHint;
    public bool IsFinal;
}

/// <summary>
/// B 通道返回值：描述控制标签和要执行的命令。
/// </summary>
[Serializable]
public class AIControlPlan
{
    [JsonProperty("turn_id")]
    public string TurnId;

    [JsonProperty("control_tags")]
    public List<string> ControlTags = new List<string>();

    [JsonProperty("requires_memory")]
    public bool RequiresMemory;

    [JsonProperty("memory_query")]
    public string MemoryQuery;

    [JsonProperty("should_refine_dialogue")]
    public bool ShouldRefineDialogue;

    [JsonProperty("should_refine_actions")]
    public bool ShouldRefineActions;

    [JsonProperty("commands")]
    public List<AICommand> Commands = new List<AICommand>();

    [JsonIgnore]
    public AITurnPhase Phase;

    public bool HasControlTag(string tag)
    {
        if (ControlTags == null || string.IsNullOrWhiteSpace(tag)) return false;
        return ControlTags.Exists(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// C 通道查询请求。
/// QueryText 默认可以直接用用户输入；B 如果给出更精确的 MemoryQuery，调度器可再发一次 C2。
/// </summary>
[Serializable]
public class AIMemoryQuery
{
    public string TurnId;
    public string QueryText;
    public int Limit = 8;
}

/// <summary>
/// C 通道返回的记忆结果。
/// </summary>
[Serializable]
public class AIMemoryResult
{
    public string TurnId;
    public string QueryText;
    public List<AIMemoryItem> Items = new List<AIMemoryItem>();
    public int ElapsedMs;
    public string Source;

    public bool HasItems => Items != null && Items.Count > 0;
}

/// <summary>
/// 单条记忆候选。后续从 JSONL 换成向量检索时仍然可以沿用这层结构。
/// </summary>
[Serializable]
public class AIMemoryItem
{
    public string Id;
    public string Content;
    public string Type;
    public float Score;
    public string Source;
}

/// <summary>
/// 一轮调度的最终汇总，方便测试脚本或 UI 观察。
/// </summary>
[Serializable]
public class AITurnResult
{
    public string TurnId;
    public AIDialogueResult FastDialogue;
    public AIDialogueResult FinalDialogue;
    public AIControlPlan FastControlPlan;
    public AIControlPlan FinalControlPlan;
    public AIMemoryResult MemoryResult;
}
