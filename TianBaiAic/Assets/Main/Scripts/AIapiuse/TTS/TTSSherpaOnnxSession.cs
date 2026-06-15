using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eitan.SherpaONNXUnity.Runtime.Native;
using UnityEngine;

/// <summary>
/// 本地 sherpa-onnx TTS 会话。
/// 职责边界：只负责“加载本地模型 + 把文本合成为 float PCM 数据”，不直接操作 AudioSource。
/// 这样 TTSMain 可以用同一套播放逻辑承接远端 API 和本地模型两种结果。
/// </summary>
public sealed class TTSSherpaOnnxSession : IDisposable
{
    public TTSConfig Config { get; private set; }
    public bool IsInitialized => _tts != null;
    public bool IsGenerating { get; private set; }
    public int SampleRate => _tts != null ? _tts.SampleRate : 0;
    public int NumSpeakers => _tts != null ? _tts.NumSpeakers : 0;

    private readonly object _initLock = new object();
    private readonly object _generationLock = new object();
    private OfflineTts _tts;
    private string _loadedModelRoot;

    public TTSSherpaOnnxSession(TTSConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config), "TTSConfig 不能为空。");
    }

    /// <summary>
    /// 主动加载本地 sherpa-onnx 模型。
    /// TTSMain 会在场景开始时调用它，避免第一次真正说话时才初始化模型导致明显卡顿。
    /// </summary>
    public bool Preload(Action<string> onError = null)
    {
        try
        {
            EnsureInitialized();
            return true;
        }
        catch (Exception e)
        {
            onError?.Invoke($"本地 sherpa-onnx TTS 预加载失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 生成语音。
    /// description 参数是为了和远端 TTSAiConnectApi 保持相同调用形状；本地 VITS 模型目前不会读取声音描述。
    /// </summary>
    public async Task<TTSAudioResult> GenerateSpeechAsync(
        string description,
        string content,
        Action<string> onError = null,
        CancellationToken cancellationToken = default)
    {
        if (IsGenerating)
        {
            onError?.Invoke("当前有正在生成的本地 TTS，请稍后再试。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            onError?.Invoke("TTS content 不能为空。");
            return null;
        }

        try
        {
            EnsureInitialized();
        }
        catch (Exception e)
        {
            onError?.Invoke($"本地 sherpa-onnx TTS 初始化失败: {e.Message}");
            return null;
        }

        IsGenerating = true;

        try
        {
            // sherpa 的 Generate 是同步原生调用。放到后台线程，避免长句合成时阻塞 Unity 主线程。
            return await Task.Run(() => GenerateOnWorker(content, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            onError?.Invoke("本地 TTS 生成已取消。");
            return null;
        }
        catch (Exception e)
        {
            onError?.Invoke($"本地 sherpa-onnx TTS 生成失败: {e.Message}");
            return null;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public void Dispose()
    {
        lock (_initLock)
        {
            if (_tts == null)
            {
                return;
            }

            try
            {
                _tts.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TTSSherpaOnnxSession] Dispose sherpa TTS failed: {e.Message}");
            }
            finally
            {
                _tts = null;
                _loadedModelRoot = null;
            }
        }
    }

    private void EnsureInitialized()
    {
        if (_tts != null)
        {
            return;
        }

        lock (_initLock)
        {
            if (_tts != null)
            {
                return;
            }

            OfflineTtsConfig sherpaConfig = BuildSherpaConfig();
            _tts = new OfflineTts(sherpaConfig);

            if (_tts.SampleRate <= 0)
            {
                Dispose();
                throw new InvalidOperationException("sherpa-onnx 没有返回有效采样率，模型可能没有正确加载。");
            }

            Debug.Log($"[TTSSherpaOnnxSession] 本地 TTS 模型加载完成: {_loadedModelRoot}, sampleRate={_tts.SampleRate}, speakers={_tts.NumSpeakers}");
        }
    }

    private TTSAudioResult GenerateOnWorker(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // OfflineTts 不是为并发 Generate 设计的，这里显式串行化，避免多次说话同时打进原生层。
        lock (_generationLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OfflineTtsGeneratedAudio generatedAudio = null;
            try
            {
                int speakerId = Mathf.Max(0, Config.SherpaSpeakerId);
                float speed = Mathf.Clamp(Config.SherpaSpeed, 0.5f, 2f);
                generatedAudio = _tts.Generate(content.Trim(), speed, speakerId);

                if (generatedAudio == null || generatedAudio.NumSamples <= 0)
                {
                    throw new InvalidOperationException("sherpa-onnx 返回了空音频。");
                }

                float[] samples = generatedAudio.Samples;
                int sampleRate = generatedAudio.SampleRate > 0 ? generatedAudio.SampleRate : _tts.SampleRate;

                return new TTSAudioResult
                {
                    FloatSamples = samples,
                    SampleRate = sampleRate,
                    Channels = 1,
                    FallbackSampleRate = sampleRate,
                    FallbackChannels = 1,
                    AssistantText = content,
                    ClipName = $"Sherpa_TTS_{DateTime.Now:HHmmssfff}"
                };
            }
            finally
            {
                // 先把 Samples 拷贝到托管数组，再释放原生结果句柄。
                generatedAudio?.Dispose();
            }
        }
    }

    private OfflineTtsConfig BuildSherpaConfig()
    {
        string modelRoot = Config.ResolveSherpaPath(Config.SherpaModelRoot);
        _loadedModelRoot = modelRoot;

        if (!Directory.Exists(modelRoot))
        {
            throw new DirectoryNotFoundException($"sherpa 模型目录不存在: {modelRoot}");
        }

        string modelPath = RequireFile(Config.ResolveSherpaModelFile(Config.SherpaModelFile), "VITS model");
        string tokensPath = RequireFile(Config.ResolveSherpaModelFile(Config.SherpaTokensFile), "tokens");
        string lexiconPath = ResolveOptionalFile(Config.SherpaLexiconFile);
        string dictDir = ResolveOptionalDirectory(Config.SherpaDictDir);

        var sherpaConfig = new OfflineTtsConfig(true)
        {
            MaxNumSentences = Mathf.Max(1, Config.SherpaMaxNumSentences),
            SilenceScale = Mathf.Max(0f, Config.SherpaSilenceScale),
            RuleFsts = string.Join(",", ResolveExistingRuleFsts())
        };

        sherpaConfig.Model = new OfflineTtsModelConfig(true)
        {
            NumThreads = Mathf.Max(1, Config.SherpaNumThreads),
            Provider = string.IsNullOrWhiteSpace(Config.SherpaProvider) ? "cpu" : Config.SherpaProvider.Trim(),
            Debug = Config.SherpaDebug ? 1 : 0,
            Vits = new OfflineTtsVitsModelConfig(true)
            {
                Model = modelPath,
                Tokens = tokensPath,
                Lexicon = lexiconPath,
                DictDir = dictDir,
                NoiseScale = Mathf.Max(0f, Config.SherpaNoiseScale),
                NoiseScaleW = Mathf.Max(0f, Config.SherpaNoiseScaleW),
                LengthScale = Mathf.Clamp(Config.SherpaLengthScale, 0.5f, 2f)
            }
        };

        return sherpaConfig;
    }

    private IEnumerable<string> ResolveExistingRuleFsts()
    {
        if (Config.SherpaRuleFsts == null)
        {
            yield break;
        }

        foreach (string ruleFst in Config.SherpaRuleFsts)
        {
            string path = ResolveOptionalFile(ruleFst);
            if (!string.IsNullOrEmpty(path))
            {
                yield return path;
            }
        }
    }

    private string ResolveOptionalFile(string fileOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileOrPath))
        {
            return string.Empty;
        }

        string path = Config.ResolveSherpaModelFile(fileOrPath);
        return File.Exists(path) ? path : string.Empty;
    }

    private string ResolveOptionalDirectory(string dirOrPath)
    {
        if (string.IsNullOrWhiteSpace(dirOrPath))
        {
            return string.Empty;
        }

        string path = Path.IsPathRooted(dirOrPath)
            ? dirOrPath
            : Path.Combine(Config.ResolveSherpaPath(Config.SherpaModelRoot), dirOrPath);

        return Directory.Exists(path) ? path : string.Empty;
    }

    private static string RequireFile(string path, string label)
    {
        if (File.Exists(path))
        {
            return path;
        }

        throw new FileNotFoundException($"找不到 sherpa {label} 文件: {path}", path);
    }
}
