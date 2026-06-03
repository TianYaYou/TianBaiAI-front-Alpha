using UnityEngine;

public class SpringBodyCreator : MonoBehaviour
{
    [Header("配置参数")]
    public int nodeCount = 20;         // 自定义节点数量
    public float radius = 1.0f;        // 骨架半径
    public float springForce = 150f;   // 增加节点后，弹簧力需适当增大
    public float damper = 8f;

    [ContextMenu("生成均匀物理骨架")]
    public void CreateRig()
    {
        // 1. 创建中心
        GameObject center = new GameObject("LiquidBall_Center");
        Rigidbody centerRb = center.AddComponent<Rigidbody>();
        centerRb.useGravity = false;

        // 2. 斐波那契球面采样分布
        float phi = Mathf.PI * (3f - Mathf.Sqrt(5f)); // 黄金角度

        for (int i = 0; i < nodeCount; i++)
        {
            float y = 1 - (i / (float)(nodeCount - 1)) * 2; // y 从 1 到 -1
            float radiusAtY = Mathf.Sqrt(1 - y * y);        // y 处的圆半径

            float theta = phi * i; // 黄金角度增量

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            Vector3 pos = new Vector3(x, y, z) * radius;

            // 3. 实例化节点
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            jointObj.name = $"Joint_{i}";
            jointObj.transform.position = center.transform.position + pos;
            jointObj.transform.localScale = Vector3.one * (radius * 0.1f);
            jointObj.transform.SetParent(center.transform);

            // 物理配置
            Rigidbody jointRb = jointObj.AddComponent<Rigidbody>();
            jointRb.useGravity = false;
            jointRb.linearDamping = 2f; // 增加阻尼让它更像粘性液体

            SpringJoint sj = jointObj.AddComponent<SpringJoint>();
            sj.connectedBody = centerRb;
            sj.spring = springForce;
            sj.damper = damper;
            sj.autoConfigureConnectedAnchor = false;
            sj.anchor = Vector3.zero;
            sj.connectedAnchor = pos; // 平衡点设在生成的球面位置

            // 隐藏节点显示
            // jointObj.GetComponent<MeshRenderer>().enabled = false;
        }
    }
}