using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoResizePanel : MonoBehaviour
{
    public RectTransform panel;
    public TextMeshProUGUI text;
    public float padding = 20f;
    public float smoothSpeed = 10f;

    [Header("Display Timing Settings")]
    public float baseDisplayTime = 2f;
    public float extraTimePerChar = 0.1f;
    public float fadeDuration = 1f;

    private CanvasGroup canvasGroup;
    private string lastText = "";
    private int version = 0; // 控制器版本号，确保打断前一协程



    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    void Update()
    {
        // 检测文字变化
        if (text.text != lastText)
        {
            lastText = text.text;
            ShowPanel();

            // 打断前一个协程版本
            version++;

            float displayDuration = baseDisplayTime + extraTimePerChar * text.text.Length;
            StartCoroutine(DelayedFadeOut(version, displayDuration));
        }

        // 高度平滑调整
        float preferred = text.preferredHeight + padding;
        float currentHeight = panel.sizeDelta.y;
        float newHeight = Mathf.Lerp(currentHeight, preferred, Time.deltaTime * smoothSpeed);

        Vector2 sizeDelta = panel.sizeDelta;
        sizeDelta.y = newHeight;
        panel.sizeDelta = sizeDelta;
    }

    void ShowPanel()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    IEnumerator DelayedFadeOut(int currentVersion, float displayDuration)
    {
        yield return new WaitForSeconds(displayDuration);

        // 如果在等待期间版本号被修改，终止协程
        if (currentVersion != version) yield break;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeDuration)
        {
            // 若被新版本打断，则退出
            if (currentVersion != version) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
