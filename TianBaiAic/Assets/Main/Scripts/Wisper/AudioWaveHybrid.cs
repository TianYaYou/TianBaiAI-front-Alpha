using NAudio.Wave; // 必须确保 Plugins 文件夹下有 NAudio.dll
using System;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer))]
public class AudioWaveHybrid : MonoBehaviour
{
    [Header("系统音频设置")]
    public int fftSize = 1024; // 必须是2的幂 (512, 1024, 2048)

    [Header("形状调节")]
    public int pointCount = 100;
    public float totalWidth = 20f;
    public float heightScale = 50f; // 系统声音通常较小，需要调高比例
    public float bottomY = -10f;

    [Header("平滑度")]
    [Range(1, 8)] public int smoothIterations = 3;
    public float riseSpeed = 30f;
    public float fallSpeed = 10f;

    private Mesh mesh;
    private LineRenderer lineRenderer;
    private float[] rawHeights;
    private Vector3[] topVertices;

    // NAudio 相关变量
    private WasapiLoopbackCapture capture;
    private float[] spectrumData; // 存储处理后的频谱数据



    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        spectrumData = new float[fftSize];
        rawHeights = new float[pointCount];
        topVertices = new Vector3[pointCount];

        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = false;

        InitMesh();
        StartNAudioCapture();
    }

    void StartNAudioCapture()
    {
        // 获取当前系统的默认播放设备格式
        var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console);
        capture = new WasapiLoopbackCapture(device);

        // 订阅音频数据到达事件
        capture.DataAvailable += (s, e) =>
        {
            float maxPos = 0;
            byte[] buffer = e.Buffer;
            int bytesRecorded = e.BytesRecorded;

            // 将字节数组转换为浮点样本并进行简单的幅度提取
            for (int i = 0; i < bytesRecorded; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                int dataIdx = (i / 4) % fftSize;
                spectrumData[dataIdx] = Mathf.Abs(sample);
            }
        };

        capture.StartRecording();
    }

    void InitMesh()
    {
        Vector3[] vertices = new Vector3[pointCount * 2];
        int[] triangles = new int[(pointCount - 1) * 6];

        float segmentWidth = totalWidth / (pointCount - 1);
        for (int i = 0; i < pointCount; i++)
        {
            float xPos = -totalWidth / 2f + i * segmentWidth;
            vertices[i] = new Vector3(xPos, bottomY, 0); // 底部固定
            vertices[i + pointCount] = new Vector3(xPos, 0, 0); // 顶部动态
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
        // --- 1. 数据映射 ---
        for (int i = 0; i < pointCount; i++)
        {
            // 映射 spectrumData 到 rawHeights
            int sampleIdx = Mathf.FloorToInt((float)i / pointCount * (fftSize / 2));
            float target = spectrumData[sampleIdx] * heightScale;

            // 限制最大高度并应用上升下降速度
            rawHeights[i] = Mathf.Lerp(rawHeights[i], target, Time.deltaTime * (target > rawHeights[i] ? riseSpeed : fallSpeed));
        }

        // --- 2. 空间模糊 (平滑波浪) ---
        float[] blur = new float[pointCount];
        Array.Copy(rawHeights, blur, pointCount);
        for (int it = 0; it < smoothIterations; it++)
        {
            for (int i = 1; i < pointCount - 1; i++)
            {
                blur[i] = (blur[i - 1] + blur[i] + blur[i + 1]) / 3f;
            }
        }

        // --- 3. 同步到网格和线条 ---
        Vector3[] meshVerts = mesh.vertices;
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 v = new Vector3(-totalWidth / 2f + i * (totalWidth / (pointCount - 1)), blur[i], 0);
            topVertices[i] = v;
            meshVerts[i + pointCount] = v;
        }

        lineRenderer.SetPositions(topVertices);
        mesh.vertices = meshVerts;
        mesh.RecalculateBounds();
    }

    private void OnApplicationQuit()
    {
        if (capture != null)
        {
            capture.StopRecording();
            capture.Dispose();
        }
    }
}