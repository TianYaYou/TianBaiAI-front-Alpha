using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 命令路由器：收集所有 IAICommandHandler，并按 command.type 分发执行。
/// 后续 mod 注入时，只要场景里存在实现 IAICommandHandler 的 MonoBehaviour，就能被扫描注册。
/// </summary>
public class AICommandRouter : MonoBehaviour
{
    public static AICommandRouter Instance { get; private set; }

    [Header("Debug")]
    public bool logCommands = true;

    private readonly List<IAICommandHandler> _handlers = new List<IAICommandHandler>();

    private void Awake()
    {
        Instance = this;
        RegisterSceneHandlers();
    }

    public static AICommandRouter GetOrCreate()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<AICommandRouter>();
        if (Instance != null) return Instance;

        var go = new GameObject("AICommandRouter");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AICommandRouter>();
        return Instance;
    }

    public void RegisterHandler(IAICommandHandler handler)
    {
        if (handler == null || _handlers.Contains(handler)) return;
        _handlers.Add(handler);
    }

    public void RegisterSceneHandlers()
    {
        _handlers.Clear();
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IAICommandHandler handler)
            {
                RegisterHandler(handler);
            }
        }
    }

    public List<string> GetSupportedCommandTypes()
    {
        var result = new List<string>();
        foreach (IAICommandHandler handler in _handlers)
        {
            if (handler?.SupportedCommandTypes == null) continue;
            foreach (string type in handler.SupportedCommandTypes)
            {
                if (!string.IsNullOrWhiteSpace(type) && !result.Contains(type)) result.Add(type);
            }
        }

        return result;
    }

    public async Task ExecuteManyAsync(
        IEnumerable<AICommand> commands,
        AITurnContext context,
        string timing = null,
        CancellationToken cancellationToken = default)
    {
        if (commands == null) return;

        var ordered = new List<AICommand>(commands);
        ordered.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (AICommand command in ordered)
        {
            if (command == null) continue;
            command.EnsureIds(context?.TurnId);
            if (!command.MatchesTiming(timing)) continue;
            await ExecuteAsync(command, context, cancellationToken);
        }
    }

    public async Task<AICommandExecutionResult> ExecuteAsync(
        AICommand command,
        AITurnContext context,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Type))
        {
            return AICommandExecutionResult.Fail("Command type is empty.");
        }

        foreach (IAICommandHandler handler in _handlers)
        {
            if (handler == null || !handler.CanHandle(command)) continue;
            AICommandExecutionResult result = await handler.ExecuteAsync(command, context, cancellationToken);
            if (logCommands)
            {
                Debug.Log($"[AICommandRouter] {command.Type}: {(result.Success ? "OK" : "FAIL")} {result.Message}");
            }
            return result;
        }

        string message = $"No handler for command type: {command.Type}";
        if (logCommands) Debug.LogWarning($"[AICommandRouter] {message}");
        return AICommandExecutionResult.Fail(message);
    }
}
