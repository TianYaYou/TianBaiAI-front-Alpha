using UnityEngine;
using UnityEngine.Windows.Speech;
using TMPro;
using System.Text;

public class LocalWindowsSTT : MonoBehaviour
{
    public TextMeshProUGUI tmpText;
    private DictationRecognizer dictationRecognizer;
    private StringBuilder finalResults = new StringBuilder();
    private string hypothesisText = "";

    void Start()
    {
        InitializeRecognizer();
    }

    void InitializeRecognizer()
    {
        if (dictationRecognizer != null)
        {
            DisposeRecognizer();
        }

        dictationRecognizer = new DictationRecognizer();

        dictationRecognizer.DictationHypothesis += (text) => {
            hypothesisText = text;
            UpdateTMPDisplay();
        };

        dictationRecognizer.DictationResult += (text, confidence) => {
            finalResults.AppendLine(text);
            hypothesisText = "";
            UpdateTMPDisplay();
        };

        // 核心：处理超时并自动重启
        dictationRecognizer.DictationComplete += (cause) => {
            if (cause == DictationCompletionCause.TimeoutExceeded ||
                cause == DictationCompletionCause.Complete)
            {
                Debug.Log("检测到超时或正常结束，正在自动重启监听...");
                RestartRecognizer();
            }
            else
            {
                Debug.LogError("识别由于非预期原因停止: " + cause);
            }
        };

        dictationRecognizer.DictationError += (error, hresult) => {
            Debug.LogError($"识别错误: {error} HRESULT: {hresult}");
        };

        dictationRecognizer.Start();
    }

    // 延迟一小会儿重启，防止死循环占用 CPU
    void RestartRecognizer()
    {
        Invoke("InitializeRecognizer", 0.5f);
    }

    void UpdateTMPDisplay()
    {
        tmpText.text = finalResults.ToString() + "<color=#888888>" + hypothesisText + "</color>";
    }

    void DisposeRecognizer()
    {
        if (dictationRecognizer != null)
        {
            dictationRecognizer.Stop();
            dictationRecognizer.Dispose();
            dictationRecognizer = null;
        }
    }

    private void OnDestroy() => DisposeRecognizer();
}