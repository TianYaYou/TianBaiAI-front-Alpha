using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TTS 测试场景专用脚本。
/// 临时挂在 TTS-Test 上，用来验证：InputField 输入文本 -> 调 MIMO TTS -> 解码 AudioClip -> AudioSource 播放。
/// 后续接入天白主链路后，这个脚本可以删除。
/// </summary>
public class TTS_test : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TTS 主控制器。为空时会自动在当前物体上寻找或创建。")]
    public TTSMain ttsMain;

    [Tooltip("测试输入框。为空时自动寻找场景里的 TMP_InputField。")]
    public TMP_InputField inputField;

    [Tooltip("点击后触发 TTS 测试。为空时自动寻找场景里的 Button。")]
    public Button playButton;

    [Tooltip("可选：显示测试状态。如果不拖，会优先复用按钮里的文本。")]
    public TMP_Text statusText;

    [Header("Test Input")]
    [TextArea(2, 4)]
    public string defaultContent = "（轻笑）你好呀，我是天白。今天也想陪你慢慢把事情做好。";

    [Tooltip("测试用情绪，会拼进 my_description。主链路里会来自 AI 返回 JSON 的 emotion。")]
    public string testEmotion = "温柔";

    [Tooltip("输入框为空时是否使用 defaultContent。")]
    public bool useDefaultWhenInputEmpty = true;

    private void Start()
    {
        AutoBindReferences();

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(RunTest);
            playButton.onClick.AddListener(RunTest);
        }

        SetStatus("TTS Ready");
    }

    /// <summary>
    /// 按钮入口。Unity UI Button 不支持直接绑定 async Task，所以这里用 async void 做最外层事件包装。
    /// </summary>
    public async void RunTest()
    {
        AutoBindReferences();

        string content = inputField != null ? inputField.text : "";
        if (string.IsNullOrWhiteSpace(content) && useDefaultWhenInputEmpty)
        {
            content = defaultContent;
            if (inputField != null)
            {
                inputField.text = content;
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            SetStatus("请输入要朗读的文字");
            Debug.LogWarning("[TTS_test] Empty content, TTS request skipped.");
            return;
        }

        SetInteractable(false);
        SetStatus("正在请求 TTS...");
        Debug.Log($"[TTS_test] Start TTS test. content={content}, emotion={testEmotion}");

        await ttsMain.SpeakAsync(
            content,
            testEmotion,
            onComplete: clip =>
            {
                SetStatus(clip != null ? $"播放中 {clip.length:0.00}s" : "生成失败");
                Debug.Log($"[TTS_test] TTS complete. clip={(clip != null ? clip.name : "null")}");
            },
            onError: error =>
            {
                SetStatus("TTS 失败");
                Debug.LogError($"[TTS_test] {error}");
            });

        SetInteractable(true);
    }

    private void AutoBindReferences()
    {
        if (ttsMain == null)
        {
            ttsMain = GetComponent<TTSMain>();
            if (ttsMain == null)
            {
                ttsMain = gameObject.AddComponent<TTSMain>();
            }
        }

        if (inputField == null)
        {
            inputField = FindFirstObjectByType<TMP_InputField>();
        }

        if (playButton == null)
        {
            playButton = FindFirstObjectByType<Button>();
        }

        if (statusText == null && playButton != null)
        {
            statusText = playButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void SetInteractable(bool interactable)
    {
        if (playButton != null)
        {
            playButton.interactable = interactable;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[TTS_test] {message}");
    }
}
