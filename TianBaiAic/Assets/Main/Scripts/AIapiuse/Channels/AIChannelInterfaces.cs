using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A 通道：只负责生成天白要说的话。
/// </summary>
public interface IAIDialogueChannel
{
    Task<AIDialogueResult> GenerateDialogueAsync(AITurnContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// B 通道：只负责分析控制标签和行为命令。
/// </summary>
public interface IAIControlPlannerChannel
{
    Task<AIControlPlan> GenerateControlPlanAsync(AITurnContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// C 通道：只负责检索记忆。
/// </summary>
public interface IAIMemoryProvider
{
    Task<AIMemoryResult> SearchMemoryAsync(AIMemoryQuery query, AITurnContext context, CancellationToken cancellationToken = default);
}
