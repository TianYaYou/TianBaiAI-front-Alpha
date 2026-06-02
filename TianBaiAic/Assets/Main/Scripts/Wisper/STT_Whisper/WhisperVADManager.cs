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
        WaitingForAI,
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

    [Tooltip("唤醒阶段的静音判定时间。越短唤醒越快，但太短可能把一句话切得过碎。")]
    public float wakeWordSilenceSeconds = 0.6f;

    [Tooltip("指令阶段的静音判定时间。用户停顿超过这个时间后自动提交。")]
    public float commandSilenceSeconds = 2.0f;

    [Tooltip("如果唤醒词后面已经跟着指令，例如“天白帮我打开设置”，就直接提交后半句，不再要求用户再说一次。")]
    public bool submitCommandInWakePhrase = true;

    [Tooltip("AI 回答完成后，延迟一点再重新打开麦克风，避免录音组件刚释放就立刻重启。")]
    public float restartWakeListenDelaySeconds = 0.2f;

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

        // 关键：MicrophoneRecord.echo=true 会把录到的音频回放出来，并生成 Unity 临时对象“One shot audio”。
        // 这个项目只需要识别文本，不需要回放用户自己的声音，所以启动时强制关闭。
        microphoneRecord.echo = false;

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
        WebDialog.SetInputText("天白正在听");
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

                if (TryExtractCommandFromWakeText(recognizedText, out string wakeCommand))
                {
                    Debug.Log($"[WhisperVAD] Wake phrase contains command: {wakeCommand}");
                    SubmitRecognizedCommand(wakeCommand);
                    return;
                }

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

        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            AppendToHistory("<color=#808080>用户:</color> (未识别到有效语音)");
            StartWakeWordListening();
            if (actionButton != null) actionButton.interactable = true;
            return;
        }

        SubmitRecognizedCommand(recognizedText);
    }

    private void SubmitRecognizedCommand(string recognizedText)
    {
        // 到这里说明已经拿到正式指令，先显示在 Whisper 面板，再提交给 UI/AI 链路。
        _currentState = ListenState.WaitingForAI;
        buttonText.text = "提交中...";
        buttonText.color = Color.cyan;
        if (actionButton != null) actionButton.interactable = false;

        AppendToHistory($"<color=#FFFFFF>用户:</color> {recognizedText}");
        UpdateUIStatus("<color=#00BFFF><i>[系统：已提交给 AI，等待回答完成]</i></color>");

        // 语音链路和手动输入保持一致：
        // 先把识别文本写进场景里的 InputField，再调用 WebDialog 的提交逻辑。
        bool submitted = WebDialog.SubmitText(recognizedText, ScheduleWakeWordRestart);
        Debug.Log($"[WhisperVAD] SubmitText result: {submitted}");
        if (!submitted)
        {
            // 如果 UI 输入框暂时不可用，就直接走 AI 控制器，避免语音结果丢失。
            bool requestStarted = AIConversationController.TryAsk(
                recognizedText,
                onStreamUpdate: WebDialog.Dialog,
                onComplete: reply =>
                {
                    WebDialog.Dialog(reply);
                    ScheduleWakeWordRestart();
                },
                onError: error =>
                {
                    WebDialog.Dialog($"AI config or request failed:\n{error}");
                    ScheduleWakeWordRestart();
                });
            if (!requestStarted)
            {
                ScheduleWakeWordRestart();
            }

            Debug.LogWarning("[WhisperVAD] SubmitText failed, fallback to AIConversationController.");
        }
    }

    private void ScheduleWakeWordRestart()
    {
        if (!isActiveAndEnabled) return;

        CancelInvoke(nameof(RestartWakeWordListeningAfterAI));
        if (restartWakeListenDelaySeconds <= 0f)
        {
            RestartWakeWordListeningAfterAI();
            return;
        }

        Invoke(nameof(RestartWakeWordListeningAfterAI), restartWakeListenDelaySeconds);
    }

    private void RestartWakeWordListeningAfterAI()
    {
        if (!isActiveAndEnabled || microphoneRecord == null) return;

        Debug.Log("[WhisperVAD] AI finished, restart wake word listening.");
        StartWakeWordListening();
        if (actionButton != null) actionButton.interactable = true;
    }

    private bool TryExtractCommandFromWakeText(string recognizedText, out string commandText)
    {
        commandText = string.Empty;
        if (!submitCommandInWakePhrase) return false;
        if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(wakeWord)) return false;

        int wakeIndex = recognizedText.IndexOf(wakeWord);
        if (wakeIndex < 0) return false;

        // 唤醒词后面的内容才是真正指令；前面的噪声或误识别不提交。
        string tail = recognizedText.Substring(wakeIndex + wakeWord.Length);
        tail = TrimWakeCommandText(tail);
        if (string.IsNullOrWhiteSpace(tail)) return false;

        commandText = tail;
        return true;
    }

    private static string TrimWakeCommandText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 去掉“天白，”“天白:”后面常见的标点和空格，避免提交内容前面带逗号。
        char[] separators = { ' ', '　', ',', '.', ':', ';', '，', '。', '：', '；', '、', '！', '!', '？', '?', '-', '—' };
        return text.Trim().Trim(separators);
    }

    void OnDestroy()
    {
        if (microphoneRecord != null) microphoneRecord.OnRecordStop -= OnRecordStop;
    }
}
