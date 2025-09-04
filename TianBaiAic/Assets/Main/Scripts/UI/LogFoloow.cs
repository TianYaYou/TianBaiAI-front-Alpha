using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogFoloow : MonoBehaviour
{
    public GameObject cubsim;
    public GameObject image;

    public Vector2 vector = new Vector2(0.15f, 0.3f);
    Camera main_camera;

    // Start is called before the first frame update
    void Start()
    {
        main_camera = Camera.main;
    }

    bool mirror = false;
    // Update is called once per frame
    void Update()
    {
        Vector3 cubsimPos = cubsim.transform.position;
        cubsimPos.y += vector.y * cubsim.transform.localScale.y;
        if (mirror) cubsimPos.x += vector.x * cubsim.transform.localScale.x;
        else cubsimPos.x -= vector.x * cubsim.transform.localScale.x;
        //获取cubsimPos在画布上的位置
        Vector3 screenPos = main_camera.WorldToScreenPoint(cubsimPos);
        //设置范围防止溢出屏幕
        screenPos.x = Mathf.Clamp(screenPos.x, 150, Screen.width - 150);
        screenPos.y = Mathf.Clamp(screenPos.y, 150, Screen.height - 150);
        //设置z轴为0
        screenPos.z = 0;

        //在屏幕右侧镜像
        if (cubsim.transform.position.x < Camera.main.transform.position.x)
        {
            if (!mirror)
            {
                mirror = true;
                image.GetComponent<RectTransform>().localScale = new Vector3(-1, 1, 1);
            }
        }
        else
        {
            if (mirror)
            {
                mirror = false;
                image.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 1);

            }
        }

        //移动到画布上
        GetComponent<RectTransform>().position = screenPos;
    }
}
