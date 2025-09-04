using UnityEngine;
using UnityEngine.UI;
using Live2D.Cubism.Framework.Expression;
using System.Collections.Generic;
using System;
using TMPro;

public class Live2DExpressionButtonCreator : MonoBehaviour
{
    private static Live2DExpressionButtonCreator instance;

    [Header("UI 设置")]
    public Transform buttonContainer; // 按钮的父容器（例如一个带有 VerticalLayoutGroup 的 Panel）  
    public Button buttonPrefab;       // 预制的按钮（需在 Inspector 中指定）  

    [Header("Live2D 设置")]
    public CubismExpressionController expressionController; // 模型上的 ExpressionController  

    public static void SetExpression(int index)
    {
        if (instance == null || instance.expressionController == null)
        {
            Debug.LogError("Live2DExpressionButtonCreator 实例或 ExpressionController 未设置！");
            return;
        }

        // 设置当前表达的索引  
        instance.expressionController.CurrentExpressionIndex = index;
    }

    void Start()
    {
        instance = this;
        // 获取表达列表  
        var expressionList = expressionController.ExpressionsList;
       
        if (expressionList == null || expressionList.CubismExpressionObjects == null)
        {
            Debug.LogError("未找到表达列表或表达列表为空！");
            return;
        }

        // 遍历每个表达，创建对应的按钮  
        foreach (var expression in expressionList.CubismExpressionObjects)
        {
            if (expression == null) continue;

            // 实例化按钮  
            var newButton = Instantiate(buttonPrefab, buttonContainer);

            // 设置按钮文本为表达名称  
            var buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = expression.name;
            }

            // 为按钮添加点击事件，播放对应的表达  
            newButton.onClick.AddListener(() =>
            {
                PlayExpression(expression);
            });
        }

        void PlayExpression(CubismExpressionData expression)
        {
            //获取当前表达的索引
            int index = Array.IndexOf(expressionList.CubismExpressionObjects, expression);
            if (index >= 0)
            {
                // 播放表达  
                expressionController.CurrentExpressionIndex = index;
            }
        }
    }
}