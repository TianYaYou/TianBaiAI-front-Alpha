using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer))]
public class AudioVisualizer : MonoBehaviour
{
    [Header("组件引用")]
    public AudioSource audioSource;

    [Header("形状调节")]
    public int pointCount = 100;
    public float totalWidth = 20f;
    public float heightScale = 8f;
    public float bottomY = -10f; // 确保这个值足够低，盖住屏幕下方

    [Header("平滑度")]
    [Range(1, 8)] public int smoothIterations = 3;
    public float riseSpeed = 30f;
    public float fallSpeed = 10f;

    private Mesh mesh;
    private LineRenderer lineRenderer;
    private float[] spectrumData;
    private float[] rawHeights;
    private Vector3[] topVertices;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        spectrumData = new float[2048];
        rawHeights = new float[pointCount];
        topVertices = new Vector3[pointCount];

        // LineRenderer 初始设置
        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = false;

        InitMesh();
    }

    void InitMesh()
    {
        Vector3[] vertices = new Vector3[pointCount * 2];
        int[] triangles = new int[(pointCount - 1) * 6];

        float segmentWidth = totalWidth / (pointCount - 1);
        for (int i = 0; i < pointCount; i++)
        {
            float xPos = -totalWidth / 2f + i * segmentWidth;
            vertices[i] = new Vector3(xPos, bottomY, 0); // 底部固定点
            vertices[i + pointCount] = new Vector3(xPos, 0, 0); // 顶部动态点
        }

        int t = 0;
        for (int i = 0; i < pointCount - 1; i++)
        {
            triangles[t++] = i;
            triangles[t++] = i + pointCount;
            triangles[t++] = i + 1;
            triangles[t++] = i + 1;
            triangles[t++] = i + pointCount;
            triangles[t++] = i + pointCount + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    void Update()
    {
        if (!audioSource) return;
        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        // --- 1. 数据采集与平滑 (同之前) ---
        float logMin = Mathf.Log10(50);
        float logMax = Mathf.Log10(10000);
        float sampleRate = AudioSettings.outputSampleRate;

        for (int i = 0; i < pointCount; i++)
        {
            float freq = Mathf.Pow(10, logMin + (float)i / pointCount * (logMax - logMin));
            int idx = Mathf.Clamp(Mathf.FloorToInt(freq * 2048 * 2 / sampleRate), 0, 2047);
            float target = Mathf.InverseLerp(-70, -10, (spectrumData[idx] > 0.00001f ? 20 * Mathf.Log10(spectrumData[idx]) : -80)) * heightScale;
            rawHeights[i] = Mathf.Lerp(rawHeights[i], target, Time.deltaTime * (target > rawHeights[i] ? riseSpeed : fallSpeed));
        }

        // --- 2. 空间模糊 (水波纹) ---
        float[] blur = new float[pointCount];
        System.Array.Copy(rawHeights, blur, pointCount);
        for (int it = 0; it < smoothIterations; it++)
            for (int i = 1; i < pointCount - 1; i++)
                blur[i] = (blur[i - 1] + blur[i] + blur[i + 1]) / 3f;

        // --- 3. 同步 Line 和 Mesh ---
        Vector3[] meshVerts = mesh.vertices;
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 v = new Vector3(-totalWidth / 2f + i * (totalWidth / (pointCount - 1)), blur[i], 0);
            topVertices[i] = v; // 给线条用
            meshVerts[i + pointCount] = v; // 给面片用
        }

        lineRenderer.SetPositions(topVertices);
        mesh.vertices = meshVerts;
        mesh.RecalculateBounds();
    }
}