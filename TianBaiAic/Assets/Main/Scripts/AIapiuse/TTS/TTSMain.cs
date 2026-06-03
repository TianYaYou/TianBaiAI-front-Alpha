using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 场景中的 TTS 控制器。
/// 负责读取配置文件、拼接 MIMO description、调用 TTSAiConnectApi，并把返回音频送进 AudioSource。
/// </summary>
public class TTSMain : MonoBehaviour
{
    public static TTSMain Instance { get; private set; }

    [Header("Config")]
    [Tooltip("为空时使用 TTSConfigLoader.GetConfigPath()。之后启动器可以写这个路径，或用环境变量指定新路径。")]
    public string configPathOverride = "";

    [Tooltip("配置文件不存在时自动生成模板，方便第一次运行时看到应该填写哪些字段。")]
    public bool createTemplateIfMissing = true;

    [Header("Playback")]
    public AudioSource audioSource;
    public bool stopCurrentBeforePlay = true;
    public bool autoPlay = true;
    public float pcmVolume = 1f;

    [Header("Debug")]
    public bool logTTS = true;

    private TTSAiConnectApi _session;
    private TTSConfig _config;
    private string _loadedConfigPath;
    private CancellationTokenSource _currentRequestCts;

    public bool IsSpeaking { get; private set; }
    public AudioClip LastClip { get; private set; }
    public string LastDescription { get; private set; }
    public string LastContent { get; private set; }

    private readonly Dictionary<string, string> _emotionDescriptions = new Dictionary<string, string>
    {
        { "自然", "语气自然、亲近，像在轻声回应用户。" },
        { "高兴", "语气更明亮一点，带一点轻快的笑意。" },
        { "开心", "语气更明亮一点，带一点轻快的笑意。" },
        { "害怕", "语气略微放轻，带一点不安，但仍保持清晰。" },
        { "嗔怪", "语气带一点轻轻的责备和撒娇感，不要真的生气。" },
        { "失望", "语气稍微低落一点，但仍然温柔克制。" },
        { "疑问", "句尾带一点询问感，像认真确认用户的意思。" },
        { "挑逗", "语气更慵懒、亲近一点，带轻微玩笑感，保持自然不过火。" },
        { "温柔", "语气柔和、慢一点，像在安抚用户。" }
    };

    private void Awake()
    {
        Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>
    /// 启动器修改配置后调用这个方法，让下一次 TTS 重新读取配置。
    /// </summary>
    public void ReloadConfig()
    {
        _session = null;
        _config = null;
        _loadedConfigPath = null;
        LastClip = null;
    }

    /// <summary>
    /// 静态入口，方便未来 AI 主链路直接调用。
    /// </summary>
    public static bool TrySpeak(
        string content,
        string emotion = "",
        Action<AudioClip> onComplete = null,
        Action<string> onError = null)
    {
        var controller = GetOrCreateInstance();
        if (controller == null)
        {
            Debug.LogError("[TTSMain] No TTSMain available.");
            return false;
        }

        _ = controller.SpeakAsync(content, emotion, onComplete, onError);
        return true;
    }

    private static TTSMain GetOrCreateInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<TTSMain>();
        if (Instance != null) return Instance;

        var go = new GameObject("TTSMain");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<TTSMain>();
        return Instance;
    }

    /// <summary>
    /// 对外主入口：让天白说一句话。
    /// content 是要朗读的文本；emotion 来自 AI 返回 JSON 的 emotion 字段或调用方自行指定。
    /// </summary>
    public async Task<AudioClip> SpeakAsync(
        string content,
        string emotion = "",
        Action<AudioClip> onComplete = null,
        Action<string> onError = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            onError?.Invoke("TTS content is empty.");
            return null;
        }

        if (!TryEnsureSession(out string reason))
        {
            Debug.LogWarning($"[TTSMain] Config not ready: {reason}");
            onError?.Invoke(reason);
            return null;
        }

        if (stopCurrentBeforePlay)
        {
            Stop();
        }

        _currentRequestCts = new CancellationTokenSource();
        IsSpeaking = true;
        LastContent = content;
        LastDescription = BuildVoiceDescription(emotion);

        try
        {
            if (logTTS)
            {
                Debug.Log($"[TTSMain] Start TTS. emotion={NormalizeEmotion(emotion)}, content={content}");
                Debug.Log($"[TTSMain] Voice description: {LastDescription}");
            }

            TTSAudioResult result = await _session.GenerateSpeechAsync(
                LastDescription,
                content,
                onError,
                _currentRequestCts.Token);

            if (result == null)
            {
                return null;
            }

            AudioClip clip = CreateClipFromResult(result);
            if (clip == null)
            {
                onError?.Invoke("AudioClip decode failed.");
                return null;
            }

            LastClip = clip;
            if (autoPlay && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                if (logTTS) Debug.Log($"[TTSMain] Playing clip: {clip.name}, length={clip.length:0.00}s");
            }

            onComplete?.Invoke(clip);
            return clip;
        }
        catch (Exception e)
        {
            string error = $"TTS failed: {e.Message}";
            Debug.LogError($"[TTSMain] {error}");
            onError?.Invoke(error);
            return null;
        }
        finally
        {
            IsSpeaking = false;
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;
        }
    }

    public void Stop()
    {
        if (_currentRequestCts != null && !_currentRequestCts.IsCancellationRequested)
        {
            _currentRequestCts.Cancel();
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private bool TryEnsureSession(out string reason)
    {
        reason = null;
        if (_session != null) return true;

        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            if (createTemplateIfMissing)
            {
                CreateTemplateConfig(path);
            }

            reason = $"TTS config file not found or template just created: {path}";
            return false;
        }

        if (!TTSConfigLoader.TryLoadFromPath(path, out _config, out reason))
        {
            return false;
        }

        _session = new TTSAiConnectApi(_config, _config.BuildDefaultSessionSettings());
        _loadedConfigPath = path;
        if (logTTS) Debug.Log($"[TTSMain] Loaded config: {_loadedConfigPath}");
        return true;
    }

    private string BuildVoiceDescription(string emotion)
    {
        if (_config == null)
        {
            return "";
        }

        if (!_config.AppendEmotionToDescription)
        {
            return _config.BaseVoiceDescription;
        }

        string normalized = NormalizeEmotion(emotion);
        string emotionText = _emotionDescriptions.TryGetValue(normalized, out string desc)
            ? desc
            : $"根据句子内容表现“{normalized}”的情绪，但不要破坏固定音色。";

        // my_description = 固定声音底色 + 当前句子的情绪微调。
        return $"{_config.BaseVoiceDescription}\n当前情绪：{emotionText}";
    }

    private string NormalizeEmotion(string emotion)
    {
        if (_config == null || string.IsNullOrWhiteSpace(_config.DefaultEmotion))
        {
            return string.IsNullOrWhiteSpace(emotion) ? "自然" : emotion.Trim();
        }

        return string.IsNullOrWhiteSpace(emotion) ? _config.DefaultEmotion : emotion.Trim();
    }

    private AudioClip CreateClipFromResult(TTSAudioResult result)
    {
        if (result == null || result.AudioBytes == null || result.AudioBytes.Length == 0)
        {
            return null;
        }

        if (result.IsWav)
        {
            return WavAudioClipUtility.TryCreateFromWav(result.AudioBytes, "MIMO_TTS_WAV");
        }

        // MIMO 偶尔可能返回裸 PCM，小米 Python 测试脚本里就是按 int16 + 24000Hz 兜底播放。
        return WavAudioClipUtility.CreateFromPcm16(
            result.AudioBytes,
            "MIMO_TTS_PCM",
            Mathf.Max(1, result.FallbackChannels),
            Mathf.Max(8000, result.FallbackSampleRate),
            pcmVolume);
    }

    private string GetConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(configPathOverride))
        {
            return configPathOverride;
        }

        return TTSConfigLoader.GetConfigPath();
    }

    private static void CreateTemplateConfig(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var config = TTSConfig.CreateTemplate();
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
        Debug.Log($"[TTSMain] Created TTS config template: {path}");
    }
}

/// <summary>
/// 把 MIMO 返回的音频字节转成 Unity AudioClip。
/// 当前支持 WAV PCM16 / WAV float32 / 裸 PCM16，后续返回格式扩展时集中改这里。
/// </summary>
public static class WavAudioClipUtility
{
    public static AudioClip TryCreateFromWav(byte[] wavBytes, string clipName)
    {
        if (wavBytes == null || wavBytes.Length < 44)
        {
            Debug.LogWarning("[TTSMain] WAV bytes too short.");
            return null;
        }

        try
        {
            int position = 12;
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int audioFormat = 0;
            int dataStart = -1;
            int dataSize = 0;

            // RIFF/WAVE 由多个 chunk 组成，fmt 和 data 的顺序理论上不固定，所以这里逐块扫描。
            while (position + 8 <= wavBytes.Length)
            {
                string chunkId = ReadFourCc(wavBytes, position);
                int chunkSize = ReadInt32LE(wavBytes, position + 4);
                int chunkDataStart = position + 8;

                if (chunkSize < 0 || chunkDataStart + chunkSize > wavBytes.Length)
                {
                    break;
                }

                if (chunkId == "fmt ")
                {
                    audioFormat = ReadInt16LE(wavBytes, chunkDataStart);
                    channels = ReadInt16LE(wavBytes, chunkDataStart + 2);
                    sampleRate = ReadInt32LE(wavBytes, chunkDataStart + 4);
                    bitsPerSample = ReadInt16LE(wavBytes, chunkDataStart + 14);
                }
                else if (chunkId == "data")
                {
                    dataStart = chunkDataStart;
                    dataSize = chunkSize;
                }

                position = chunkDataStart + chunkSize;
                if ((chunkSize & 1) == 1) position += 1;
            }

            if (dataStart < 0 || dataSize <= 0 || channels <= 0 || sampleRate <= 0)
            {
                Debug.LogWarning("[TTSMain] WAV header missing fmt/data chunk.");
                return null;
            }

            if (audioFormat == 1 && bitsPerSample == 16)
            {
                return CreateFromPcm16(wavBytes, clipName, channels, sampleRate, 1f, dataStart, dataSize);
            }

            if (audioFormat == 3 && bitsPerSample == 32)
            {
                return CreateFromFloat32(wavBytes, clipName, channels, sampleRate, dataStart, dataSize);
            }

            Debug.LogWarning($"[TTSMain] Unsupported WAV format. audioFormat={audioFormat}, bits={bitsPerSample}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTSMain] WAV decode failed: {e.Message}");
            return null;
        }
    }

    public static AudioClip CreateFromPcm16(
        byte[] bytes,
        string clipName,
        int channels,
        int sampleRate,
        float volume = 1f,
        int start = 0,
        int size = -1)
    {
        if (size < 0) size = bytes.Length - start;
        int sampleCount = size / 2;
        if (sampleCount <= 0 || channels <= 0)
        {
            return null;
        }

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int byteIndex = start + i * 2;
            short value = (short)(bytes[byteIndex] | (bytes[byteIndex + 1] << 8));
            samples[i] = Mathf.Clamp(value / 32768f * volume, -1f, 1f);
        }

        int frames = sampleCount / channels;
        AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateFromFloat32(
        byte[] bytes,
        string clipName,
        int channels,
        int sampleRate,
        int start,
        int size)
    {
        int sampleCount = size / 4;
        if (sampleCount <= 0 || channels <= 0)
        {
            return null;
        }

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToSingle(bytes, start + i * 4);
        }

        int frames = sampleCount / channels;
        AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static string ReadFourCc(byte[] bytes, int offset)
    {
        return $"{(char)bytes[offset]}{(char)bytes[offset + 1]}{(char)bytes[offset + 2]}{(char)bytes[offset + 3]}";
    }

    private static short ReadInt16LE(byte[] bytes, int offset)
    {
        return (short)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static int ReadInt32LE(byte[] bytes, int offset)
    {
        return bytes[offset]
               | (bytes[offset + 1] << 8)
               | (bytes[offset + 2] << 16)
               | (bytes[offset + 3] << 24);
    }
}
