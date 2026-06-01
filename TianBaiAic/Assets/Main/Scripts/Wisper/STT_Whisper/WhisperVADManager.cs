using UnityEngine;
using Whisper;
using Whisper.Utils;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Text; // 引入StringBuilder用于高效拼接文本

public class WhisperVADManager_History : MonoBehaviour
{
    public enum ListenState
    {
        Idle,
        ListeningForWakeWord, // 阶段一：监听唤醒词
        ListeningForCommand,  // 阶段二：录制正式指令
        Processing            // 处理中
    }

    [Header("核心引用")]
    public WhisperManager whisperManager;
    public MicrophoneRecord microphoneRecord;

    [Header("UI 绑定")]
    public TextMeshProUGUI tmpText;
    public Button actionButton;
    public TextMeshProUGUI buttonText;

    [Header("唤醒设置")]
    public string wakeWord = "天白";
    private ListenState _currentState = ListenState.Idle;

    // 用于保存所有历史聊天记录
    private StringBuilder _chatHistory = new StringBuilder();

    async void Start()
    {
        actionButton.interactable = false;
        UpdateUIStatus("<color=#FFA500>系统：模型加载中...</color>");

        await whisperManager.InitModel();

        microphoneRecord.vadStop = true;
        microphoneRecord.OnRecordStop += OnRecordStop;

        actionButton.interactable = true;
        actionButton.onClick.AddListener(ToggleRecording);

        // 添加一条初始欢迎语到历史记录中
        AppendToHistory($"<color=#00FFFF>天白：</color>你好！请随时叫我的名字“{wakeWord}”唤醒我。");

        // 模型加载完毕后，自动进入唤醒词监听循环
        StartWakeWordListening();
    }

    public void ToggleRecording()
    {
        if (microphoneRecord.IsRecording)
            microphoneRecord.StopRecord();
        else if (_currentState == ListenState.Idle)
            StartWakeWordListening();
    }

    // --- 核心UI更新逻辑 ---
    // 将旧历史记录与实时尾部提示合并显示
    private void UpdateUIStatus(string statusMessage)
    {
        // 如果没有历史记录，直接显示状态；如果有，则换行显示在最末尾
        if (_chatHistory.Length == 0)
        {
            tmpText.text = statusMessage;
        }
        else
        {
            tmpText.text = _chatHistory.ToString() + "\n\n" + statusMessage;
        }
    }

    // 将确定的消息追加到历史记录中保存
    private void AppendToHistory(string message)
    {
        if (_chatHistory.Length > 0)
            _chatHistory.AppendLine(); // 换行

        _chatHistory.Append(message);
    }
    // ----------------------

    private void StartWakeWordListening()
    {
        _currentState = ListenState.ListeningForWakeWord;

        microphoneRecord.vadStopTime = 1.2f;
        microphoneRecord.StartRecord();

        buttonText.text = "监听唤醒词中...";
        buttonText.color = Color.gray;

        // 末尾显示浅灰色的提示，不污染历史记录
        UpdateUIStatus($"<color=#808080><i>[系统：正在后台监听唤醒词 \"{wakeWord}\"...]</i></color>");
    }

    private void StartCommandListening()
    {
        _currentState = ListenState.ListeningForCommand;

        microphoneRecord.vadStopTime = 3.0f;
        microphoneRecord.StartRecord();

        buttonText.text = "录音中";
        buttonText.color = Color.green;

        // 追加AI的应答到历史记录中
        AppendToHistory("<color=#00FFFF>天白：</color>我在！请吩咐...");
        // 更新UI，末尾显示正在听取指令
        UpdateUIStatus("<color=#00FF00><i>[系统：正在倾听您的指令 (停顿3秒自动发送)...]</i></color>");
    }

    private async void OnRecordStop(AudioChunk recordedAudio)
    {
        ListenState previousState = _currentState;
        _currentState = ListenState.Processing;
        actionButton.interactable = false;

        if (previousState == ListenState.ListeningForCommand)
        {
            UpdateUIStatus("<color=#FFFF00><i>[系统：语音识别中...]</i></color>");
        }

        // 执行本地识别
        var res = await whisperManager.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
        string recognizedText = res != null ? res.Result.Trim() : "";

        if (previousState == ListenState.ListeningForWakeWord)
        {
            if (recognizedText.Contains(wakeWord))
            {
                Debug.Log($"[唤醒成功] 听到关键词: {recognizedText}");
                // 如果用户除了唤醒词还带了其他话，也可以作为用户发言记录下来（可选）
                // AppendToHistory($"<color=#FFFFFF>用户：</color>{recognizedText}");

                StartCommandListening();
                actionButton.interactable = true;
                return;
            }
            else
            {
                if (!string.IsNullOrEmpty(recognizedText))
                    Debug.Log($"[忽略杂音] {recognizedText}");

                StartWakeWordListening();
                actionButton.interactable = true;
                return;
            }
        }
        else if (previousState == ListenState.ListeningForCommand)
        {
            buttonText.text = "处理指令中...";
            buttonText.color = Color.white;

            // 将用户正式说的指令永久固化到聊天历史中
            if (!string.IsNullOrEmpty(recognizedText))
            {
                AppendToHistory($"<color=#FFFFFF>用户：</color>{recognizedText}");
            }
            else
            {
                AppendToHistory("<color=#808080>用户：(未听清指令)</color>");
            }

            UpdateUIStatus("<color=#FFA500><i>[系统：正在处理您的请求...]</i></color>");
            Debug.Log($"[收到最终指令]: {recognizedText}");

            // TODO: 在这里对接你的业务逻辑 (如请求 LLM 返回结果)
            await Task.Delay(1000); // 模拟耗时

            // 假设 LLM 返回了结果，你可以把它追加进历史记录
            // AppendToHistory("<color=#00FFFF>天白：</color>好的，已经为您处理完毕。");

            // 重新回到后台监听唤醒词状态
            StartWakeWordListening();
            actionButton.interactable = true;
        }
    }
}