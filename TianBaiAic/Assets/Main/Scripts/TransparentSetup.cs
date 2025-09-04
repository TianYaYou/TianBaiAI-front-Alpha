using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityRawInput;

public class TransparentSetup : MonoBehaviour
{
#if !UNITY_EDITOR
    public static TransparentSetup Instance;

    private uint originalStyle;
    private uint originalExStyle;
    private IntPtr hWnd = IntPtr.Zero; // 修复：初始化字段 hWnd


    public void RestoreWindow()  // 恢复窗口
    {
        if (hWnd == IntPtr.Zero) return;

        // 禁止点击
        SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle);
    }

    void OnApplicationQuit()
    {
        RestoreWindow();
    }

    void OnDestroy()
    {
        RestoreWindow();
    }

    #region Windows API

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint LWA_COLORKEY = 0x00000001;
    private const uint LWA_ALPHA = 0x00000002;

    #endregion

    bool ableToClick = true; // 是否允许点击
    void Start()
    {
        hWnd = GetActiveWindow(); // 使用字段 hWnd，而不是声明新的局部变量
        originalStyle = GetWindowLong(hWnd, -16);       // GWL_STYLE
        originalExStyle = GetWindowLong(hWnd, -20);     // GWL_EXSTYLE

        //RawInput.Start();
        //RawInput.WorkInBackground = true;

        // 获取原扩展样式
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

        Instance = this;

        // 设置窗口为透明 + 点击穿透
        //SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);


    }
    public static void SetTransparent(bool isTransparent)
    {
        if (isTransparent)
        {
            // 设置窗口为透明 + 点击穿透
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            // 恢复窗口
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle);
        }
    }

    public static void SetClickable(bool isClickable)
    {
        if (isClickable)
        {
            // 允许点击
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            // 禁止点击
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || RawInput.IsKeyDown(RawKey.Tab))
        {
            RestoreWindow();
        }

        //检查鼠标是否点击
        if (Input.GetMouseButtonDown(0))
        {
            //检查鼠标上是否有遮挡物体
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // 如果点击到物体，恢复窗口
                RestoreWindow();
            }
                // 如果点击到UI，恢复窗口
            else if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                RestoreWindow();
            }
            else
            {
                // 如果没有点击到物体，设置窗口为透明 + 点击穿透
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
        }

        
        if (Input.GetKeyDown(KeyCode.C) || GlobalInput.GetKeyDown(KeyCode.C))
        {
            // 切换点击穿透状态
            ableToClick = !ableToClick;
            if (ableToClick)
            {
                // 允许点击
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            else
            {
                // 禁止点击
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle);
            }
        }
        
    }
#endif
}
