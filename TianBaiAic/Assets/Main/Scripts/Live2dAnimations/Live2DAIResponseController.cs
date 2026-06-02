using System;
using Live2D.Cubism.Framework.Expression;
using UnityEngine;

/// <summary>
/// 把 AI 返回的 emotion/movement 接到 Live2D。
/// 表情通过 CubismExpressionController 播放，动作通过 Animator 的 Action 参数触发。
/// </summary>
public class Live2DAIResponseController : MonoBehaviour
{
    public static Live2DAIResponseController Instance { get; private set; }

    [Header("Live2D References")]
    public CubismExpressionController expressionController;
    public Animator animator;
    public Live2DAniActionControl actionControl;

    void Awake()
    {
        Instance = this;
        AutoBind();
    }

    void Reset()
    {
        AutoBind();
    }

    public static void Apply(string emotion, string movement)
    {
        var controller = GetOrCreateController();
        if (controller == null) return;

        controller.ApplyEmotion(emotion);
        controller.ApplyMovement(movement);
    }

    public bool ApplyEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion)) return false;
        AutoBind();
        if (expressionController == null || expressionController.ExpressionsList == null) return false;

        string[] candidates = ResolveExpressionCandidates(emotion);
        for (int i = 0; i < expressionController.ExpressionsList.CubismExpressionObjects.Length; i++)
        {
            var expression = expressionController.ExpressionsList.CubismExpressionObjects[i];
            if (expression == null) continue;

            foreach (string candidate in candidates)
            {
                if (expression.name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    expressionController.CurrentExpressionIndex = i;
                    Debug.Log($"[Live2DAI] 表情: {emotion} -> {expression.name}({i})");
                    return true;
                }
            }
        }

        Debug.LogWarning($"[Live2DAI] 未找到匹配表情: {emotion}");
        return false;
    }

    public bool ApplyMovement(string movement)
    {
        if (string.IsNullOrWhiteSpace(movement) || movement == "无") return false;
        AutoBind();

        int actionId = ResolveActionId(movement);
        if (actionId <= 0)
        {
            Debug.LogWarning($"[Live2DAI] 未找到匹配动作: {movement}");
            return false;
        }

        if (actionControl != null)
        {
            actionControl.PlayAction(actionId);
        }
        else if (animator != null)
        {
            animator.SetInteger("Action", actionId);
        }
        else
        {
            return false;
        }

        Debug.Log($"[Live2DAI] 动作: {movement} -> Action {actionId}");
        return true;
    }

    private void AutoBind()
    {
        if (expressionController == null) expressionController = GetComponent<CubismExpressionController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (actionControl == null) actionControl = GetComponent<Live2DAniActionControl>();
    }

    private static Live2DAIResponseController GetOrCreateController()
    {
        if (Instance != null) return Instance;

        Instance = FindObjectOfType<Live2DAIResponseController>();
        if (Instance != null) return Instance;

        var expression = FindObjectOfType<CubismExpressionController>();
        if (expression == null) return null;

        Instance = expression.gameObject.GetComponent<Live2DAIResponseController>();
        if (Instance == null) Instance = expression.gameObject.AddComponent<Live2DAIResponseController>();
        Instance.AutoBind();
        return Instance;
    }

    private static string[] ResolveExpressionCandidates(string emotion)
    {
        if (emotion.Contains("高兴") || emotion.Contains("开心")) return new[] { "心心眼", "星星眼" };
        if (emotion.Contains("害怕") || emotion.Contains("惊")) return new[] { "圈圈眼", "悲伤" };
        if (emotion.Contains("嗔怪") || emotion.Contains("生气")) return new[] { "生气" };
        if (emotion.Contains("失望") || emotion.Contains("悲伤")) return new[] { "悲伤" };
        if (emotion.Contains("疑问") || emotion.Contains("困惑")) return new[] { "圈圈眼", "鸡爪眼" };
        if (emotion.Contains("挑逗") || emotion.Contains("调皮")) return new[] { "猫耳", "心心眼" };
        return new[] { emotion };
    }

    private static int ResolveActionId(string movement)
    {
        if (movement.Contains("挥手") || movement.Contains("招手")) return 1;
        if (movement.Contains("点头") || movement.Contains("开心") || movement.Contains("靠近")) return 1;
        if (movement.Contains("摇头") || movement.Contains("拒绝") || movement.Contains("生气")) return 2;

        // 允许 prompt 直接返回“Action1/Action2/1/2”这类简写，方便调试。
        string digits = string.Empty;
        foreach (char c in movement)
        {
            if (char.IsDigit(c)) digits += c;
        }

        if (int.TryParse(digits, out int id)) return id;
        return 0;
    }
}
