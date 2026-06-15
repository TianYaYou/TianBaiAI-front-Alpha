using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TianBaiAI.Memory;
using UnityEngine;

/// <summary>
/// C 通道默认实现：本地记忆检索。
/// 当前复用 MemoryService 的 JSONL 存储；未来替换成语义检索时只需要换这个类。
/// </summary>
public class MemorySearchChannel : MonoBehaviour, IAIMemoryProvider
{
    [Header("Memory")]
    public int defaultLimit = 8;

    [Header("Debug")]
    public bool logMemory = true;

    public Task<AIMemoryResult> SearchMemoryAsync(
        AIMemoryQuery query,
        AITurnContext context,
        CancellationToken cancellationToken = default)
    {
        query ??= new AIMemoryQuery();
        string queryText = string.IsNullOrWhiteSpace(query.QueryText) ? context?.UserInput : query.QueryText;
        int limit = query.Limit > 0 ? query.Limit : defaultLimit;
        string turnId = !string.IsNullOrWhiteSpace(query.TurnId) ? query.TurnId : context?.TurnId;

        Stopwatch stopwatch = Stopwatch.StartNew();
        var result = new AIMemoryResult
        {
            TurnId = turnId,
            QueryText = queryText,
            Source = "MemoryService"
        };

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(result);
        }

        try
        {
            var legacyQuery = new LegacyReadMemory
            {
                content_key = queryText
            };

            var records = MemoryService.ReadMemory(legacyQuery, limit);
            foreach (MemoryRecord record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.content)) continue;
                result.Items.Add(new AIMemoryItem
                {
                    Id = record.id,
                    Content = record.content,
                    Type = record.keys != null && record.keys.Count > 0 ? string.Join(",", record.keys) : "memory",
                    Score = 1f,
                    Source = record.source
                });
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[MemorySearchChannel] Memory query failed: {e.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = (int)stopwatch.ElapsedMilliseconds;
        }

        if (logMemory)
        {
            UnityEngine.Debug.Log($"[MemorySearchChannel] query={queryText}, results={result.Items.Count}, elapsed={result.ElapsedMs}ms");
        }

        return Task.FromResult(result);
    }
}
