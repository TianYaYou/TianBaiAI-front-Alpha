using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TianBaiAI.Memory;

/// <summary>
/// 执行 B 通道的 memory_write 命令。
/// payload: { "content": "...", "tags": ["project"] }
/// </summary>
public class MemoryWriteCommandHandler : AICommandHandlerBehaviour
{
    public override IEnumerable<string> SupportedCommandTypes => new[] { "memory_write" };

    public override Task<AICommandExecutionResult> ExecuteAsync(
        AICommand command,
        AITurnContext context,
        CancellationToken cancellationToken)
    {
        string content = command.GetPayloadString("content");
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(AICommandExecutionResult.Fail("memory content is empty."));
        }

        List<string> tags = command.GetPayloadStringList("tags");
        if (tags.Count == 0) tags.Add("ai_parallel");

        var memory = new LegacyWriteMemory
        {
            time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            key = tags,
            content = content
        };

        MemoryService.WriteMemory(memory, "ai_parallel_orchestrator");
        return Task.FromResult(AICommandExecutionResult.Ok(content));
    }
}
