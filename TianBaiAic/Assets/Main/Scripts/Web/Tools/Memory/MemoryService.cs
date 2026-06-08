using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TianBaiAI.Memory
{
    /// <summary>
    /// 单条记忆记录。当前默认写入 JSONL，后续替换为语义检索时仍可复用这层数据模型。
    /// </summary>
    [Serializable]
    public class MemoryRecord
    {
        public string id;
        public string time;
        public List<string> keys = new List<string>();
        public string content;
        public string source;
    }

    /// <summary>
    /// 记忆查询条件。contentKey 是内容关键词，key 是分类关键词。
    /// </summary>
    [Serializable]
    public class MemoryQuery
    {
        public string time;
        public string key;
        public string contentKey;
    }

    /// <summary>
    /// 好感度快照。先存一个 0-1 的值，之后可以改成事件流或曲线系统。
    /// </summary>
    [Serializable]
    public class FavorabilitySnapshot
    {
        public float value = 0.5f;
        public string updatedAt;
        public string source;
    }

    /// <summary>
    /// 存储抽象接口。未来从 JSONL/CSV 切到向量检索时，只需要换实现，不动 AI 分发逻辑。
    /// </summary>
    public interface IMemoryStore
    {
        void Write(MemoryRecord record);
        List<MemoryRecord> Query(MemoryQuery query, int limit = 20);
        FavorabilitySnapshot LoadFavorability();
        void SaveFavorability(FavorabilitySnapshot snapshot);
    }

    /// <summary>
    /// 默认本地存储：memory.jsonl 存记忆，favorability.json 存好感度。
    /// JSONL 比 CSV 更容易兼容多 key 和未来扩展字段。
    /// </summary>
    public class JsonLineMemoryStore : IMemoryStore
    {
        private readonly string _rootPath;
        private string MemoryFile => Path.Combine(_rootPath, "memory.jsonl");
        private string FavorabilityFile => Path.Combine(_rootPath, "favorability.json");

        public JsonLineMemoryStore(string rootPath)
        {
            _rootPath = rootPath;
        }

        public void Write(MemoryRecord record)
        {
            EnsureRoot();
            File.AppendAllText(MemoryFile, JsonConvert.SerializeObject(record) + Environment.NewLine);
        }

        public List<MemoryRecord> Query(MemoryQuery query, int limit = 20)
        {
            EnsureRoot();
            var results = new List<MemoryRecord>();
            if (!File.Exists(MemoryFile)) return results;

            foreach (string line in File.ReadLines(MemoryFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                MemoryRecord record;
                try
                {
                    record = JsonConvert.DeserializeObject<MemoryRecord>(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (record == null) continue;
                if (!Matches(record, query)) continue;

                results.Add(record);
                if (results.Count >= limit) break;
            }

            return results;
        }

        public FavorabilitySnapshot LoadFavorability()
        {
            EnsureRoot();
            if (!File.Exists(FavorabilityFile))
            {
                return new FavorabilitySnapshot
                {
                    value = 0.5f,
                    updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    source = "default"
                };
            }

            try
            {
                return JsonConvert.DeserializeObject<FavorabilitySnapshot>(File.ReadAllText(FavorabilityFile))
                       ?? new FavorabilitySnapshot { value = 0.5f };
            }
            catch (JsonException)
            {
                return new FavorabilitySnapshot { value = 0.5f, source = "parse_failed" };
            }
        }

        public void SaveFavorability(FavorabilitySnapshot snapshot)
        {
            EnsureRoot();
            File.WriteAllText(FavorabilityFile, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        }

        private void EnsureRoot()
        {
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);
        }

        private static bool Matches(MemoryRecord record, MemoryQuery query)
        {
            if (query == null) return true;

            if (!string.IsNullOrWhiteSpace(query.time) && !Contains(record.time, query.time)) return false;

            if (!string.IsNullOrWhiteSpace(query.key))
            {
                bool keyMatched = false;
                if (record.keys != null)
                {
                    foreach (string key in record.keys)
                    {
                        if (Contains(key, query.key))
                        {
                            keyMatched = true;
                            break;
                        }
                    }
                }

                if (!keyMatched) return false;
            }

            if (!string.IsNullOrWhiteSpace(query.contentKey) && !Contains(record.content, query.contentKey)) return false;
            return true;
        }

        private static bool Contains(string text, string value)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(value)) return false;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// AI 记忆系统入口。AIResponseDispatcher 通过这里写入记忆和好感度。
    /// </summary>
    public static class MemoryService
    {
        private const string EnvVarName = "TIANBAI_MEMORY_PATH";
        private static IMemoryStore _store;

        public static string DefaultMemoryRoot
        {
            get
            {
                string envPath = Environment.GetEnvironmentVariable(EnvVarName);
                if (!string.IsNullOrWhiteSpace(envPath)) return envPath;
                return Path.Combine(Application.streamingAssetsPath, "Memory");
            }
        }

        public static void ConfigureStore(IMemoryStore store)
        {
            _store = store;
        }

        public static MemoryRecord WriteMemory(LegacyWriteMemory memory, string source = "ai")
        {
            if (memory == null || string.IsNullOrWhiteSpace(memory.content)) return null;

            var record = new MemoryRecord
            {
                id = Guid.NewGuid().ToString("N"),
                time = string.IsNullOrWhiteSpace(memory.time) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : memory.time,
                keys = memory.key != null ? new List<string>(memory.key) : new List<string>(),
                content = memory.content,
                source = source
            };

            GetStore().Write(record);
            Debug.Log($"[MemoryService] 写入记忆: {record.content}");
            return record;
        }

        public static List<MemoryRecord> ReadMemory(LegacyReadMemory memory, int limit = 20)
        {
            if (memory == null) return new List<MemoryRecord>();

            var query = new MemoryQuery
            {
                time = memory.time,
                key = memory.key,
                contentKey = memory.content_key
            };

            List<MemoryRecord> results = GetStore().Query(query, limit);
            Debug.Log($"[MemoryService] 查询记忆: {results.Count} 条");
            return results;
        }

        public static FavorabilitySnapshot SaveFavorability(float value, string source = "ai")
        {
            var snapshot = new FavorabilitySnapshot
            {
                value = Mathf.Clamp01(value),
                updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                source = source
            };

            GetStore().SaveFavorability(snapshot);
            Debug.Log($"[MemoryService] 保存好感度: {snapshot.value:0.00}");
            return snapshot;
        }

        public static FavorabilitySnapshot LoadFavorability()
        {
            return GetStore().LoadFavorability();
        }

        private static IMemoryStore GetStore()
        {
            if (_store == null)
            {
                _store = new JsonLineMemoryStore(DefaultMemoryRoot);
            }

            return _store;
        }
    }
}
