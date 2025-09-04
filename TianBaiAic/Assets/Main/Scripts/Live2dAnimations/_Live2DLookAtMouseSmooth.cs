using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.LookAt;
using Live2D.Cubism.Framework.MouthMovement;
using Live2D.Cubism.Framework.Physics;
using UnityEngine;
using UnityEngine.UIElements;

public class Live2DLookAtMouseSmooth : MonoBehaviour, ICubismUpdatable
{
    public CubismModel model;

    /// <summary>
    /// The blend mode.
    /// </summary>
    [SerializeField]
    public CubismParameterBlendMode BlendMode = CubismParameterBlendMode.Multiply;

    public string angleXParam = "ParamAngleX";
    public string angleYParam = "ParamAngleY";
    public string eyeXParam = "ParamEyeBallX";
    public string eyeYParam = "ParamEyeBallY";
    public string bodyXParam = "ParamBodyAngleX";

    public string hairSideParam = "Param4";  // 侧发
    public string hairBackParam = "Param5";  // 后发

    public float eyeIntensity = 1.0f;
    public float headIntensity = 30f;
    public float bodyIntensity = 10f;
    public float hairIntensity = 10f;

    /// <summary>
    /// Mouth parameters.
    /// </summary>
    private CubismParameter[] Destinations { get; set; }


    /// <summary>
    /// Source parameters.
    /// </summary>
    private CubismLookParameter[] Sources { get; set; }


    private Vector2 smoothedLook = Vector2.zero;
    public float smoothSpeed = 5f; // 平滑程度

    Vector2 screen_size;

    // 执行顺序
    public int ExecutionOrder
    {
        get {
            return 750; }
    }
    public bool NeedsUpdateOnEditing
    {
        get { return true; }
    }

    public void Refresh()
    {
        var model = this.FindCubismModel();
        //获取屏幕大小
        screen_size = new Vector2(Screen.width, Screen.height);

        // Fail silently...
        if (model == null)
        {
            return;
        }
    }

    public bool HasUpdateController { get; set; }  // 物理模拟

    public void OnLateUpdate()
    {
        // 1. 获取鼠标位置
        //Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        //Vector3 localMousePos = model.transform.InverseTransformPoint(mouseWorldPos);

        // 2. 计算 [-1, 1] 区间的偏移
        //float lookX = Mathf.Clamp(localMousePos.x * 2f, -1f, 1f);
        //float lookY = Mathf.Clamp(localMousePos.y * 2f, -1f, 1f);
        //通过屏幕大小来计算
        Vector3 mousePos = Input.mousePosition;
        float lookX = Mathf.Clamp((mousePos.x / screen_size.x) * 2f - 1f, -1f, 1f) - transform.position.x;
        float lookY = Mathf.Clamp((mousePos.y / screen_size.y) * 2f - 1f, -1f, 1f) - transform.position.y;

        // 3. 平滑插值移动（让头部有延迟）
        smoothedLook = Vector2.Lerp(smoothedLook, new Vector2(lookX, lookY), Time.deltaTime * smoothSpeed);

        model.GetComponent<CubismPhysicsController>().Stabilization();  // 物理模拟
        // 4. 设置参数（叠加到当前值上）
        AddToParam(angleXParam, smoothedLook.x * headIntensity);
        AddToParam(angleYParam, smoothedLook.y * headIntensity);

        AddToParam(bodyXParam, smoothedLook.x * bodyIntensity);

        AddToParam(eyeXParam, lookX * eyeIntensity);
        AddToParam(eyeYParam, lookY * eyeIntensity);

        AddToParam(hairSideParam, smoothedLook.x * hairIntensity);
        AddToParam(hairBackParam, smoothedLook.y * hairIntensity);

        // If the parameter is linked to physics, update the physics controller
        // var physicsController = model.GetComponent<CubismPhysicsController>();
        // if (physicsController != null)
        // {
        //     physicsController.Stabilization(); // Ensure physics simulation is stable
        // }
    }
    private void AddToParam(string paramName, float delta)
    {
        CubismParameter param = model.Parameters.FindById(paramName);
        if (param != null)
        {
            // Apply the delta value using the blend mode
            param.BlendToValue(BlendMode, delta);

        }
        else
        {
            Debug.LogWarning($"Param {paramName} not found.");
        }
    }
    /*
    private void LateUpdate()
    {
        // 物理模拟
        var physicsController = model.GetComponent<CubismPhysicsController>();
        model.ForceUpdateNow(); // 保证每帧更新
    }*/

    #region Unity Events Handling
    private void Start()
    {
        // Initialize cache.
        Refresh();
    }
    private void LateUpdate()
    {
        if (!HasUpdateController)
        {
            OnLateUpdate();
        }
    }
    #endregion
}
