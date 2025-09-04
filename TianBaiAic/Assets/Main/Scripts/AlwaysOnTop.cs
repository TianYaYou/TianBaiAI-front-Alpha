using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class AlwaysOnTop : MonoBehaviour
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    void Start()
    {
        // ��ȡ���ھ��
        IntPtr hWnd = FindWindow(null, Application.productName);
        if (hWnd != IntPtr.Zero)
        {
            // ���ô���Ϊ�ö�
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
        else
        {
            Debug.LogError("δ�ҵ����ھ������ȷ�����ڱ����� Application.productName ƥ�䡣");
        }
    }
}
