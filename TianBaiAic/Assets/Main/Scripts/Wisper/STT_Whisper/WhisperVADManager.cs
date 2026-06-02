using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

/// <summary>
/// Whisper 语音监听入口。
/// 负责两段式录音：先监听唤醒词，再监听真正的用户指令；
/// 指令识别完成后会交给 WebDialog.SubmitText，让语音链路和手动输入共用同一套对话逻辑。
/// </summary>
public class WhisperVADManager_History : MonoBehaviour
{
    // 当前监听状态。用状态机区分“唤醒词录音”和“正式指令录音”，避免把唤醒词直接发给 AI。
    public enum ListenState
    {
        Idle,
        ListeningForWakeWord,
        ListeningForCommand,
        Processing
    }

    [Header("Core References")]
    public WhisperManager whisperManager;
    public MicrophoneRecord microphoneRecord;

    [Header("UI References")]
    public TextMeshProUGUI tmpText;
    public Button actionButton;
    public TextMeshProUGUI buttonText;

    [Header("Wake Settings")]
    public string wakeWord = "天白";
    public bool disableTurboManagerOnThisObject = true;
    public float wakeWordSilenceSeconds = 1.2f;
    public float commandSilenceSeconds = 3.0f;

    private ListenState _currentState = ListenState.Idle;
    private readonly StringBuilder _chatHistory = new StringBuilder();

    void Awake()
    {
        // 场景里可能同时挂了 WhisperTurboManager；这里默认关闭它，避免两个录音器同时抢麦克风。
        if (!disableTurboManagerOnThisObject) return;

        var turbo = GetComponent<WhisperTurboManager>();
        if (turbo != null) turbo.enabled = false;
    }

    async void Start()
    {
        if (whisperManager == null) whisperManager = GetComponent<WhisperManager>();
        if (microphoneRecord == null) microphoneRecord = GetComponent<MicrophoneRecord>();
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (actionButton == null) actionButton = GetComponentInChildren<Button>(true);
        if (buttonText == null && actionButton != null) buttonText = actionButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (whisperManager == null || microphoneRecord == null || tmpText == null || buttonText == null)
        {
            Debug.LogError("WhisperVADManager_History setup failed: missing references.");
            enabled = false;
            return;
        }

        if (actionButton != null) actionButton.interactable = false;
        UpdateUIStatus("<color=#FFA500>System: loading Whisper model...</color>");

        // Whisper 模型初始化完成后再开始监听，否则第一次录音可能无法被识别。
        await whisperManager.InitModel();

        microphoneRecord.vadStop = true;
        microphoneRecord.OnRecordStop -= OnRecordStop;
        microphoneRecord.OnRecordStop += OnRecordStop;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(ToggleRecording);
            actionButton.interactable = true;
        }

        AppendToHistory($"<color=#00FFFF>天白:</color> 你好，请说唤醒词 \"{wakeWord}\"。");
        StartWakeWordListening();
    }

    public void ToggleRecording()
    {
        if (microphoneRecord.IsRecording)
        {
            microphoneRecord.StopRecord();
        }
        else if (_currentState == ListenState.Idle)
        {
            StartWakeWordListening();
        }
    }

    private void UpdateUIStatus(string statusMessage)
    {
        if (_chatHistory.Length == 0) tmpText.text = statusMessage;
        else tmpText.text = _chatHistory + "\n\n" + statusMessage;
    }

    private void AppendToHistory(string message)
    {
        if (_chatHistory.Length > 0) _chatHistory.AppendLine();
        _chatHistory.Append(message);
    }

    private void StartWakeWordListening()
    {
        // 第一段录音只负责判断有没有说出 wakeWord。
        _currentState = ListenState.ListeningForWakeWord;

        microphoneRecord.vadStopTime = wakeWordSilenceSeconds;
        microphoneRecord.StartRecord();

        buttonText.text = "监听唤醒词中...";
        buttonText.color = Color.gray;
        UpdateUIStatus($"<color=#808080><i>[系统：正在监听唤醒词 \"{wakeWord}\"]</i></color>");
    }

    private void StartCommandListening()
    {
        // 第二段录音才是要发送给 AI 的用户内容。
        _currentState = ListenState.ListeningForCommand;

        microphoneRecord.vadStopTime = commandSilenceSeconds;
        microphoneRecord.StartRecord();

        buttonText.text = "录音中...";
        buttonText.color = Color.green;
        AppendToHistory("<color=#00FFFF>天白:</color> 我在，请说。");
        UpdateUIStatus("<color=#00FF00><i>[系统：请说指令，停顿后自动发送]</i></color>");
    }

    private async void OnRecordStop(AudioChunk recordedAudio)
    {
        // VAD 检测到停顿后会进入这里；previousState 决定这段音频应该按唤醒词还是按指令处理。
        ListenState previousState = _currentState;
        _currentState = ListenState.Processing;
        if (actionButton != null) actionButton.interactable = false;

        if (previousState == ListenState.ListeningForCommand)
        {
            UpdateUIStatus("<color=#FFFF00><i>[系统：正在识别语音...]</i></color>");
        }

        var res = await whisperManager.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
        string recognizedText = res != null ? res.Result.Trim() : string.Empty;
        Debug.Log($"[WhisperVAD] 识别结果: {recognizedText}");

        if (previousState == ListenState.ListeningForWakeWord)
        {
            // 只有识别文本包含唤醒词时，才进入正式指令录音。
            if (!string.IsNullOrWhiteSpace(recognizedText) && recognizedText.Contains(wakeWord))
            {
                Debug.Log($"Wake word detected: {recognizedText}");
                StartCommandListening();
                if (actionButton != null) actionButton.interactable = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                Debug.Log($"Ignored non-wake text: {recognizedText}");
            }

            StartWakeWordListening();
            if (actionButton != null) actionButton.interactable = true;
            return;
        }

        if (previousState != ListenState.ListeningForCommand)
        {
            StartWakeWordListening();
            if (actionButton != null) actionButton.interactable = true;
            return;
        }

        // 到这里说明已经拿到正式指令，先显示在 Whisper 面板，再提交给 UI/AI 链路。
        buttonText.text = "提交中...";
        buttonText.color = Color.cyan;

        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            AppendToHistory("<color=#808080>用户:</color> (未识别到有效语音)");
            StartWakeWordListening();
            if (actionButton != null) actionButton.interactable = true;
            return;
        }

        AppendToHistory($"<color=#FFFFFF>用户:</color> {recognizedText}");

        // 语音链路和手动输入保持一致：
        // 先把识别文本写进场景里的 InputField，再调用 WebDialog 的提交逻辑。
        bool submitted = WebDialog.SubmitText(recognizedText);
        Debug.Log($"[WhisperVAD] SubmitText result: {submitted}");
        if (!submitted)
        {
            // 如果 UI 输入框暂时不可用，就直接走 AI 控制器，避免语音结果丢失。
            AIConversationController.TryAsk(
                recognizedText,
                onStreamUpdate: WebDialog.Dialog,
                onComplete: WebDialog.Dialog,
                onError: error => WebDialog.Dialog($"AI config or request failed:\n{error}"));
            Debug.LogWarning("[WhisperVAD] SubmitText failed, fallback to AIConversationController.");
        }

        StartWakeWordListening();
        if (actionButton != null) actionButton.interactable = true;
    }

    void OnDestroy()
    {
        if (microphoneRecord != null) microphoneRecord.OnRecordStop -= OnRecordStop;
    }
}
