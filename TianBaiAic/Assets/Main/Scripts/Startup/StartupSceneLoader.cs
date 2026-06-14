using System;
using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity 启动页加载控制器。
/// 放在 Start.unity 里使用：先把启动窗口恢复为窗口化、居中并显示加载提示，再异步加载 Main 场景；
/// 主场景加载到可激活状态后，再按照配置决定是否切到全屏并正式进入主场景。
/// </summary>
public class StartupSceneLoader : MonoBehaviour
{
    private const int DefaultStartupWidth = 800;
    private const int DefaultStartupHeight = 450;

    [Header("Scene")]
    [Tooltip("加载完成后要进入的主场景名称，需要在 Build Settings 里启用。")]
    public string mainSceneName = "Main";

    [Tooltip("场景加载到 90% 后至少停留多久，避免启动页一闪而过。")]
    public float minimumSplashSeconds = 0.8f;

    [Header("Startup Window")]
    [Tooltip("启动时强制恢复窗口化。Unity 会记住上次运行的全屏状态，这里用于覆盖注册表里的历史值。")]
    public bool forceWindowedOnStart = true;

    [Tooltip("启动时强制恢复启动页尺寸。用于避免上次全屏运行留下 1920x1080 一类的历史分辨率。")]
    public bool forceStartupResolutionOnStart = true;

    [Tooltip("是否由加载脚本强制设置启动页窗口大小。通常关闭，让已有窗口脚本负责无边框/透明/尺寸。")]
    public bool resizeStartupWindow = false;

    [Tooltip("启动页窗口宽度。横屏启动页默认使用 800x450。")]
    public int startupWidth = DefaultStartupWidth;

    [Tooltip("启动页窗口高度。横屏启动页默认使用 800x450。")]
    public int startupHeight = DefaultStartupHeight;

    [Tooltip("启动时把窗口移动到屏幕中央。")]
    public bool centerWindowOnStart = true;

    [Header("Main Window")]
    [Tooltip("激活主场景前是否应用主界面窗口分辨率。仅在 fullscreenBeforeActivateMain 关闭时生效。")]
    public bool applyMainResolutionBeforeActivate = true;

    [Tooltip("主界面窗口宽度。应和 Player Settings 的 Default Screen Width 保持一致。")]
    public int mainWindowWidth = 800;

    [Tooltip("主界面窗口高度。应和 Player Settings 的 Default Screen Height 保持一致。")]
    public int mainWindowHeight = 450;

    [Tooltip("激活主场景前是否强制切回全屏。当前先开启，保证主界面能完整全屏渲染。")]
    public bool fullscreenBeforeActivateMain = true;

    [Tooltip("切全屏后等待一帧再激活主场景，让窗口状态先稳定下来。")]
    public bool waitOneFrameAfterFullscreen = true;

    [Header("UI")]
    [Tooltip("启动页上的 TMP 文本。为空时会自动寻找场景里的第一个 TextMeshProUGUI。")]
    public TextMeshProUGUI statusText;

    [Tooltip("是否在 Console 输出加载过程，方便调试打包后的启动流程。")]
    public bool logStartup = true;

    /// <summary>
    /// Unity 会在脚本运行前读取注册表里的历史窗口状态。
    /// 这里用 BeforeSceneLoad 做第一层兜底，尽量在场景脚本 Awake 之前把启动窗口拉回窗口化。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceWindowedBeforeSceneLoad()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(DefaultStartupWidth, DefaultStartupHeight, false);
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
#endif

    private void Awake()
    {
        ApplyEarlyWindowedMode();
    }

    private IEnumerator Start()
    {
        BindStatusTextIfNeeded();

        SetStatus("正在准备启动窗口...");
        ApplyStartupWindow();
        yield return null;

        float startTime = Time.realtimeSinceStartup;
        SetStatus("正在加载主场景...");

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            SetStatus($"加载失败：没有找到场景 {mainSceneName}");
            Debug.LogError($"[StartupSceneLoader] LoadSceneAsync returned null. scene={mainSceneName}");
            yield break;
        }

        // 先加载到 90%，等窗口尺寸调整完成后再激活主场景，避免用户看到中间闪烁。
        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            SetStatus($"正在加载主场景... {progress:P0}");
            yield return null;
        }

        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minimumSplashSeconds)
        {
            SetStatus("正在整理启动资源...");
            yield return new WaitForSecondsRealtime(minimumSplashSeconds - elapsed);
        }

        SetStatus("加载完成，正在进入主界面...");

        if (fullscreenBeforeActivateMain)
        {
            ApplyMainWindowFullscreen();
            if (waitOneFrameAfterFullscreen)
            {
                yield return null;
            }
        }
        else if (applyMainResolutionBeforeActivate)
        {
            ApplyMainWindowResolution();
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
    }

    private void BindStatusTextIfNeeded()
    {
        if (statusText != null) return;

#if UNITY_2023_1_OR_NEWER
        statusText = FindFirstObjectByType<TextMeshProUGUI>();
#else
        statusText = FindObjectOfType<TextMeshProUGUI>();
#endif
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (logStartup)
        {
            Debug.Log($"[StartupSceneLoader] {message}");
        }
    }

    private void ApplyStartupWindow()
    {
        startupWidth = Mathf.Max(1, startupWidth);
        startupHeight = Mathf.Max(1, startupHeight);

        if (resizeStartupWindow)
        {
            // 正常启动链路里，窗口尺寸/无边框/透明由专门的窗口脚本负责。
            // 这个开关只用于测试或兜底，避免加载器和窗口脚本互相抢状态。
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(startupWidth, startupHeight, false);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!centerWindowOnStart) return;

        IntPtr hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
        {
            Debug.LogWarning("[StartupSceneLoader] 无法获取窗口句柄，启动窗口居中已跳过。");
            return;
        }

        int screenWidth = GetSystemMetrics(0);
        int screenHeight = GetSystemMetrics(1);
        int windowWidth = resizeStartupWindow ? startupWidth : Mathf.Max(1, Screen.width);
        int windowHeight = resizeStartupWindow ? startupHeight : Mathf.Max(1, Screen.height);
        int x = Mathf.Max(0, (screenWidth - windowWidth) / 2);
        int y = Mathf.Max(0, (screenHeight - windowHeight) / 2);

        // 这里只做居中；无边框、透明和实际尺寸由已有窗口脚本处理。
        uint flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW;
        if (!resizeStartupWindow)
        {
            flags |= SWP_NOSIZE;
        }

        SetWindowPos(hwnd, HWND_TOP, x, y, windowWidth, windowHeight, flags);
#endif
    }

    private void ApplyEarlyWindowedMode()
    {
        if (!forceWindowedOnStart)
        {
            return;
        }

        startupWidth = Mathf.Max(1, startupWidth);
        startupHeight = Mathf.Max(1, startupHeight);

        // Unity Standalone 会把上次运行的全屏/分辨率写进注册表。
        // 如果不在启动页最早阶段覆盖，构建包可能无视 Player Settings，直接按历史全屏启动。
        Screen.fullScreenMode = FullScreenMode.Windowed;
        if (forceStartupResolutionOnStart)
        {
            Screen.SetResolution(startupWidth, startupHeight, false);
        }

        if (logStartup)
        {
            Debug.Log($"[StartupSceneLoader] 已强制恢复启动窗口：{Screen.fullScreenMode}, {startupWidth}x{startupHeight}");
        }
    }

    private void ApplyMainWindowFullscreen()
    {
        Resolution resolution = Screen.currentResolution;
        // 当前阶段主界面先使用全屏窗口模式，后续自动窗口方案完成后再替换这里。
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.SetResolution(resolution.width, resolution.height, true);
    }

    private void ApplyMainWindowResolution()
    {
        mainWindowWidth = Mathf.Max(1, mainWindowWidth);
        mainWindowHeight = Mathf.Max(1, mainWindowHeight);

        // 主界面现在走窗口化运行链路，这里只调整分辨率，不切全屏。
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(mainWindowWidth, mainWindowHeight, false);

        if (logStartup)
        {
            Debug.Log($"[StartupSceneLoader] 已应用主界面窗口分辨率：{mainWindowWidth}x{mainWindowHeight}");
        }
    }
}
