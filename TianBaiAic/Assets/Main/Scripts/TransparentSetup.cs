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
    private IntPtr hWnd = IntPtr.Zero; // �޸�����ʼ���ֶ� hWnd


    public void RestoreWindow()  // �ָ�����
    {
        if (hWnd == IntPtr.Zero) return;

        // ��ֹ���
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

    bool ableToClick = true; // �Ƿ�������
    void Start()
    {
        hWnd = GetActiveWindow(); // ʹ���ֶ� hWnd�������������µľֲ�����
        originalStyle = GetWindowLong(hWnd, -16);       // GWL_STYLE
        originalExStyle = GetWindowLong(hWnd, -20);     // GWL_EXSTYLE

        //RawInput.Start();
        //RawInput.WorkInBackground = true;

        // ��ȡԭ��չ��ʽ
        var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

        Instance = this;

        // ���ô���Ϊ͸�� + �����͸
        //SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);


    }
    public static void SetTransparent(bool isTransparent)
    {
        if (isTransparent)
        {
            // ���ô���Ϊ͸�� + �����͸
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            // �ָ�����
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle);
        }
    }

    public static void SetClickable(bool isClickable)
    {
        if (isClickable)
        {
            // ������
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            // ��ֹ���
            SetWindowLong(Instance.hWnd, GWL_EXSTYLE, Instance.originalExStyle);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || RawInput.IsKeyDown(RawKey.Tab))
        {
            RestoreWindow();
        }

        //�������Ƿ���
        if (Input.GetMouseButtonDown(0))
        {
            //���������Ƿ����ڵ�����
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // �����������壬�ָ�����
                RestoreWindow();
            }
                // ��������UI���ָ�����
            else if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                RestoreWindow();
            }
            else
            {
                // ���û�е�������壬���ô���Ϊ͸�� + �����͸
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
        }

        
        if (Input.GetKeyDown(KeyCode.C) || GlobalInput.GetKeyDown(KeyCode.C))
        {
            // �л������͸״̬
            ableToClick = !ableToClick;
            if (ableToClick)
            {
                // ������
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            else
            {
                // ��ֹ���
                SetWindowLong(hWnd, GWL_EXSTYLE, originalExStyle);
            }
        }
        
    }
#endif
}
