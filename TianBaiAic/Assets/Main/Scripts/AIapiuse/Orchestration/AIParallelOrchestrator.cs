using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 多通道 AI 调度器。
/// A 只聊天，B 只做控制规划，C 只检索记忆；调度器负责并行、二次请求和命令分发。
/// </summary>
public class AIParallelOrchestrator : MonoBehaviour
{
    public static AIParallelOrchestrator Instance { get; private set; }

    [Header("Channels")]
    public MonoBehaviour dialogueChannelBehaviour;
    public MonoBehaviour controlPlannerChannelBehaviour;
    public MonoBehaviour memoryProviderBehaviour;
    public AICommandRouter commandRouter;

    [Header("Runtime")]
    public bool autoCreateDefaultComponents = true;
    public bool autoSpeakTTS = true;
    public bool executeCommands = true;
    public int memoryTimeoutMs = 5000;
    public bool rerunMemoryWithPlannerQuery = true;

    [Header("Debug")]
    public bool logOrchestrator = true;

    private IAIDialogueChannel DialogueChannel => dialogueChannelBehaviour as IAIDialogueChannel;
    private IAIControlPlannerChannel ControlPlannerChannel => controlPlannerChannelBehaviour as IAIControlPlannerChannel;
    private IAIMemoryProvider MemoryProvider => memoryProviderBehaviour as IAIMemoryProvider;

    private CancellationTokenSource _currentTurnCts;

    public bool IsRunning { get; private set; }
    public AITurnResult LastTurnResult { get; private set; }

    private void Awake()
    {
        Instance = this;
        EnsureDefaultComponents();
    }

    public static AIParallelOrchestrator GetOrCreate()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<AIParallelOrchestrator>();
        if (Instance != null) return Instance;

        var go = new GameObject("AIParallelOrchestrator");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AIParallelOrchestrator>();
        return Instance;
    }

    public static bool TryAsk(
        string userText,
        Action<string> onFastText = null,
        Action<string> onFinalText = null,
        Action<string> onComplete = null,
        Action<string> onError = null)
    {
        AIParallelOrchestrator orchestrator = GetOrCreate();
        if (orchestrator == null)
        {
            onError?.Invoke("AIParallelOrchestrator is not available.");
            return false;
        }

        _ = orchestrator.RunTurnAsync(userText, onFastText, onFinalText, onComplete, onError);
        return true;
    }

    public async Task<AITurnResult> RunTurnAsync(
        string userText,
        Action<string> onFastText = null,
        Action<string> onFinalText = null,
        Action<string> onComplete = null,
        Action<string> onError = null)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            onError?.Invoke("User input is empty.");
            return null;
        }

        EnsureDefaultComponents();
        if (!ValidateChannels(out string channelError))
        {
            onError?.Invoke(channelError);
            return null;
        }

        CancelCurrentTurn();
        var turnCts = new CancellationTokenSource();
        _currentTurnCts = turnCts;
        CancellationToken token = turnCts.Token;
        IsRunning = true;

        AITurnContext context = AITurnContext.Create(userText);
        context.CancellationToken = token;
        context.AvailableCommandTypes = commandRouter != null ? commandRouter.GetSupportedCommandTypes() : new System.Collections.Generic.List<string>();

        var result = new AITurnResult { TurnId = context.TurnId };
        LastTurnResult = result;

        try
        {
            if (logOrchestrator) Debug.Log($"[AIParallelOrchestrator] Turn start: {context.TurnId}, input={userText}");

            Task<AIDialogueResult> dialogueTask = SafeDialogueAsync(context, token);
            Task<AIControlPlan> planTask = SafePlanAsync(context, token);
            Task<AIMemoryResult> memoryTask = SafeMemoryAsync(context.UserInput, context, token);

            AIDialogueResult fastDialogue = await dialogueTask;
            context.FastDialogueResult = fastDialogue;
            context.FinalDialogueResult = fastDialogue;
            result.FastDialogue = fastDialogue;
            result.FinalDialogue = fastDialogue;
            PublishDialogue(fastDialogue, onFastText, speak: true);

            AIControlPlan fastPlan = await planTask;
            context.FastControlPlan = fastPlan;
            result.FastControlPlan = fastPlan;
            await ExecuteCommandsAsync(fastPlan, context, "immediate", token);

            bool needsMemory = fastPlan != null && (fastPlan.RequiresMemory || fastPlan.HasControlTag("memory"));
            if (needsMemory)
            {
                AIMemoryResult memory = await ResolveMemoryForPlanAsync(fastPlan, memoryTask, context, token);
                context.MemoryResult = memory;
                result.MemoryResult = memory;

                context.Phase = AITurnPhase.Refine;
                Task<AIDialogueResult> refinedDialogueTask = SafeDialogueAsync(context, token);
                Task<AIControlPlan> refinedPlanTask = SafePlanAsync(context, token);

                AIDialogueResult finalDialogue = await refinedDialogueTask;
                context.FinalDialogueResult = finalDialogue;
                result.FinalDialogue = finalDialogue;
                PublishDialogue(finalDialogue, onFinalText, speak: true);

                AIControlPlan finalPlan = await refinedPlanTask;
                context.FinalControlPlan = finalPlan;
                result.FinalControlPlan = finalPlan;
                await ExecuteCommandsAsync(finalPlan, context, "immediate", token);
                await ExecuteCommandsAsync(finalPlan, context, "after_dialogue", token);
                await ExecuteCommandsAsync(finalPlan, context, "background", token);

                onComplete?.Invoke(finalDialogue != null ? finalDialogue.Text : fastDialogue.Text);
            }
            else
            {
                await ExecuteCommandsAsync(fastPlan, context, "after_dialogue", token);
                await ExecuteCommandsAsync(fastPlan, context, "background", token);
                onComplete?.Invoke(fastDialogue.Text);
            }

            if (logOrchestrator) Debug.Log($"[AIParallelOrchestrator] Turn complete: {context.TurnId}");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (logOrchestrator) Debug.Log($"[AIParallelOrchestrator] Turn cancelled: {context.TurnId}");
            return result;
        }
        catch (Exception e)
        {
            string error = $"AI turn failed: {e.Message}";
            Debug.LogError($"[AIParallelOrchestrator] {error}");
            onError?.Invoke(error);
            return result;
        }
        finally
        {
            if (_currentTurnCts == turnCts)
            {
                IsRunning = false;
                _currentTurnCts = null;
            }

            turnCts.Dispose();
        }
    }

    public void CancelCurrentTurn()
    {
        if (_currentTurnCts != null && !_currentTurnCts.IsCancellationRequested)
        {
            _currentTurnCts.Cancel();
        }
    }

    private void EnsureDefaultComponents()
    {
        if (!autoCreateDefaultComponents) return;

        if (dialogueChannelBehaviour == null)
        {
            dialogueChannelBehaviour = GetComponent<ChatDialogueChannel>() ?? gameObject.AddComponent<ChatDialogueChannel>();
        }

        if (controlPlannerChannelBehaviour == null)
        {
            controlPlannerChannelBehaviour = GetComponent<ControlPlannerChannel>() ?? gameObject.AddComponent<ControlPlannerChannel>();
        }

        if (memoryProviderBehaviour == null)
        {
            memoryProviderBehaviour = GetComponent<MemorySearchChannel>() ?? gameObject.AddComponent<MemorySearchChannel>();
        }

        commandRouter ??= AICommandRouter.GetOrCreate();
        EnsureDefaultCommandHandlers(commandRouter);
        commandRouter.RegisterSceneHandlers();
    }

    private static void EnsureDefaultCommandHandlers(AICommandRouter router)
    {
        if (router == null) return;
        if (router.GetComponent<Live2DExpressionCommandHandler>() == null) router.gameObject.AddComponent<Live2DExpressionCommandHandler>();
        if (router.GetComponent<Live2DMotionCommandHandler>() == null) router.gameObject.AddComponent<Live2DMotionCommandHandler>();
        if (router.GetComponent<MemoryWriteCommandHandler>() == null) router.gameObject.AddComponent<MemoryWriteCommandHandler>();
    }

    private bool ValidateChannels(out string reason)
    {
        reason = null;
        if (DialogueChannel == null) reason = "Dialogue channel is not assigned or does not implement IAIDialogueChannel.";
        else if (ControlPlannerChannel == null) reason = "Control planner channel is not assigned or does not implement IAIControlPlannerChannel.";
        else if (MemoryProvider == null) reason = "Memory provider is not assigned or does not implement IAIMemoryProvider.";
        else if (commandRouter == null) reason = "AICommandRouter is not assigned.";
        return reason == null;
    }

    private async Task<AIDialogueResult> SafeDialogueAsync(AITurnContext context, CancellationToken token)
    {
        try
        {
            return await DialogueChannel.GenerateDialogueAsync(context, token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIParallelOrchestrator] A channel failed: {e.Message}");
            return new AIDialogueResult
            {
                TurnId = context.TurnId,
                Phase = context.Phase,
                Text = "天白这边请求有点卡住了，可以稍后再试一下吗？",
                IsFinal = true
            };
        }
    }

    private async Task<AIControlPlan> SafePlanAsync(AITurnContext context, CancellationToken token)
    {
        try
        {
            return await ControlPlannerChannel.GenerateControlPlanAsync(context, token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIParallelOrchestrator] B channel failed: {e.Message}");
            return new AIControlPlan { TurnId = context.TurnId, Phase = context.Phase };
        }
    }

    private async Task<AIMemoryResult> SafeMemoryAsync(string queryText, AITurnContext context, CancellationToken token)
    {
        try
        {
            var query = new AIMemoryQuery { TurnId = context.TurnId, QueryText = queryText, Limit = 8 };
            return await MemoryProvider.SearchMemoryAsync(query, context, token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AIParallelOrchestrator] C channel failed: {e.Message}");
            return new AIMemoryResult { TurnId = context.TurnId, QueryText = queryText, Source = "failed" };
        }
    }

    private async Task<AIMemoryResult> ResolveMemoryForPlanAsync(
        AIControlPlan plan,
        Task<AIMemoryResult> defaultMemoryTask,
        AITurnContext context,
        CancellationToken token)
    {
        AIMemoryResult memory = await WaitMemoryWithTimeoutAsync(defaultMemoryTask, context, token);
        string planQuery = plan != null ? plan.MemoryQuery : null;
        if (rerunMemoryWithPlannerQuery
            && !string.IsNullOrWhiteSpace(planQuery)
            && !string.Equals(planQuery, context.UserInput, StringComparison.OrdinalIgnoreCase))
        {
            AIMemoryResult refinedMemory = await SafeMemoryAsync(planQuery, context, token);
            if (refinedMemory != null && refinedMemory.HasItems)
            {
                return refinedMemory;
            }
        }

        return memory;
    }

    private async Task<AIMemoryResult> WaitMemoryWithTimeoutAsync(
        Task<AIMemoryResult> memoryTask,
        AITurnContext context,
        CancellationToken token)
    {
        Task timeoutTask = Task.Delay(Mathf.Max(100, memoryTimeoutMs), token);
        Task completed = await Task.WhenAny(memoryTask, timeoutTask);
        if (completed == memoryTask)
        {
            return await memoryTask;
        }

        Debug.LogWarning($"[AIParallelOrchestrator] Memory search timeout: {memoryTimeoutMs}ms");
        return new AIMemoryResult { TurnId = context.TurnId, QueryText = context.UserInput, Source = "timeout" };
    }

    private void PublishDialogue(AIDialogueResult dialogue, Action<string> callback, bool speak)
    {
        if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.Text)) return;
        callback?.Invoke(dialogue.Text);
        if (autoSpeakTTS && speak)
        {
            TTSMain.TrySpeak(dialogue.Text, dialogue.EmotionHint);
        }
    }

    private async Task ExecuteCommandsAsync(AIControlPlan plan, AITurnContext context, string timing, CancellationToken token)
    {
        if (!executeCommands || plan == null || plan.Commands == null || plan.Commands.Count == 0) return;
        await commandRouter.ExecuteManyAsync(plan.Commands, context, timing, token);
    }
}
