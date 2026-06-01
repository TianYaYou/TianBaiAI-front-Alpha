using UnityEngine;
using Whisper;
using Whisper.Utils; // 必须引用这个命名空间来获取录音组件
using TMPro;
using UnityEngine.UI;

public class WhisperTurboManager : MonoBehaviour
{
    [Header("核心引用")]
    public WhisperManager whisperManager;
    public MicrophoneRecord microphoneRecord; // 关键：手动拖入麦克风录音组件

    [Header("UI 绑定")]
    public TextMeshProUGUI tmpText;
    public Button actionButton;
    public TextMeshProUGUI buttonText;

    private WhisperStream _stream;
    private string _allText = "";
    private bool _isRecording = false;

    async void Start()
    {
        // 1. 初始化模型
        actionButton.interactable = false;
        buttonText.text = "模型加载中...";
        await whisperManager.InitModel();

        // 2. 配置流参数 (关键修改：将录音组件传入 CreateStream)
        // 这样 _stream 才会自动从 microphoneRecord 获取音频数据
        _stream = await whisperManager.CreateStream(microphoneRecord);

        // 3. 实时流输出回调 (注意检查是 .Text 还是 .Result)
        _stream.OnSegmentUpdated += (segment) =>
        {
            // segment.Text 是目前大多数版本的标准写法
            tmpText.text = _allText + "<color=#00FF00>" + segment.Result + "</color>";
        };

        // 4. 段落完成回调
        _stream.OnSegmentFinished += (segment) =>
        {
            _allText += segment.Result + " ";
            tmpText.text = _allText;
        };

        buttonText.text = "开始监听";
        actionButton.interactable = true;
        actionButton.onClick.AddListener(ToggleRecording);
    }

    public void ToggleRecording()
    {
        if (!_isRecording)
        {
            // 启动顺序：先启流，再启录音
            _stream.StartStream();
            microphoneRecord.StartRecord();

            _isRecording = true;
            buttonText.text = "停止监听";
            buttonText.color = Color.red;
            Debug.Log("Whisper 本地监听启动成功");
        }
        else
        {
            // 关闭顺序：先关录音，再关流
            microphoneRecord.StopRecord();
            _stream.StopStream();

            _isRecording = false;
            buttonText.text = "开始监听";
            buttonText.color = Color.white;
            Debug.Log("Whisper 本地监听已停止");
        }
    }

    private void OnDestroy()
    {
        _stream?.StopStream();
    }
}