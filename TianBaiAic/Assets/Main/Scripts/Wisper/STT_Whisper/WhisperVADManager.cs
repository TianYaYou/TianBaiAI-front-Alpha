using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

public class WhisperVADManager_History : MonoBehaviour
{
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
        _currentState = ListenState.ListeningForWakeWord;

        microphoneRecord.vadStopTime = wakeWordSilenceSeconds;
        microphoneRecord.StartRecord();

        buttonText.text = "监听唤醒词中...";
        buttonText.color = Color.gray;
        UpdateUIStatus($"<color=#808080><i>[系统：正在监听唤醒词 \"{wakeWord}\"]</i></color>");
    }

    private void StartCommandListening()
    {
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

        // Use the same path as manual input: fill scene Input, then call InputDialog().
        bool submitted = WebDialog.SubmitText(recognizedText);
        Debug.Log($"[WhisperVAD] SubmitText result: {submitted}");
        if (!submitted)
        {
            // Fallback to direct bridge if UI input is unavailable.
            AIChatBridge.TrySend(recognizedText);
            Debug.LogWarning("[WhisperVAD] SubmitText 失败，已回退 AIChatBridge 直连。");
        }

        StartWakeWordListening();
        if (actionButton != null) actionButton.interactable = true;
    }

    void OnDestroy()
    {
        if (microphoneRecord != null) microphoneRecord.OnRecordStop -= OnRecordStop;
    }
}
