using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatusBox : MonoBehaviour
{
    public Color LogColor = Color.white;
    public Color WarningColor = Color.yellow;
    public Color ErrorColor = Color.red;
    public Color SuccessColor = Color.green;

    public GameObject MainBox;
    public GameObject ConnectBox;
    public GameObject DebugBox;

    public static StatusBox statusBox;

    void Start()
    {
        statusBox = this;
    }

    public float connect_time = 1.0f;
    public float connect_time_max = 10.0f;

    void Update()
    {
        if (connect_time > 0.0f)
        {
            connect_time -= Time.deltaTime;
            if (connect_time <= 0.0f)
            {
                connect_time = 0.0f;
                ConnectBox.GetComponent<TextMeshProUGUI>().color = ErrorColor;
                ConnectBox.GetComponent<TextMeshProUGUI>().text = "后端连接丢失";
            }
        }
        if (log_time > 0.0f)
        {
            log_time -= Time.deltaTime;
            if (log_time <= 0.0f)
            {
                log_time = 0.0f;
                DebugBox.GetComponent<TextMeshProUGUI>().text = "";
            }
        }
    }

    public static void Heartbeat()
    {
        if (statusBox != null)
        {
            statusBox.connect_time = statusBox.connect_time_max;
            statusBox.ConnectBox.GetComponent<TextMeshProUGUI>().color = statusBox.SuccessColor;
            statusBox.ConnectBox.GetComponent<TextMeshProUGUI>().text = "后端连接正常";
        }
    }

    float log_time = 2.0f;
    public static void Log(string message)
    {
        if (statusBox != null)
        {
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().color = statusBox.LogColor;
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().text = message;
            statusBox.log_time = 2.0f;
        }
    }
    public static void Warning(string message)
    {
        if (statusBox != null)
        {
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().color = statusBox.WarningColor;
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().text = message;
            statusBox.log_time = 2.0f;
        }
    }
    public static void Error(string message)
    {
        if (statusBox != null)
        {
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().color = statusBox.ErrorColor;
            statusBox.DebugBox.GetComponent<TextMeshProUGUI>().text = message;
            statusBox.log_time = 2.0f;
        }
    }
}
