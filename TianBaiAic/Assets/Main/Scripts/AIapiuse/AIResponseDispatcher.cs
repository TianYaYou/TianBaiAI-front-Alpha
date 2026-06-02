using TianBaiAI.Memory;
using UnityEngine;

/// <summary>
/// AI 回复分发器。
/// 负责把旧 prompt JSON 中的 content 之外的字段，分发给记忆、好感度、Live2D 表情和动作系统。
/// TTS 和 actions 工具队列之后重构，暂时不在这里执行。
/// </summary>
public static class AIResponseDispatcher
{
    public static void Dispatch(LegacyDialogResponse response)
    {
        if (response == null) return;

        Live2DAIResponseController.Apply(response.emotion, response.movement);

        if (response.favorability.HasValue)
        {
            MemoryService.SaveFavorability(response.favorability.Value);
        }

        if (response.writememory != null)
        {
            MemoryService.WriteMemory(response.writememory);
        }

        if (response.readmemory != null)
        {
            var results = MemoryService.ReadMemory(response.readmemory);
            if (results.Count == 0)
            {
                Debug.Log("[AIResponseDispatcher] readmemory: 没有找到相关记忆。当前版本先记录查询结果，后续会接二次对话注入。");
            }
            else
            {
                Debug.Log($"[AIResponseDispatcher] readmemory: 找到 {results.Count} 条。当前版本先记录查询结果，后续会接二次对话注入。");
            }
        }

        if (response.actions != null && response.actions.Count > 0)
        {
            Debug.Log($"[AIResponseDispatcher] actions 暂未执行，等待工具队列重构: {string.Join(", ", response.actions)}");
        }
    }
}
