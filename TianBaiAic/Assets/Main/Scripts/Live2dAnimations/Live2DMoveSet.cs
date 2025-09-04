using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Live2DMoveSet : MonoBehaviour
{
    Vector3 targetPos; // 目标位置
    Vector3 targetScale; // 目标缩放

    bool isMoving = false; // 是否正在移动

    // 开始时执行调用
    void Start()
    {
        targetPos = transform.position; // 初始化目标位置
        targetScale = transform.localScale; // 初始化目标缩放

    }

    // 每一帧执行调用
    void Update()
    {
        if (isMoving)
        {
            //获取鼠标增加值(像素)
            float deltaX = Input.GetAxis("Mouse X") * 0.03f;
            float deltaY = Input.GetAxis("Mouse Y") * 0.03f;
            targetPos += new Vector3(deltaX, deltaY, 0); // 更新目标位置
            //滚轮缩放
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                targetScale += new Vector3(scroll, scroll, 0); // 更新目标缩放
            }
            
            if(Input.GetMouseButtonUp(0))  //当鼠标抬起时
            {
                isMoving = false; // 停止移动
            }
        }
        else
        {
            //当鼠标点击自己碰撞箱时
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.transform == transform) // 如果点击的是自己
                    {
                        isMoving = true; // 开始移动
                    }
                }
            }
        }
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            //滚轮缩放
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                targetScale -= new Vector3(scroll, scroll, 0); // 更新目标缩放
            }
            //限制缩放范围
            targetScale.x = Mathf.Clamp(targetScale.x, 0.2f, 3);
            targetScale.y = Mathf.Clamp(targetScale.y, 0.2f, 3);
        }


        //平滑移动
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10); // 平滑移动到目标位置
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 10); // 平滑缩放到目标缩放
    }


}
