using System.Collections;
using System.Collections.Generic;
using NAudio.Wave;
using Unity.VisualScripting;
using UnityEngine;

public class Live2DAniActionControl : MonoBehaviour
{
    public Live2DLookControl lookControl;
    public Animator animator;

    //触发随机动画概率
    public float randomAniProb = 0.1f;

    // Update is called once per frame
    void FixedUpdate()
    {
        if(animator.GetInteger("Action") != 0) animator.SetInteger("Action", 0); 

        //如果鼠标在不屏幕上
        if (lookControl.Live2DCubismLookEyeActive < 1)
        {
            //增加随机动画概率
            randomAniProb += Time.deltaTime * 0.02f;
        }
        //如果鼠标在屏幕上
        else
        {
            //减少随机动画概率
            randomAniProb -= Time.deltaTime * 0.1f;
        }
        if (randomAniProb < 0) return;
        //限制随机动画概率在0-1之间
        randomAniProb = Mathf.Clamp01(randomAniProb);
        //每两秒尝试触发一次随机动画
        if (Random.Range(0f, 1f) < randomAniProb * Time.deltaTime)
        {
            //触发随机动画
            animator.SetInteger("Action", Random.Range(1, 3));  //播放在1-2之间的动画
            randomAniProb = -1f;  //重置随机动画概率
        }
    }
}
