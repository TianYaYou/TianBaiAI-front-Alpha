using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class GlobalKeyListener : MonoBehaviour
{
#if WinAPI
    private static IntPtr hookId = IntPtr.Zero;
    private static LowLevelKeyboardProc proc = HookCallback;
    private static Thread hookThread;

    // 映射虚拟键码到 Unity 的 KeyCode
    private static readonly Dictionary<int, KeyCode> vkCodeToKeyCode = new()
    {
#region 映射
        { 0x08, KeyCode.Backspace },
        { 0x09, KeyCode.Tab },
        { 0x0D, KeyCode.Return },
        { 0x10, KeyCode.LeftShift },
        { 0x11, KeyCode.LeftControl },
        { 0x12, KeyCode.LeftAlt },
        { 0x14, KeyCode.CapsLock },
        { 0x1B, KeyCode.Escape },
        { 0x20, KeyCode.Space },
        { 0x21, KeyCode.PageUp },
        { 0x22, KeyCode.PageDown },
        { 0x23, KeyCode.End },
        { 0x24, KeyCode.Home },
        { 0x25, KeyCode.LeftArrow },
        { 0x26, KeyCode.UpArrow },
        { 0x27, KeyCode.RightArrow },
        { 0x28, KeyCode.DownArrow },
        { 0x2D, KeyCode.Insert },
        { 0x2E, KeyCode.Delete },
        { 0x30, KeyCode.Alpha0 },
        { 0x31, KeyCode.Alpha1 },
        { 0x32, KeyCode.Alpha2 },
        { 0x33, KeyCode.Alpha3 },
        { 0x34, KeyCode.Alpha4 },
        { 0x35, KeyCode.Alpha5 },
        { 0x36, KeyCode.Alpha6 },
        { 0x37, KeyCode.Alpha7 },
        { 0x38, KeyCode.Alpha8 },
        { 0x39, KeyCode.Alpha9 },
        { 0x41, KeyCode.A },
        { 0x42, KeyCode.B },
        { 0x43, KeyCode.C },
        { 0x44, KeyCode.D },
        { 0x45, KeyCode.E },
        { 0x46, KeyCode.F },
        { 0x47, KeyCode.G },
        { 0x48, KeyCode.H },
        { 0x49, KeyCode.I },
        { 0x4A, KeyCode.J },
        { 0x4B, KeyCode.K },
        { 0x4C, KeyCode.L },
        { 0x4D, KeyCode.M },
        { 0x4E, KeyCode.N },
        { 0x4F, KeyCode.O },
        { 0x50, KeyCode.P },
        { 0x51, KeyCode.Q },
        { 0x52, KeyCode.R },
        { 0x53, KeyCode.S },
        { 0x54, KeyCode.T },
        { 0x55, KeyCode.U },
        { 0x56, KeyCode.V },
        { 0x57, KeyCode.W },
        { 0x58, KeyCode.X },
        { 0x59, KeyCode.Y },
        { 0x5A, KeyCode.Z },
        { 0x60, KeyCode.Keypad0 },
        { 0x61, KeyCode.Keypad1 },
        { 0x62, KeyCode.Keypad2 },
        { 0x63, KeyCode.Keypad3 },
        { 0x64, KeyCode.Keypad4 },
        { 0x65, KeyCode.Keypad5 },
        { 0x66, KeyCode.Keypad6 },
        { 0x67, KeyCode.Keypad7 },
        { 0x68, KeyCode.Keypad8 },
        { 0x69, KeyCode.Keypad9 },
        { 0x6A, KeyCode.KeypadMultiply },
        { 0x6B, KeyCode.KeypadPlus },
        { 0x6C, KeyCode.KeypadEnter },
        { 0x6D, KeyCode.KeypadMinus },
        { 0x6E, KeyCode.KeypadPeriod },
        { 0x70, KeyCode.F1 },
        { 0x71, KeyCode.F2 },
        { 0x72, KeyCode.F3 },
        { 0x73, KeyCode.F4 },
        { 0x74, KeyCode.F5 },
        { 0x75, KeyCode.F6 },
        { 0x76, KeyCode.F7 },
        { 0x77, KeyCode.F8 },
        { 0x78, KeyCode.F9 },
        { 0x79, KeyCode.F10 },
        { 0x7A, KeyCode.F11 },
        { 0x7B, KeyCode.F12 },
        { 0x90, KeyCode.Numlock },
        { 0x91, KeyCode.ScrollLock },
        { 0xA0, KeyCode.LeftShift },
        { 0xA1, KeyCode.RightShift },
        { 0xA2, KeyCode.LeftControl },
        { 0xA3, KeyCode.RightControl },
        { 0xA4, KeyCode.LeftAlt },
        { 0xA5, KeyCode.RightAlt },
        { 0xBA, KeyCode.Semicolon },
        { 0xBB, KeyCode.Equals },
        { 0xBC, KeyCode.Comma },
        { 0xBD, KeyCode.Minus },
        { 0xBE, KeyCode.Period },
        { 0xBF, KeyCode.Slash },
        { 0xC0, KeyCode.BackQuote },
        { 0xDB, KeyCode.LeftBracket },
        { 0xDC, KeyCode.Backslash },
        { 0xDD, KeyCode.RightBracket },
        { 0xDE, KeyCode.Quote },
#endregion
    };

    void OnEnable()
    {
        hookThread = new Thread(() =>
        {
            hookId = SetHook(proc);
            if (hookId == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError("Failed to set global keyboard hook. Error: " + Marshal.GetLastWin32Error());
                return;
            }

            // 消息循环保持钩子活跃
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0)) { }
        });
        hookThread.IsBackground = true;
        hookThread.Start();

        DontDestroyOnLoad(gameObject);
    }

    void OnDisable()
    {
        if (hookId != IntPtr.Zero)
        {
            if (UnhookWindowsHookEx(hookId))
            {
                UnityEngine.Debug.Log("Global keyboard hook unhooked.");
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to unhook global keyboard hook. Error: " + Marshal.GetLastWin32Error());
            }
            hookId = IntPtr.Zero;
        }
        if (hookThread != null && hookThread.IsAlive)
        {
            hookThread.Abort(); // 强制终止线程，更安全的方法是发送消息让线程自行退出
            hookThread = null;
        }
        GlobalInput.Reset();
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            KeyCode unityKey = ConvertVKToKeyCode(vkCode);

            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                GlobalInput.SetKeyState(unityKey, true);
            else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                GlobalInput.SetKeyState(unityKey, false);
        }

        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private static KeyCode ConvertVKToKeyCode(int vkCode)
    {
        return vkCodeToKeyCode.TryGetValue(vkCode, out var keyCode) ? keyCode : KeyCode.None;
    }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }
#endif
}

public static class GlobalInput
{
    private static readonly HashSet<KeyCode> _pressedKeys = new();
    private static readonly HashSet<KeyCode> _downBuffer = new();

    public static bool GetKey(KeyCode key)
    {
        return _pressedKeys.Contains(key);
    }

    public static bool GetKeyDown(KeyCode key)
    {
        if (_pressedKeys.Contains(key) && !_downBuffer.Contains(key))
        {
            _downBuffer.Add(key);
            return true;
        }
        return false;
    }

    public static bool GetKeyUp(KeyCode key)
    {
        if (!_pressedKeys.Contains(key) && _downBuffer.Contains(key))
        {
            _downBuffer.Remove(key);
            return true;
        }
        return false;
    }

    internal static void SetKeyState(KeyCode key, bool isDown)
    {
        if (isDown)
            _pressedKeys.Add(key);
        else
            _pressedKeys.Remove(key);
    }

    // 在每一帧开始时调用，以清除 _downBuffer
    public static void ResetDownBuffer()
    {
        _downBuffer.Clear();
    }

    // 可选：清理所有按键状态（可在程序暂停或退出时调用，OnDisable 中已调用）
    public static void Reset()
    {
        _pressedKeys.Clear();
        _downBuffer.Clear();
    }
}

// 你需要在 Unity 的一个脚本中每一帧调用 GlobalInput.ResetDownBuffer()
public class GlobalInputFrameResetter : MonoBehaviour
{
    void Update()
    {
        GlobalInput.ResetDownBuffer();
    }
}