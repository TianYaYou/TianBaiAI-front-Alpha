using UnityEngine;

public class Live2DAniActionControl : MonoBehaviour
{
    public static Live2DAniActionControl Instance { get; private set; }

    public Live2DLookControl lookControl;
    public Animator animator;

    // 触发随机动画概率。
    public float randomAniProb = 0.1f;

    private int _actionHoldFrames;

    void Awake()
    {
        Instance = this;
        if (animator == null) animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        // 外部触发的动作至少保留几个 FixedUpdate，避免刚设置就被本脚本清零。
        if (animator != null && animator.GetInteger("Action") != 0)
        {
            if (_actionHoldFrames > 0)
            {
                _actionHoldFrames--;
            }
            else
            {
                animator.SetInteger("Action", 0);
            }
        }

        if (lookControl == null || animator == null) return;

        // 如果鼠标不在屏幕上，逐渐增加随机动作概率。
        if (lookControl.Live2DCubismLookEyeActive < 1)
        {
            randomAniProb += Time.deltaTime * 0.02f;
        }
        else
        {
            randomAniProb -= Time.deltaTime * 0.1f;
        }

        if (randomAniProb < 0) return;
        randomAniProb = Mathf.Clamp01(randomAniProb);

        // 每帧按概率尝试触发一个轻量随机动作。
        if (Random.Range(0f, 1f) < randomAniProb * Time.deltaTime)
        {
            PlayAction(Random.Range(1, 3));
            randomAniProb = -1f;
        }
    }

    public void PlayAction(int actionId, int holdFrames = 2)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return;

        animator.SetInteger("Action", actionId);
        _actionHoldFrames = Mathf.Max(1, holdFrames);
        randomAniProb = -1f;
    }
}
