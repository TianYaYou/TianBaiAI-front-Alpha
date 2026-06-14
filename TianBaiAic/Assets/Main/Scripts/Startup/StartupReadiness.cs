using UnityEngine;

/// <summary>
/// 启动期等待接口。
/// 需要在进入主场景前完成初始化的模块，可以实现这个接口，让 StartupSceneLoader 统一等待。
/// </summary>
public interface IStartupReadiness
{
    /// <summary>
    /// 当前模块是否已经准备完成。
    /// 返回 true 后，启动流程才会继续进入全屏和主场景切换阶段。
    /// </summary>
    bool IsStartupReady { get; }

    /// <summary>
    /// 等待这个模块时显示在启动页上的提示文字。
    /// 为空时 StartupSceneLoader 会使用通用提示。
    /// </summary>
    string StartupReadinessMessage { get; }
}

/// <summary>
/// 启动期等待组件基类。
/// 后续模型加载、配置加载、TTS 预热等模块可以继承它，只需要在完成时调用 MarkReady()。
/// </summary>
public class StartupReadiness : MonoBehaviour, IStartupReadiness
{
    [SerializeField]
    [Tooltip("等待这个模块时显示的提示。")]
    private string waitingMessage = "正在准备启动模块...";

    [SerializeField]
    [Tooltip("是否在 Awake 时自动标记为完成。测试占位模块可以打开，真实加载模块通常关闭。")]
    private bool readyOnAwake = true;

    private bool isReady;

    /// <inheritdoc />
    public bool IsStartupReady => isReady;

    /// <inheritdoc />
    public string StartupReadinessMessage => waitingMessage;

    private void Awake()
    {
        if (readyOnAwake)
        {
            MarkReady();
        }
    }

    /// <summary>
    /// 标记当前启动模块准备完成。
    /// 继承类完成异步加载、模型初始化或资源预热后调用它即可。
    /// </summary>
    protected void MarkReady()
    {
        isReady = true;
    }

    /// <summary>
    /// 重新置为未完成状态。
    /// 如果后续模块需要重新加载启动资源，可以用它重新进入等待。
    /// </summary>
    protected void MarkNotReady()
    {
        isReady = false;
    }
}
