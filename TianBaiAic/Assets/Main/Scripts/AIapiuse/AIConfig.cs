using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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
    [Header("基本设置")]
    public string Name = "Default_AI";
    public AICompatibilityMode Mode = AICompatibilityMode.OpenAI;
    public string ApiKey = "";

    [Header("路由设置")]
    public string ApiHost = "https://api.openai.com/v1";
    public string ApiPath = "/chat/completions";
    public string ModelsPath = "/models";

    public List<string> ModelList = new List<string> { "gpt-4o", "gpt-3.5-turbo" };

    /// <summary>
    /// 智能拼接聊天 API 的完整 URL
    /// </summary>
    public string GetChatEndpoint()
    {
        return BuildUrl(ApiHost, ApiPath);
    }

    /// <summary>
    /// 智能拼接获取模型 API 的完整 URL
    /// </summary>
    public string GetModelsEndpoint()
    {
        return BuildUrl(ApiHost, ModelsPath);
    }

    private string BuildUrl(string host, string path)
    {
        if (string.IsNullOrEmpty(host)) return path;
        if (string.IsNullOrEmpty(path)) return host;
        return $"{host.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    /// <summary>
    /// 尝试从 API 获取模型列表
    /// </summary>
    public async Task<List<string>> TryFetchModelsAsync()
    {
        string url = GetModelsEndpoint();
        using HttpClient client = new HttpClient();

        // 自动清理密钥前后的空格，防止 401 报错
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
                fetchedModels.Add(item["id"].ToString());
            }

            ModelList = fetchedModels;
            Debug.Log($"成功从 {url} 获取 {ModelList.Count} 个模型");
            return ModelList;
        }
        catch (Exception e)
        {
            Debug.LogError($"获取模型列表失败: {e.Message}\n如果确认密钥无误但仍报错，可能是该服务商未开放 /models 接口，可忽略此错误。");
            return null;
        }
    }
}