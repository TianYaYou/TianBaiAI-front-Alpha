using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景 UI 对话桥接脚本。
/// 负责读取 InputField、把用户文本交给 AIConversationController，并把最终回复写入 OutputText。
/// Whisper 识别结果也会先经过这里，因此手动输入和语音输入最终走同一条 AI 调用链路。
/// </summary>
public class WebDialog : MonoBehaviour
{
    public static WebDialog webDialog;

    // OutputText：AI 最终说出来的内容显示位置。
    public GameObject OutputText;

    // InputText：场景里的 TMP_InputField。语音识别结果也会写进这里，方便直接在场景中 debug。
    public GameObject InputText;

    [Header("Compatibility")]
    [Tooltip("仅用于兼容旧 Python 后端；当前新链路默认不走 WebApi。")]
    public bool enableLegacyWebApiFallback = false;

    void Start()
    {
        // 保留旧代码的静态调用方式，方便 WebApi/按钮/Whisper 直接调用 WebDialog。
        webDialog = this;
    }

    private static WebDialog GetInstance()
    {
        if (webDialog != null) return webDialog;

        webDialog = UnityEngine.Object.FindObjectOfType<WebDialog>();
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
        // AI 回复、错误提示、流式文本更新都会走这个入口，统一写到 OutputText。
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

    // 供 UI 按钮调用：保持原行为（提交后清空输入框）。
    public static void InputDialog()
    {
        InputDialogInternal(true);
    }

    // 内部提交入口：clearAfterRead=true 表示手动按钮提交后清空；false 表示语音调试时保留 InputField 内容。
    // onRequestFinished 用于语音链路：AI 完整回答或失败后再通知 Whisper 重新开始监听。
    private static void InputDialogInternal(bool clearAfterRead, Action onRequestFinished = null)
    {
        if (!TryGetInputField(out var field)) return;
        string input = field.text;
        if (clearAfterRead) field.text = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogWarning("[WebDialog] InputDialog skipped: input is empty.");
            onRequestFinished?.Invoke();
            return;
        }

        Debug.Log($"[WebDialog] InputDialog send: {input}");
        bool requestFinished = false;
        void FinishRequestOnce()
        {
            if (requestFinished) return;
            requestFinished = true;
            onRequestFinished?.Invoke();
        }

        // 优先使用新的 AI 对话控制器：读取配置 -> 调 API -> 把回复写进 Output。
        if (AIConversationController.TryAsk(
            input,
            onStreamUpdate: Dialog,
            onComplete: reply =>
            {
                Dialog(reply);
                FinishRequestOnce();
            },
            onError: error =>
            {
                Dialog($"AI config or request failed:\n{error}");
                FinishRequestOnce();
            }))
        {
            // Debug 模式下保留文本，便于你确认“识别文本 -> InputField -> 提交”链路。
            if (!clearAfterRead) field.text = input;
            return;
        }

        if (webDialog != null && webDialog.enableLegacyWebApiFallback)
        {
            // Compatibility fallback: legacy Python WebApi.
            WebApi.Upmassage(input, "dialog");
            FinishRequestOnce();
            return;
        }

        StatusBox.Warning("AI request was not started. Check the AI config message in Output.");
        FinishRequestOnce();
    }

    public static bool SubmitText(string text, Action onRequestFinished = null)
    {
        // Whisper 调用入口：先把识别文本放入 InputField，再复用 InputDialogInternal 提交。
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

        // 关键调试日志：确认识别文本是否真的写入了 InputField。
        Debug.Log($"[WebDialog] Fill InputField with: {text}");
        field.text = text;
        // 语音链路提交后不清空输入框，便于你在 Inspector/Hierarchy 里看到最后一条文本。
        InputDialogInternal(false, onRequestFinished);
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
