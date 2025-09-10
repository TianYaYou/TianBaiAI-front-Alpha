using TMPro;
using UnityEngine;

public class LogToTMP : MonoBehaviour
{
    [Header("UI 绑定")]
    public TextMeshProUGUI logText; 
    public int maxLines = 10;       // 最多显示多少行日志

    private static System.Collections.Generic.Queue<string> logs = new System.Collections.Generic.Queue<string>();

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        string prefix = "";

        switch (type)
        {
            case LogType.Warning: prefix = "<color=yellow>[Warning]</color> "; break;
            case LogType.Error: prefix = "<color=red>[Error]</color> "; break;
            case LogType.Exception: prefix = "<color=red>[Exception]</color> "; break;
            default: prefix = "[Log] "; break;
        }

        logs.Enqueue(prefix + logString);

        while (logs.Count > maxLines)
            logs.Dequeue();

        if (logText != null)
            logText.text = string.Join("\n", logs.ToArray());
    }
}
