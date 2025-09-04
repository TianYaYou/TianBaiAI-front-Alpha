using UnityEngine;
using UnityEngine.UI;
using Live2D.Cubism.Framework.Expression;
using System.Collections.Generic;
using System;
using TMPro;

public class Live2DExpressionButtonCreator : MonoBehaviour
{
    private static Live2DExpressionButtonCreator instance;

    [Header("UI ����")]
    public Transform buttonContainer; // ��ť�ĸ�����������һ������ VerticalLayoutGroup �� Panel��  
    public Button buttonPrefab;       // Ԥ�Ƶİ�ť������ Inspector ��ָ����  

    [Header("Live2D ����")]
    public CubismExpressionController expressionController; // ģ���ϵ� ExpressionController  

    public static void SetExpression(int index)
    {
        if (instance == null || instance.expressionController == null)
        {
            Debug.LogError("Live2DExpressionButtonCreator ʵ���� ExpressionController δ���ã�");
            return;
        }

        // ���õ�ǰ��������  
        instance.expressionController.CurrentExpressionIndex = index;
    }

    void Start()
    {
        instance = this;
        // ��ȡ����б�  
        var expressionList = expressionController.ExpressionsList;
       
        if (expressionList == null || expressionList.CubismExpressionObjects == null)
        {
            Debug.LogError("δ�ҵ�����б�����б�Ϊ�գ�");
            return;
        }

        // ����ÿ����������Ӧ�İ�ť  
        foreach (var expression in expressionList.CubismExpressionObjects)
        {
            if (expression == null) continue;

            // ʵ������ť  
            var newButton = Instantiate(buttonPrefab, buttonContainer);

            // ���ð�ť�ı�Ϊ�������  
            var buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = expression.name;
            }

            // Ϊ��ť��ӵ���¼������Ŷ�Ӧ�ı��  
            newButton.onClick.AddListener(() =>
            {
                PlayExpression(expression);
            });
        }

        void PlayExpression(CubismExpressionData expression)
        {
            //��ȡ��ǰ��������
            int index = Array.IndexOf(expressionList.CubismExpressionObjects, expression);
            if (index >= 0)
            {
                // ���ű��  
                expressionController.CurrentExpressionIndex = index;
            }
        }
    }
}