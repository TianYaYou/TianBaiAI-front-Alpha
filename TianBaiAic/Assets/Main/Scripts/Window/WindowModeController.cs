using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 当前桌宠窗口模式。
/// DesktopPet = 小尺寸透明桌宠窗口；Fullscreen = 占满屏幕的展示模式。
/// </summary>
public enum DesktopPetWindowMode
{
    DesktopPet,
    Fullscreen
}

/// <summary>
/// 桌宠窗口模式控制器。
/// 职责：
/// 1. 管理桌宠模式 / 全屏模式切换；
/// 2. 提供窗口拖动、窗口缩放、窗口尺寸恢复等统一接口；
/// 3. 尽量只使用 Win32 改真实窗口大小，避免再次调用 Unity Screen API 干扰透明窗口样式。
/// </summary>
public class WindowModeController : MonoBehaviour
{
    private static WindowModeController _instance;
    private static bool _isApplicationQuitting;

    /// <summary>
    /// 对外单例入口。
    /// 如果场景里没有手动放置控制器，就自动创建一个，避免必须先改场景资源。
    /// </summary>
    public static WindowModeController Instance
    {
        get
        {
            if (_isApplicationQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<WindowModeController>();
#else
                _instance = FindObjectOfType<WindowModeController>();
#endif
            }

            if (_instance == null)
            {
                var go = new GameObject("WindowModeController");
                _instance = go.AddComponent<WindowModeController>();
            }

            return _instance;
        }
    }

    [Header("Initial Mode")]
    [Tooltip("进入主场景后，是否自动应用初始窗口模式。开启后，当前版本默认会把主界面切回桌宠模式。")]
    public bool applyInitialModeOnStart = true;

    [Tooltip("首次初始化时应用的默认窗口模式。保留 Fullscreen，方便后续做沉浸展示。")]
    public DesktopPetWindowMode initialMode = DesktopPetWindowMode.DesktopPet;

    [Header("Desktop Window Size")]
    [Tooltip("首次进入桌宠模式时的目标宽度。")]
    public int desktopWindowWidth = 780;

    [Tooltip("首次进入桌宠模式时的目标高度。")]
    public int desktopWindowHeight = 980;

    [Tooltip("桌宠窗口最小宽度，避免缩得太小后角色和 UI 无法操作。")]
    public int minDesktopWindowWidth = 420;

    [Tooltip("桌宠窗口最小高度，避免缩得太小后角色和 UI 无法操作。")]
    public int minDesktopWindowHeight = 560;

    [Tooltip("桌宠窗口最大宽度占当前主屏幕宽度的比例。")]
    [Range(0.2f, 1f)]
    public float maxDesktopWidthScreenRatio = 0.8f;

    [Tooltip("桌宠窗口最大高度占当前主屏幕高度的比例。")]
    [Range(0.2f, 1f)]
    public float maxDesktopHeightScreenRatio = 0.9f;

    [Header("Desktop Window Behaviour")]
    [Tooltip("是否保持窗口置顶。桌宠模式通常建议一直置顶。")]
    public bool keepWindowTopMost = true;

    [Tooltip("桌宠模式下是否优先使用 Windows 原生拖窗消息。原生拖窗比逐帧 SetWindowPos 更不容易出现拖动后局部渲染不全。")]
    public bool useNativeWindowDrag = true;

    [Tooltip("是否允许窗口部分拖出屏幕。")]
    public bool allowPartialOffscreen = true;

    [Tooltip("允许部分离屏时，至少要保留多少像素仍然留在屏幕内，防止窗口完全丢失。")]
    public int visibleScreenMargin = 120;

    [Tooltip("Alt + 滚轮缩放时每次变化的比例。0.08 表示每次大约放大 / 缩小 8%。")]
    [Range(0.01f, 0.3f)]
    public float altWheelScaleStep = 0.08f;

    [Header("Hotkey")]
    [Tooltip("切换桌宠模式 / 全屏模式的快捷键。")]
    public KeyCode toggleFullscreenKey = KeyCode.F11;

    [Header("Debug")]
    [Tooltip("输出窗口模式切换和拖动缩放日志，方便调试。")]
    public bool logWindowMode = true;

    public DesktopPetWindowMode CurrentMode { get; private set; } = DesktopPetWindowMode.Fullscreen;
    public bool IsDraggingWindow => _isDraggingWindow;

    private bool _isInitialized;
    private bool _isDraggingWindow;
    private bool _hasDesktopWindowRect;
    private RectInt _desktopWindowRect;
    private RectInt _dragStartWindowRect;
    private Vector2Int _dragStartCursorScreen;
    private IntPtr _windowHandle = IntPtr.Zero;

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
#endif

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        // 延后一帧再取句柄，让主场景和透明窗口脚本先把窗口稳定下来。
        yield return null;
        TryInitializeWindowController();
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            TryInitializeWindowController();
        }

        if (_isInitialized && Input.GetKeyDown(toggleFullscreenKey))
        {
            ToggleFullscreenMode();
        }
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
    }

    /// <summary>
    /// 开始角色拖窗。
    /// 只在桌宠模式下生效；全屏模式下不允许直接拖动整个屏幕窗口。
    /// </summary>
    public bool BeginCharacterDrag()
    {
        if (!TryInitializeWindowController())
        {
            return false;
        }

        if (CurrentMode != DesktopPetWindowMode.DesktopPet)
        {
            return false;
        }

        if (useNativeWindowDrag)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // 让 Windows 自己接管拖窗流程。
            // 对透明无边框窗口来说，这比我们手动每帧 SetWindowPos 更稳定，
            // 能减少拖动过程中或拖动结束后局部渲染没有刷新的概率。
            ReleaseCapture();
            SendMessage(_windowHandle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            CacheDesktopWindowRect(GetCurrentWindowRect());
#endif

            if (logWindowMode)
            {
                Debug.Log($"[WindowModeController] Native drag window finished. rect={GetCurrentWindowRect()}");
            }

            _isDraggingWindow = false;
            return false;
        }

        _dragStartCursorScreen = GetCursorScreenPosition();
        _dragStartWindowRect = GetCurrentWindowRect();
        _isDraggingWindow = true;

        if (logWindowMode)
        {
            Debug.Log($"[WindowModeController] Begin drag window. rect={_dragStartWindowRect}");
        }

        return true;
    }

    /// <summary>
    /// 更新拖窗。
    /// 每一帧根据鼠标全局屏幕坐标改真实窗口位置，而不是在 Unity 内部平移角色。
    /// </summary>
    public void UpdateCharacterDrag()
    {
        if (!_isDraggingWindow || CurrentMode != DesktopPetWindowMode.DesktopPet)
        {
            return;
        }

        Vector2Int currentCursorScreen = GetCursorScreenPosition();
        Vector2Int delta = currentCursorScreen - _dragStartCursorScreen;
        RectInt targetRect = new RectInt(
            _dragStartWindowRect.x + delta.x,
            _dragStartWindowRect.y + delta.y,
            _dragStartWindowRect.width,
            _dragStartWindowRect.height);

        targetRect = ClampWindowRect(targetRect);
        ApplyWindowRect(targetRect);
        CacheDesktopWindowRect(targetRect);
    }

    /// <summary>
    /// 结束拖窗。
    /// </summary>
    public void EndCharacterDrag()
    {
        _isDraggingWindow = false;
    }

    /// <summary>
    /// Alt + 滚轮缩放桌宠窗口。
    /// 如果当前还在全屏模式，会先退出到桌宠模式，再按滚轮方向继续缩放。
    /// </summary>
    public void ResizeDesktopWindowByScroll(float wheelDelta)
    {
        if (Mathf.Abs(wheelDelta) <= 0.001f)
        {
            return;
        }

        if (!TryInitializeWindowController())
        {
            return;
        }

        if (CurrentMode == DesktopPetWindowMode.Fullscreen)
        {
            EnterDesktopPetMode();
        }

        RectInt currentRect = GetDesktopWindowRectOrFallback();
        float stepSign = Mathf.Sign(wheelDelta);
        float scale = stepSign > 0f ? 1f + altWheelScaleStep : 1f / (1f + altWheelScaleStep);

        int newWidth = Mathf.RoundToInt(currentRect.width * scale);
        int newHeight = Mathf.RoundToInt(currentRect.height * scale);

        GetDesktopWindowSizeLimits(out int minWidth, out int minHeight, out int maxWidth, out int maxHeight);
        newWidth = Mathf.Clamp(newWidth, minWidth, maxWidth);
        newHeight = Mathf.Clamp(newHeight, minHeight, maxHeight);

        // 以窗口底边中心作为锚点，让角色看起来像站在桌面上放大/缩小，而不是往上飘。
        int anchorX = currentRect.x + currentRect.width / 2;
        int anchorBottomY = currentRect.y + currentRect.height;
        RectInt resizedRect = new RectInt(
            anchorX - newWidth / 2,
            anchorBottomY - newHeight,
            newWidth,
            newHeight);

        resizedRect = ClampWindowRect(resizedRect);
        ApplyWindowRect(resizedRect);
        CacheDesktopWindowRect(resizedRect);

        if (logWindowMode)
        {
            Debug.Log($"[WindowModeController] Resize desktop window by wheel. delta={wheelDelta}, rect={resizedRect}");
        }
    }

    /// <summary>
    /// 切换全屏 / 桌宠模式。
    /// </summary>
    public void ToggleFullscreenMode()
    {
        if (CurrentMode == DesktopPetWindowMode.Fullscreen)
        {
            EnterDesktopPetMode();
        }
        else
        {
            EnterFullscreenMode();
        }
    }

    /// <summary>
    /// 进入桌宠模式。
    /// 会恢复上次桌宠窗口尺寸；如果还没有历史记录，就使用默认值居中创建一个新窗口。
    /// </summary>
    public void EnterDesktopPetMode()
    {
        if (!TryInitializeWindowController())
        {
            return;
        }

        RectInt rect = GetDesktopWindowRectOrFallback();
        rect = ClampWindowRect(rect);
        ApplyWindowRect(rect);
        CacheDesktopWindowRect(rect);
        CurrentMode = DesktopPetWindowMode.DesktopPet;

        if (logWindowMode)
        {
            Debug.Log($"[WindowModeController] Enter desktop mode. rect={rect}");
        }
    }

    /// <summary>
    /// 进入全屏模式。
    /// 这里保留“全屏模式”的产品定义，但实现上仍然使用 Win32 直接铺满主屏幕，
    /// 避免调用 Unity Screen API 重新写窗口样式。
    /// </summary>
    public void EnterFullscreenMode()
    {
        if (!TryInitializeWindowController())
        {
            return;
        }

        if (CurrentMode == DesktopPetWindowMode.DesktopPet)
        {
            CacheDesktopWindowRect(GetCurrentWindowRect());
        }

        RectInt fullscreenRect = GetPrimaryDisplayRect();
        ApplyWindowRect(fullscreenRect);
        CurrentMode = DesktopPetWindowMode.Fullscreen;
        _isDraggingWindow = false;

        if (logWindowMode)
        {
            Debug.Log($"[WindowModeController] Enter fullscreen mode. rect={fullscreenRect}");
        }
    }

    private bool TryInitializeWindowController()
    {
        if (_isInitialized)
        {
            return true;
        }

        if (!TryCacheWindowHandle())
        {
            return false;
        }

        RectInt currentRect = GetCurrentWindowRect();
        bool isFullscreenLike = IsRectFullscreenLike(currentRect);

        if (!isFullscreenLike)
        {
            CacheDesktopWindowRect(currentRect);
            CurrentMode = DesktopPetWindowMode.DesktopPet;
        }
        else
        {
            CurrentMode = DesktopPetWindowMode.Fullscreen;
        }

        _isInitialized = true;

        if (applyInitialModeOnStart)
        {
            if (initialMode == DesktopPetWindowMode.DesktopPet)
            {
                EnterDesktopPetMode();
            }
            else
            {
                EnterFullscreenMode();
            }
        }

        return true;
    }

    private bool TryCacheWindowHandle()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_windowHandle != IntPtr.Zero)
        {
            return true;
        }

        _windowHandle = FindWindow(null, Application.productName);
        if (_windowHandle == IntPtr.Zero)
        {
            _windowHandle = GetActiveWindow();
        }

        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        return true;
#else
        _windowHandle = new IntPtr(1);
        return true;
#endif
    }

    private RectInt GetDesktopWindowRectOrFallback()
    {
        if (_hasDesktopWindowRect)
        {
            return _desktopWindowRect;
        }

        RectInt fallback = BuildDefaultDesktopRect();
        CacheDesktopWindowRect(fallback);
        return fallback;
    }

    private RectInt BuildDefaultDesktopRect()
    {
        RectInt screenRect = GetPrimaryDisplayRect();
        GetDesktopWindowSizeLimits(out int minWidth, out int minHeight, out int maxWidth, out int maxHeight);

        int width = Mathf.Clamp(desktopWindowWidth, minWidth, maxWidth);
        int height = Mathf.Clamp(desktopWindowHeight, minHeight, maxHeight);
        int x = Mathf.RoundToInt((screenRect.width - width) * 0.5f);
        int y = Mathf.RoundToInt((screenRect.height - height) * 0.5f);

        return new RectInt(x, y, width, height);
    }

    private void CacheDesktopWindowRect(RectInt rect)
    {
        _desktopWindowRect = rect;
        _hasDesktopWindowRect = true;
    }

    private void GetDesktopWindowSizeLimits(out int minWidth, out int minHeight, out int maxWidth, out int maxHeight)
    {
        RectInt screenRect = GetPrimaryDisplayRect();
        minWidth = Mathf.Max(64, minDesktopWindowWidth);
        minHeight = Mathf.Max(64, minDesktopWindowHeight);
        maxWidth = Mathf.Max(minWidth, Mathf.RoundToInt(screenRect.width * maxDesktopWidthScreenRatio));
        maxHeight = Mathf.Max(minHeight, Mathf.RoundToInt(screenRect.height * maxDesktopHeightScreenRatio));
    }

    private RectInt ClampWindowRect(RectInt rect)
    {
        RectInt screenRect = GetPrimaryDisplayRect();
        int width = Mathf.Clamp(rect.width, 64, screenRect.width);
        int height = Mathf.Clamp(rect.height, 64, screenRect.height);

        int minX;
        int maxX;
        int minY;
        int maxY;

        if (allowPartialOffscreen)
        {
            minX = visibleScreenMargin - width;
            maxX = screenRect.width - visibleScreenMargin;
            minY = visibleScreenMargin - height;
            maxY = screenRect.height - visibleScreenMargin;
        }
        else
        {
            minX = 0;
            maxX = screenRect.width - width;
            minY = 0;
            maxY = screenRect.height - height;
        }

        if (maxX < minX)
        {
            maxX = minX;
        }

        if (maxY < minY)
        {
            maxY = minY;
        }

        int x = Mathf.Clamp(rect.x, minX, maxX);
        int y = Mathf.Clamp(rect.y, minY, maxY);
        return new RectInt(x, y, width, height);
    }

    private RectInt GetCurrentWindowRect()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_windowHandle == IntPtr.Zero || !GetWindowRect(_windowHandle, out RECT rect))
        {
            return BuildDefaultDesktopRect();
        }

        return new RectInt(rect.Left, rect.Top, Mathf.Max(1, rect.Right - rect.Left), Mathf.Max(1, rect.Bottom - rect.Top));
#else
        return new RectInt(0, 0, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
#endif
    }

    private void ApplyWindowRect(RectInt rect)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        IntPtr insertAfter = keepWindowTopMost ? HWND_TOPMOST : HWND_TOP;
        uint flags = SWP_NOACTIVATE | SWP_SHOWWINDOW;
        if (!keepWindowTopMost)
        {
            flags |= SWP_NOZORDER;
        }

        SetWindowPos(_windowHandle, insertAfter, rect.x, rect.y, rect.width, rect.height, flags);
#endif
    }

    private bool IsRectFullscreenLike(RectInt rect)
    {
        RectInt screenRect = GetPrimaryDisplayRect();
        const int tolerance = 4;
        return Mathf.Abs(rect.x) <= tolerance
               && Mathf.Abs(rect.y) <= tolerance
               && Mathf.Abs(rect.width - screenRect.width) <= tolerance
               && Mathf.Abs(rect.height - screenRect.height) <= tolerance;
    }

    private RectInt GetPrimaryDisplayRect()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        int width = Mathf.Max(1, GetSystemMetrics(0));
        int height = Mathf.Max(1, GetSystemMetrics(1));
        return new RectInt(0, 0, width, height);
#else
        return new RectInt(0, 0, Mathf.Max(1, Screen.currentResolution.width), Mathf.Max(1, Screen.currentResolution.height));
#endif
    }

    private Vector2Int GetCursorScreenPosition()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (GetCursorPos(out POINT point))
        {
            return new Vector2Int(point.X, point.Y);
        }
#endif

        Vector3 mouse = Input.mousePosition;
        return new Vector2Int(Mathf.RoundToInt(mouse.x), Mathf.RoundToInt(Screen.height - mouse.y));
    }
}
