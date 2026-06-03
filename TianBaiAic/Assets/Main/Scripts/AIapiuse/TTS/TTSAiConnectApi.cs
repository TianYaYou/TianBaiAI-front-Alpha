using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 单次 TTS 会话的运行参数。
/// TTSConfig 负责从 JSON 保存配置；TTSSessionSettings 负责告诉 TTSAiConnectApi 这次请求怎么发。
/// </summary>
[Serializable]
public class TTSSessionSettings
{
    public string Model = "mimo-v2.5-tts-voicedesign";
    public string AudioFormat = "wav";
    public int FallbackSampleRate = 24000;
    public int FallbackChannels = 1;
}

/// <summary>
/// MIMO TTS 返回的音频结果。
/// TTSMain 会继续把 AudioBytes 转成 Unity AudioClip 并播放。
/// </summary>
public class TTSAudioResult
{
    public byte[] AudioBytes;
    public bool IsWav;
    public string AssistantText;
    public string RawJson;
    public int FallbackSampleRate;
    public int FallbackChannels;
}

/// <summary>
/// OpenAI-like TTS API 会话。
/// 负责拼接请求 payload、发送 HTTP 请求，并解析 choices[0].message.audio.data。
/// 它不直接操作 AudioSource；播放由 TTSMain 负责。
/// </summary>
public class TTSAiConnectApi
{
    public TTSConfig Config { get; private set; }
    public TTSSessionSettings Settings { get; private set; }
    public bool IsGenerating { get; private set; }

    public TTSAiConnectApi(TTSConfig config, TTSSessionSettings settings = null)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config), "TTSConfig 不能为空！");
        Settings = settings ?? config.BuildDefaultSessionSettings();
    }

    /// <summary>
    /// 请求 MIMO 生成语音。
    /// MIMO 的特殊点：user.content 是声音设计 description，assistant.content 是要朗读的正文。
    /// </summary>
    public async Task<TTSAudioResult> GenerateSpeechAsync(
        string description,
        string content,
        Action<string> onError = null,
        CancellationToken cancellationToken = default)
    {
        if (IsGenerating)
        {
            onError?.Invoke("当前有正在生成的 TTS，请稍后再试。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            onError?.Invoke("TTS voice description 不能为空。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            onError?.Invoke("TTS content 不能为空。");
            return null;
        }

        IsGenerating = true;

        try
        {
            JObject requestData = BuildRequestPayload(description, content);
            string jsonPayload = requestData.ToString(Formatting.None);
            string url = Config.GetTtsEndpoint();

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.ApiKey.Trim()}");

            using StringContent httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(url, httpContent, cancellationToken);

            string responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                onError?.Invoke($"MIMO TTS request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{responseText}");
                return null;
            }

            return ParseAudioResult(responseText);
        }
        catch (Exception e)
        {
            onError?.Invoke($"TTS API 请求失败: {e.Message}");
            return null;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private JObject BuildRequestPayload(string description, string content)
    {
        // MIMO TTS 是 Chat Completions 变体：消息结构像 Chat，但 extra body 需要 modalities/audio。
        return new JObject
        {
            ["model"] = Settings.Model,
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = description
                },
                new JObject
                {
                    ["role"] = "assistant",
                    ["content"] = content
                }
            },
            ["modalities"] = new JArray("text", "audio"),
            ["audio"] = new JObject
            {
                ["format"] = string.IsNullOrWhiteSpace(Settings.AudioFormat) ? "wav" : Settings.AudioFormat
            }
        };
    }

    private TTSAudioResult ParseAudioResult(string responseText)
    {
        JObject json = JObject.Parse(responseText);
        JToken message = json["choices"]?[0]?["message"];
        string audioBase64 = message?["audio"]?["data"]?.ToString();

        if (string.IsNullOrWhiteSpace(audioBase64))
        {
            throw new JsonException("MIMO TTS response does not contain choices[0].message.audio.data.");
        }

        byte[] audioBytes = Convert.FromBase64String(audioBase64);
        return new TTSAudioResult
        {
            AudioBytes = audioBytes,
            IsWav = IsWavBytes(audioBytes),
            AssistantText = message?["content"]?.ToString(),
            RawJson = responseText,
            FallbackSampleRate = Mathf.Max(8000, Settings.FallbackSampleRate),
            FallbackChannels = Mathf.Max(1, Settings.FallbackChannels)
        };
    }

    private static bool IsWavBytes(byte[] bytes)
    {
        return bytes != null
               && bytes.Length >= 12
               && bytes[0] == (byte)'R'
               && bytes[1] == (byte)'I'
               && bytes[2] == (byte)'F'
               && bytes[3] == (byte)'F'
               && bytes[8] == (byte)'W'
               && bytes[9] == (byte)'A'
               && bytes[10] == (byte)'V'
               && bytes[11] == (byte)'E';
    }
}
