using TianBaiAI.Memory;
using UnityEngine;

/// <summary>
/// AI 回复分发器。
/// 负责把旧 prompt JSON 中的字段分发给各个系统：
/// content 交给 TTS 朗读，emotion/movement 交给 Live2D，memory/favorability 交给记忆系统。
/// 工具 actions 队列之后会单独重构，目前仍然只记录不执行。
/// </summary>
public static class AIResponseDispatcher
{
    public static void Dispatch(LegacyDialogResponse response)
    {
        if (response == null) return;

        // Live2D 先动起来：让表情和动作尽量跟文字输出同时发生。
        Live2DAIResponseController.Apply(response.emotion, response.movement);

        // TTS 不阻塞主对话链路：Output 文字会先显示，语音在后台生成并播放。
        DispatchTTS(response);

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

    private static void DispatchTTS(LegacyDialogResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.content))
        {
            Debug.LogWarning("[AIResponseDispatcher] TTS skipped: response.content is empty.");
            return;
        }

        // response.emotion 会进入 TTSMain.BuildVoiceDescription，用来微调本句语气。
        bool started = TTSMain.TrySpeak(
            response.content,
            response.emotion,
            onComplete: clip =>
            {
                if (clip != null)
                {
                    Debug.Log($"[AIResponseDispatcher] TTS started playback: {clip.name}, {clip.length:0.00}s");
                }
            },
            onError: error =>
            {
                // TTS 失败不应该打断文字对话，所以这里只记录 warning。
                Debug.LogWarning($"[AIResponseDispatcher] TTS failed: {error}");
            });

        if (!started)
        {
            Debug.LogWarning("[AIResponseDispatcher] TTS was not started.");
        }
    }
}
