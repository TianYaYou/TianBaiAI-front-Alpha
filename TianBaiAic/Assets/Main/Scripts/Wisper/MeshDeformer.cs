using UnityEngine;
using System.Collections.Generic; // 引入 List

public class MeshDeformerOptimized : MonoBehaviour
{
    [Header("Settings")]
    public float deformIntensity = 1.0f;
    public float springForce = 20.0f; // 弹力：让顶点恢复原位的力量
    public float damping = 5.0f;      // 阻尼：防止变形震荡不停
    public float maxGravityDist = 0.5f; // 只有这么近的引力点才生效（优化性能）

    Mesh deformingMesh;
    Vector3[] originalVertices, displacedVertices;
    Vector3[] vertexVelocities; // 记录每个顶点的速度，用于物理模拟

    // 使用结构体记录引力点和它的创建时间/强度
    struct GravityPoint
    {
        public Vector3 position;
        public float timeRemaining;
    }
    List<GravityPoint> gravityPoints = new List<GravityPoint>();

    Vector3 lastPos;

    void Start()
    {
        // 建议使用 sharedMesh 还是 mesh 取决于是否所有物体共用变形
        // 这里使用 mesh 创建实例，互不干扰
        deformingMesh = GetComponent<MeshFilter>().mesh;
        originalVertices = deformingMesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        vertexVelocities = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++)
        {
            displacedVertices[i] = originalVertices[i];
        }
        lastPos = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. 添加新的引力点 (使用 List 避免 GC)
        Vector3 velocity = (transform.position - lastPos) / dt;
        if (velocity.magnitude > 0.1f)
        {
            // 在物体后方一点生成引力点
            Vector3 pointPos = transform.InverseTransformPoint(lastPos);
            gravityPoints.Add(new GravityPoint { position = pointPos, timeRemaining = 0.5f }); // 存活 0.5 秒
        }

        // 2. 清理过期的引力点 (防止无限膨胀)
        for (int i = gravityPoints.Count - 1; i >= 0; i--)
        {
            var gp = gravityPoints[i];
            gp.timeRemaining -= dt;
            if (gp.timeRemaining <= 0)
            {
                gravityPoints.RemoveAt(i);
            }
            else
            {
                gravityPoints[i] = gp; // 更新时间
            }
        }

        // 3. 更新顶点
        UpdateVertices(dt);

        lastPos = transform.position;
    }

    void UpdateVertices(float dt)
    {
        for (int i = 0; i < displacedVertices.Length; i++)
        {
            Vector3 currentPos = displacedVertices[i];
            Vector3 originalPos = originalVertices[i];
            Vector3 velocity = vertexVelocities[i];

            // A. 计算引力点的拉力
            Vector3 force = Vector3.zero;
            for (int j = 0; j < gravityPoints.Count; j++)
            {
                Vector3 pointToVertex = gravityPoints[j].position - currentPos;
                float distSqr = pointToVertex.sqrMagnitude; // 使用平方距离优化开方运算

                // 优化：太远的点忽略
                if (distSqr > maxGravityDist * maxGravityDist) continue;

                // 防止除以0，加一个极小值 0.001f
                float strength = 1.0f / (distSqr + 0.001f);

                // 限制最大力，防止顶点飞出
                strength = Mathf.Min(strength, 100.0f);

                force -= pointToVertex.normalized * strength * deformIntensity;
            }

            // B. 计算弹簧恢复力 (Hooke's Law): F = -k * x
            Vector3 displacement = currentPos - originalPos;
            force -= displacement * springForce;

            // C. 简单的物理积分 (Euler Integration)
            velocity += force * dt;
            velocity -= velocity * damping * dt; // 阻尼，让速度慢下来
            vertexVelocities[i] = velocity;
            displacedVertices[i] += velocity * dt;
        }

        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals(); // 注意：这也比较耗费性能，如果顶点很多，建议降低频率
    }
}