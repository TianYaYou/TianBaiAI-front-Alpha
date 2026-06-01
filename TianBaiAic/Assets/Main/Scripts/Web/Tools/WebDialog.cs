using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WebDialog : MonoBehaviour
{
    public static WebDialog webDialog;
    public GameObject OutputText;
    public GameObject InputText;
    [Header("Compatibility")]
    public bool enableLegacyWebApiFallback = false;

    void Start()
    {
        webDialog = this;
    }

    private static WebDialog GetInstance()
    {
        if (webDialog != null) return webDialog;

        webDialog = Object.FindObjectOfType<WebDialog>();
        return webDialog;
    }

    private static bool EnsureInstance()
    {
        var inst = GetInstance();
        return inst != null;
    }

    private static bool TryGetInputField(out TMP_InputField field)
    {
        field = null;
        var inst = GetInstance();
        if (inst == null || inst.InputText == null)
        {
            return false;
        }

        field = inst.InputText.GetComponent<TMP_InputField>();
        return field != null;
    }

    public static void Dialog(string message)
    {
        if (!EnsureInstance()) return;
        var output = webDialog.OutputText != null ? webDialog.OutputText.GetComponent<TextMeshProUGUI>() : null;
        if (output != null)
        {
            output.text = message;
            webDialog.OutputText.SetActive(true);
        }

        if (Live2DLookControl.instance != null)
        {
            Live2DLookControl.SetLookEyeActive(50);
        }
    }

    // 供 UI 按钮调用：保持原行为（提交后清空输入框）
    public static void InputDialog()
    {
        InputDialogInternal(true);
    }

    // 内部提交入口：可选择是否在提交后清空输入框
    private static void InputDialogInternal(bool clearAfterRead)
    {
        if (!TryGetInputField(out var field)) return;
        string input = field.text;
        if (clearAfterRead) field.text = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogWarning("[WebDialog] InputDialog skipped: input is empty.");
            return;
        }

        Debug.Log($"[WebDialog] InputDialog send: {input}");

        // Preferred path: AIapiuse (launcher-driven config).
        if (AIChatBridge.TrySend(input))
        {
            // Debug 模式下保留文本，便于你确认“识别文本 -> InputField -> 提交”链路
            if (!clearAfterRead) field.text = input;
            return;
        }

        if (webDialog != null && webDialog.enableLegacyWebApiFallback)
        {
            // Compatibility fallback: legacy Python WebApi.
            WebApi.Upmassage(input, "dialog");
            return;
        }

        string configPath = AIConfigLoader.GetConfigPath();
        Dialog($"AI config not ready. Please set launcher config:\n{configPath}");
        StatusBox.Warning("AI config missing, legacy fallback is disabled.");
    }

    public static bool SubmitText(string text)
    {
        if (!TryGetInputField(out var field))
        {
            Debug.LogError("[WebDialog] SubmitText failed: TMP_InputField not found.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[WebDialog] SubmitText skipped: text is empty.");
            return false;
        }

        // 关键调试日志：确认识别文本是否真的写入了 InputField
        Debug.Log($"[WebDialog] Fill InputField with: {text}");
        field.text = text;
        // 语音链路提交后不清空输入框，便于你在 Inspector/Hierarchy 里看到最后一条文本
        InputDialogInternal(false);
        Debug.Log($"[WebDialog] InputField after submit: {field.text}");
        return true;
    }

    public static void GitInputDialogControl(int type)
    {
        if (type == 0) webDialog.InputText.SetActive(false);
        else if (type == 1) webDialog.InputText.SetActive(true);
    }

    public static void GitOutputDialogControl(int type)
    {
        if (type == 0) webDialog.OutputText.SetActive(false);
        else if (type == 1) webDialog.OutputText.SetActive(true);
    }

    public static void SetInputBackgroundText(string message)
    {
        webDialog.InputText.GetComponentInChildren<TextMeshProUGUI>().text = message;
    }
}
