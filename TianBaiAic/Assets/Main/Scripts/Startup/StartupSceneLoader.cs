using System;
using System.Collections;
using System.Collections.Generic;
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
    public float minimumSplashSeconds = 1f;

    [Tooltip("正式开始加载主场景前额外等待多久，让启动页先稳定显示一会儿。")]
    public float delayBeforeLoadSeconds = 0.8f;

    [Tooltip("用 Additive 加载主场景，先让 Main 上屏，再延迟卸载启动场景，减少切换瞬间的 GC/卸载压力。")]
    public bool loadMainSceneAdditively = true;

    [Tooltip("Additive 模式下，主场景激活后是否延迟卸载启动场景。启动场景很小，也可以临时关闭用于排查。")]
    public bool unloadStartupSceneAfterMainActivated = true;

    [Tooltip("主场景激活后延迟多久卸载启动场景，把卸载和 GC 从切换瞬间挪开。")]
    public float unloadStartupSceneDelaySeconds = 5f;

    [Header("Readiness")]
    [Tooltip("是否等待启动期模块完成准备。实现 IStartupReadiness 的组件会被统一等待。")]
    public bool waitForStartupReadiness = true;

    [Tooltip("等待启动期模块的最长秒数。小于等于 0 表示一直等待。")]
    public float readinessTimeoutSeconds = 30f;

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

    [Tooltip("退出程序时把 Unity 记住的窗口状态恢复为启动页尺寸，避免下次启动直接全屏。")]
    public bool resetWindowPreferencesOnQuit = true;

    [Header("UI")]
    [Tooltip("启动页上的 TMP 文本。为空时会自动寻找场景里的第一个 TextMeshProUGUI。")]
    public TextMeshProUGUI statusText;

    [Tooltip("全屏切换前要隐藏的启动页视觉根节点。为空时会自动寻找 Start 场景里的第一个 Canvas。")]
    public GameObject startupVisualRoot;

    [Tooltip("未手动指定 startupVisualRoot 时，是否自动绑定场景里的第一个 Canvas。")]
    public bool autoBindStartupVisualRoot = true;

    [Tooltip("切全屏前先隐藏启动页，避免窗口从 800x450 放大到全屏时把启动页也一起放大一帧。")]
    public bool hideStartupVisualsBeforeFullscreen = true;

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

    private Scene _startupScene;

    private void Awake()
    {
        // 进入 Main 场景后这个加载器仍需存活到退出，用于恢复 Unity 记住的窗口状态。
        _startupScene = gameObject.scene;
        DontDestroyOnLoad(gameObject);
        ApplyEarlyWindowedMode();
    }

    private IEnumerator Start()
    {
        BindStatusTextIfNeeded();
        BindStartupVisualRootIfNeeded();

        SetStatus("正在准备启动窗口...");
        ApplyStartupWindow();
        yield return null;

        if (delayBeforeLoadSeconds > 0f)
        {
            SetStatus("正在准备启动资源...");
            yield return new WaitForSecondsRealtime(delayBeforeLoadSeconds);
        }

        float startTime = Time.realtimeSinceStartup;
        SetStatus("正在加载主场景...");
        DisableStartupEventSystemsBeforeMainLoad();

        LoadSceneMode loadMode = loadMainSceneAdditively ? LoadSceneMode.Additive : LoadSceneMode.Single;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(mainSceneName, loadMode);
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

        if (waitForStartupReadiness)
        {
            yield return WaitForStartupReadiness();
        }

        SetStatus("加载完成，正在进入主界面...");

        if (fullscreenBeforeActivateMain)
        {
            HideStartupVisualsBeforeWindowChange();
            yield return null;
            ApplyMainWindowFullscreen();
            if (waitOneFrameAfterFullscreen)
            {
                yield return null;
            }
        }
        else if (applyMainResolutionBeforeActivate)
        {
            HideStartupVisualsBeforeWindowChange();
            yield return null;
            ApplyMainWindowResolution();
            yield return null;
        }

        DisableStartupSceneRuntimeObjectsBeforeMainActivation();
        loadOperation.allowSceneActivation = true;
        if (loadMainSceneAdditively)
        {
            yield return FinishAdditiveMainSceneActivation(loadOperation);
        }
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

    private void BindStartupVisualRootIfNeeded()
    {
        if (startupVisualRoot != null || !autoBindStartupVisualRoot)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
#else
        Canvas canvas = FindObjectOfType<Canvas>(true);
#endif
        if (canvas != null && canvas.gameObject != gameObject)
        {
            startupVisualRoot = canvas.gameObject;
        }
    }

    private IEnumerator WaitForStartupReadiness()
    {
        List<IStartupReadiness> readinessItems = CollectStartupReadinessItems();
        if (readinessItems.Count == 0)
        {
            SetStatus("启动模块准备完成。");
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;
        while (true)
        {
            IStartupReadiness waitingItem = FindFirstWaitingReadiness(readinessItems);
            if (waitingItem == null)
            {
                SetStatus("启动模块准备完成。");
                yield break;
            }

            string waitingMessage = string.IsNullOrWhiteSpace(waitingItem.StartupReadinessMessage)
                ? "正在等待启动模块准备完成..."
                : waitingItem.StartupReadinessMessage;
            SetStatus(waitingMessage);

            if (readinessTimeoutSeconds > 0f
                && Time.realtimeSinceStartup - startTime > readinessTimeoutSeconds)
            {
                Debug.LogWarning($"[StartupSceneLoader] 等待启动模块超时，启动流程将继续。最后等待项：{waitingMessage}");
                yield break;
            }

            yield return null;
        }
    }

    private List<IStartupReadiness> CollectStartupReadinessItems()
    {
        var readinessItems = new List<IStartupReadiness>();

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
#endif
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            if (behaviour is IStartupReadiness readiness)
            {
                readinessItems.Add(readiness);
            }
        }

        return readinessItems;
    }

    private static IStartupReadiness FindFirstWaitingReadiness(List<IStartupReadiness> readinessItems)
    {
        foreach (IStartupReadiness readiness in readinessItems)
        {
            if (readiness != null && !readiness.IsStartupReady)
            {
                return readiness;
            }
        }

        return null;
    }

    private void HideStartupVisualsBeforeWindowChange()
    {
        if (!hideStartupVisualsBeforeFullscreen || startupVisualRoot == null)
        {
            return;
        }

        // 先隐藏启动页 UI，再等待一帧切换窗口尺寸/全屏，避免启动页在切换瞬间被放大。
        startupVisualRoot.SetActive(false);
        if (logStartup)
        {
            Debug.Log($"[StartupSceneLoader] 已隐藏启动页视觉根节点：{startupVisualRoot.name}");
        }
    }

    private void DisableStartupEventSystemsBeforeMainLoad()
    {
        if (!_startupScene.IsValid() || !_startupScene.isLoaded)
        {
            return;
        }

        // 启动页没有交互需求时，提前关闭 EventSystem，避免 Additive 加载 Main 后出现双 EventSystem。
        foreach (GameObject root in _startupScene.GetRootGameObjects())
        {
            DisableComponentsInChildren<UnityEngine.EventSystems.EventSystem>(root);
        }
    }

    private void DisableStartupSceneRuntimeObjectsBeforeMainActivation()
    {
        if (!_startupScene.IsValid() || !_startupScene.isLoaded)
        {
            return;
        }

        // Additive 加载时 Start 和 Main 会短暂共存。进入 Main 前先关闭启动场景的输入、声音和相机，
        // 避免出现两个 EventSystem/AudioListener，也避免 Main 场景脚本缓存到即将卸载的启动相机。
        foreach (GameObject root in _startupScene.GetRootGameObjects())
        {
            DisableComponentsInChildren<UnityEngine.EventSystems.EventSystem>(root);
            DisableComponentsInChildren<AudioListener>(root);
            DisableComponentsInChildren<Camera>(root);
        }
    }

    private static void DisableComponentsInChildren<T>(GameObject root) where T : Behaviour
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component != null)
            {
                component.enabled = false;
            }
        }
    }

    private IEnumerator FinishAdditiveMainSceneActivation(AsyncOperation loadOperation)
    {
        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene mainScene = SceneManager.GetSceneByName(mainSceneName);
        if (mainScene.IsValid() && mainScene.isLoaded)
        {
            SceneManager.SetActiveScene(mainScene);
        }

        if (unloadStartupSceneAfterMainActivated)
        {
            StartCoroutine(UnloadStartupSceneLater());
        }
    }

    private IEnumerator UnloadStartupSceneLater()
    {
        if (unloadStartupSceneDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(unloadStartupSceneDelaySeconds);
        }

        if (!_startupScene.IsValid() || !_startupScene.isLoaded)
        {
            yield break;
        }

        // Start 场景只保留加载页 UI。延迟卸载它可以避免切换 Main 的瞬间同时触发卸载和 GC。
        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(_startupScene);
        while (unloadOperation != null && !unloadOperation.isDone)
        {
            yield return null;
        }
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
        // 这里不再使用 FullScreenWindow。Unity 会把原生全屏状态写入注册表，
        // 导致下一次进程创建窗口时直接全屏，启动页脚本来不及阻止。
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(resolution.width, resolution.height, false);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd = GetActiveWindow();
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HWND_TOP, 0, 0, resolution.width, resolution.height, SWP_SHOWWINDOW);
        }
#endif
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

    private void OnApplicationQuit()
    {
        if (!resetWindowPreferencesOnQuit)
        {
            return;
        }

        // Unity Standalone 会记住最后一次窗口状态。
        // 退出时恢复启动页尺寸，让下一次启动仍然从横屏启动页开始，而不是直接全屏。
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(DefaultStartupWidth, DefaultStartupHeight, false);
    }
}
