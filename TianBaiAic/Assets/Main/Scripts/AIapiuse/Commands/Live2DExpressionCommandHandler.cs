using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 执行 B 通道的 live2d_expression 命令。
/// payload 支持 expression/emotion/name 三种字段名，方便模型和插件使用。
/// </summary>
public class Live2DExpressionCommandHandler : AICommandHandlerBehaviour
{
    public override IEnumerable<string> SupportedCommandTypes => new[] { "live2d_expression", "expression" };

    public override Task<AICommandExecutionResult> ExecuteAsync(
        AICommand command,
        AITurnContext context,
        CancellationToken cancellationToken)
    {
        string expression = command.GetPayloadString("expression");
        if (string.IsNullOrWhiteSpace(expression)) expression = command.GetPayloadString("emotion");
        if (string.IsNullOrWhiteSpace(expression)) expression = command.GetPayloadString("name");

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(AICommandExecutionResult.Fail("expression payload is empty."));
        }

        Live2DAIResponseController.Apply(expression, "");
        return Task.FromResult(AICommandExecutionResult.Ok(expression));
    }
}
