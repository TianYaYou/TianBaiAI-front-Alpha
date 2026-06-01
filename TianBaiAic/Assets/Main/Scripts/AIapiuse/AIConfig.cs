using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum AICompatibilityMode
{
    OpenAI,
    Anthropic,
    Custom
}

[Serializable]
public class AIConfig
{
    [Header("Base")]
    public bool Enabled = false;
    public string Name = "Launcher_AI";
    public AICompatibilityMode Mode = AICompatibilityMode.OpenAI;
    public string ApiKey = "";

    [Header("Endpoints")]
    public string ApiHost = "https://api.openai.com/v1";
    public string ApiPath = "/chat/completions";
    public string ModelsPath = "/models";

    [Header("Session Defaults")]
    public string DefaultModel = "gpt-4o-mini";
    public float DefaultTemperature = 0.7f;
    public bool DefaultStream = false;
    public bool DefaultPassHistory = true;
    [TextArea(2, 6)] public string DefaultSystemPrompt = "You are a helpful assistant.";

    public List<string> ModelList = new List<string> { "gpt-4o", "gpt-3.5-turbo" };

    public bool IsReady()
    {
        return Enabled
               && !string.IsNullOrWhiteSpace(ApiHost)
               && !string.IsNullOrWhiteSpace(ApiPath)
               && !string.IsNullOrWhiteSpace(ApiKey);
    }

    public string GetChatEndpoint()
    {
        return BuildUrl(ApiHost, ApiPath);
    }

    public string GetModelsEndpoint()
    {
        return BuildUrl(ApiHost, ModelsPath);
    }

    public AISessionSettings BuildDefaultSessionSettings()
    {
        return new AISessionSettings
        {
            Model = string.IsNullOrWhiteSpace(DefaultModel) ? "gpt-4o-mini" : DefaultModel,
            Temperature = DefaultTemperature,
            Stream = DefaultStream,
            PassHistory = DefaultPassHistory,
            SystemPrompt = DefaultSystemPrompt
        };
    }

    private string BuildUrl(string host, string path)
    {
        if (string.IsNullOrEmpty(host)) return path;
        if (string.IsNullOrEmpty(path)) return host;
        return $"{host.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    public async Task<List<string>> TryFetchModelsAsync()
    {
        string url = GetModelsEndpoint();
        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey.Trim()}");

        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();

            JObject data = JObject.Parse(jsonResponse);
            List<string> fetchedModels = new List<string>();
            foreach (var item in data["data"])
            {
                string modelId = item["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    fetchedModels.Add(modelId);
                }
            }

            ModelList = fetchedModels;
            Debug.Log($"Loaded {ModelList.Count} models from {url}");
            return ModelList;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to fetch models: {e.Message}");
            return null;
        }
    }
}

public static class AIConfigLoader
{
    private const string DefaultConfigFileName = "launcher_ai_config.json";
    private const string EnvVarName = "TIANBAI_AI_CONFIG_PATH";

    public static bool TryLoad(out AIConfig config, out string reason)
    {
        config = null;
        reason = null;

        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            reason = $"Launcher config file not found: {path}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            config = JsonConvert.DeserializeObject<AIConfig>(json);
            if (config == null)
            {
                reason = $"Failed to parse config: {path}";
                return false;
            }

            if (!config.IsReady())
            {
                reason = "AI config is not ready. Enable and fill values in launcher first.";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            reason = $"Failed to read AI config: {e.Message}";
            return false;
        }
    }

    public static string GetConfigPath()
    {
        string envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        return Path.Combine(Application.persistentDataPath, DefaultConfigFileName);
    }
}
