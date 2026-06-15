using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 执行 B 通道的 live2d_motion 命令。
/// payload 支持 motion/movement/name/action_id。
/// </summary>
public class Live2DMotionCommandHandler : AICommandHandlerBehaviour
{
    public override IEnumerable<string> SupportedCommandTypes => new[] { "live2d_motion", "motion" };

    public override Task<AICommandExecutionResult> ExecuteAsync(
        AICommand command,
        AITurnContext context,
        CancellationToken cancellationToken)
    {
        int actionId = command.GetPayloadInt("action_id", 0);
        if (actionId > 0 && Live2DAniActionControl.Instance != null)
        {
            Live2DAniActionControl.Instance.PlayAction(actionId);
            return Task.FromResult(AICommandExecutionResult.Ok($"action_id={actionId}"));
        }

        string motion = command.GetPayloadString("motion");
        if (string.IsNullOrWhiteSpace(motion)) motion = command.GetPayloadString("movement");
        if (string.IsNullOrWhiteSpace(motion)) motion = command.GetPayloadString("name");

        if (string.IsNullOrWhiteSpace(motion))
        {
            return Task.FromResult(AICommandExecutionResult.Fail("motion payload is empty."));
        }

        Live2DAIResponseController.Apply("", motion);
        return Task.FromResult(AICommandExecutionResult.Ok(motion));
    }
}
