using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 角色窗口交互控制器。
/// 旧版本负责“在窗口内部平移 / 缩放角色”；
/// 新版本改成“通过角色交互来移动 / 缩放真实窗口”：
/// 1. 鼠标按住角色并拖动 -> 拖动整个桌宠窗口；
/// 2. 选中角色后按住 Alt 滚轮 -> 调整真实窗口大小；
/// 3. 保留全屏模式，缩放时如果当前是全屏，会先退出到桌宠模式再缩放。
/// </summary>
public class Live2DMoveSet : MonoBehaviour
{
    [Header("Hit Test")]
    [Tooltip("角色拖窗命中区。为空时自动使用当前物体上的 Collider2D。")]
    public Collider2D dragHitCollider;

    [Tooltip("如果点击到了子节点上的 2D 碰撞体，也视为点中了当前角色。")]
    public bool includeChildColliders = true;

    [Header("Selection")]
    [Tooltip("点击角色但没有触发拖动时，将角色标记为已选中。")]
    public bool selectCharacterOnClick = true;

    [Tooltip("点击角色外部空白区域时取消选中。")]
    public bool deselectWhenClickOutside = true;

    [Header("Window Drag")]
    [Tooltip("鼠标按下后，移动超过这个像素距离才认为是真正开始拖窗。")]
    public float dragStartThresholdPixels = 8f;

    [Header("Window Resize")]
    [Tooltip("角色被选中后，是否允许 Alt + 滚轮缩放桌宠窗口。")]
    public bool allowAltWheelResize = true;

    [Tooltip("只有鼠标悬停在角色上时，Alt + 滚轮才生效。关闭后，只要角色处于选中状态就可以缩放。")]
    public bool requireHoverForAltWheelResize = false;

    [Header("Debug")]
    [Tooltip("输出角色选中、拖窗、缩窗日志，方便调试交互流程。")]
    public bool logInteraction = false;

    public bool IsCharacterSelected => _isCharacterSelected;

    private WindowModeController _windowModeController;
    private bool _isCharacterSelected;
    private bool _isPointerDownOnCharacter;
    private bool _isDraggingWindow;
    private Vector2 _pointerDownMousePosition;

    private void Start()
    {
        if (dragHitCollider == null)
        {
            dragHitCollider = GetComponent<Collider2D>();
        }

        _windowModeController = WindowModeController.Instance;
    }

    private void Update()
    {
        if (_windowModeController == null)
        {
            _windowModeController = WindowModeController.Instance;
            if (_windowModeController == null)
            {
                return;
            }
        }

        HandleSelectionAndDrag();
        HandleAltWheelResize();
    }

    /// <summary>
    /// 处理点击选中和拖窗。
    /// 拖窗只发生在桌宠模式里；全屏模式下点击角色仍然可以选中，但不会拖动窗口。
    /// </summary>
    private void HandleSelectionAndDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi())
            {
                _isPointerDownOnCharacter = false;
            }
            else if (IsPointerOnCharacter())
            {
                _isPointerDownOnCharacter = true;
                _pointerDownMousePosition = Input.mousePosition;
            }
            else
            {
                _isPointerDownOnCharacter = false;

                if (deselectWhenClickOutside)
                {
                    SetCharacterSelected(false);
                }
            }
        }

        if (_isPointerDownOnCharacter && !_isDraggingWindow && Input.GetMouseButton(0))
        {
            float dragDistance = Vector2.Distance(_pointerDownMousePosition, (Vector2)Input.mousePosition);
            if (dragDistance >= dragStartThresholdPixels)
            {
                _isDraggingWindow = _windowModeController.BeginCharacterDrag();
                if (_isDraggingWindow && logInteraction)
                {
                    Debug.Log("[Live2DMoveSet] Character drag started.");
                }
            }
        }

        if (_isDraggingWindow && Input.GetMouseButton(0))
        {
            _windowModeController.UpdateCharacterDrag();
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool clickedCharacterWithoutDrag = _isPointerDownOnCharacter && !_isDraggingWindow;
            if (_isDraggingWindow)
            {
                _windowModeController.EndCharacterDrag();
                if (logInteraction)
                {
                    Debug.Log("[Live2DMoveSet] Character drag ended.");
                }
            }
            else if (clickedCharacterWithoutDrag && selectCharacterOnClick)
            {
                SetCharacterSelected(true);
            }

            _isDraggingWindow = false;
            _isPointerDownOnCharacter = false;
        }
    }

    /// <summary>
    /// 处理 Alt + 滚轮缩放。
    /// 缩放的是原生窗口，而不是模型自身 localScale，这样才能真正降低长期渲染面积。
    /// </summary>
    private void HandleAltWheelResize()
    {
        bool isPointerOnCharacter = IsPointerOnCharacter();
        bool canResizeWindow = _isCharacterSelected || isPointerOnCharacter;
        if (!allowAltWheelResize || !canResizeWindow)
        {
            return;
        }

        if (!Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
        {
            return;
        }

        if (IsPointerOverUi())
        {
            return;
        }

        if (requireHoverForAltWheelResize && !isPointerOnCharacter)
        {
            return;
        }

        // 旧逻辑使用 Mouse ScrollWheel 轴；这里保留它作为主输入，
        // 并用 mouseScrollDelta 做兜底，避免不同输入设置下滚轮读不到。
        float wheelDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheelDelta) <= 0.001f)
        {
            wheelDelta = Input.mouseScrollDelta.y;
        }

        if (Mathf.Abs(wheelDelta) <= 0.001f)
        {
            return;
        }

        _windowModeController.ResizeDesktopWindowByScroll(wheelDelta);
        if (logInteraction)
        {
            Debug.Log($"[Live2DMoveSet] Resize desktop window by Alt+Wheel. delta={wheelDelta}");
        }
    }

    /// <summary>
    /// 检查鼠标当前是否点在角色上。
    /// 这里优先使用显式拖动碰撞体；如果没有指定，则回退到 Physics2D 点查询。
    /// </summary>
    private bool IsPointerOnCharacter()
    {
        Vector2 worldPoint = Camera.main != null
            ? Camera.main.ScreenToWorldPoint(Input.mousePosition)
            : Vector2.zero;

        if (dragHitCollider != null)
        {
            if (dragHitCollider.OverlapPoint(worldPoint))
            {
                return true;
            }
        }

        Collider2D hitCollider = Physics2D.OverlapPoint(worldPoint);
        if (hitCollider == null)
        {
            return false;
        }

        if (hitCollider.transform == transform)
        {
            return true;
        }

        return includeChildColliders && hitCollider.transform.IsChildOf(transform);
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SetCharacterSelected(bool selected)
    {
        if (_isCharacterSelected == selected)
        {
            return;
        }

        _isCharacterSelected = selected;
        if (logInteraction)
        {
            Debug.Log($"[Live2DMoveSet] Character selected = {_isCharacterSelected}");
        }
    }
}
