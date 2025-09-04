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

    void Start()
    {
        webDialog = this;
    }

    public static void Dialog(string message)
    {
        webDialog.OutputText.GetComponent<TextMeshProUGUI>().text = message;
        webDialog.OutputText.SetActive(true);
        Live2DLookControl.SetLookEyeActive(50);
    }
    public static void InputDialog()
    {
        WebApi.Upmassage(webDialog.InputText.GetComponent<TMP_InputField>().text, "dialog");
        webDialog.InputText.GetComponent<TMP_InputField>().text = "";
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
