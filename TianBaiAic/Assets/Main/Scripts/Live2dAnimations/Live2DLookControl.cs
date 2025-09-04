using System.Collections;
using System.Collections.Generic;
using Live2D.Cubism.Samples.OriginalWorkflow.Demo;
using UnityEngine;

public class Live2DLookControl : MonoBehaviour
{
    public static Live2DLookControl instance;

    public GameObject Live2DCubismLookEyePosition;



    public float MouseActive = 0f;
    public float Live2DCubismLookEyeActive = 100f;

    Vector3 LastMouseDistance = Vector3.zero;
    public float distance = 0f;

    Vector3 randomTargetPos = Vector3.zero;
    float randomLookTimer = 0f;
    float randomLookInterval = 2f; // 随机间隔时间

    public static void SetLookEyeActive(float value)
    {
        instance.Live2DCubismLookEyeActive = value;
    }

    void Start()
    {
        randomTargetPos = Live2DCubismLookEyePosition.transform.position;
        randomLookTimer = randomLookInterval;
        instance = this;
    }

    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        distance = (mousePos - Camera.main.WorldToScreenPoint(Live2DCubismLookEyePosition.transform.position)).magnitude;
        MouseActive = Mathf.Abs(mousePos.magnitude - LastMouseDistance.magnitude);
        LastMouseDistance = mousePos;

        if (Live2DCubismLookEyeActive > 0) Live2DCubismLookEyeActive += MouseActive / 10 * Time.deltaTime;
        Live2DCubismLookEyeActive = Mathf.Lerp(Live2DCubismLookEyeActive, 0, Time.deltaTime);

        if (Live2DCubismLookEyeActive < 0.1f)
        {
            Live2DCubismLookEyeActive = 0;
        }
        if (Live2DCubismLookEyeActive <= 0 && MouseActive > 3 && distance < 200)
        {
            Live2DCubismLookEyeActive = 100;
        }
        var lookTarget = GetComponent<CubismLookTarget>();
        if (Live2DCubismLookEyeActive <= 0  )
        {
            // 随机计时
            randomLookTimer -= Time.deltaTime;
            if (lookTarget.LookAtMouse)
            {
                transform.position = Camera.main.ScreenToViewportPoint(mousePos) * 2 - Vector3.one;
                randomLookTimer = Random.Range(1, 2);
                lookTarget.LookAtMouse = false;
            }
            if (randomLookTimer <= 0f )
            {
                // 生成新的随机目标点（屏幕空间，避免超出屏幕）
                float randX = Random.Range(Screen.width * 0.2f, Screen.width * 0.8f);
                float randY = Random.Range(Screen.height * 0.2f, Screen.height * 0.8f);
                Vector3 screenTarget = new Vector3(randX, randY, Camera.main.WorldToScreenPoint(transform.position).z);
                randomTargetPos = Camera.main.ScreenToWorldPoint(screenTarget);
                randomLookTimer = Random.Range(1.5f, 10.5f); // 下次间隔
            }
            
            // 平滑移动到目标点
            transform.position = Vector3.Lerp(transform.position, randomTargetPos, Time.deltaTime * 1.5f);
        }
        else
        {
            GetComponent<CubismLookTarget>().LookAtMouse = true;
        }
    }
}
