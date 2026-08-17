using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NebulaGraphApiService.Services;

public class NebulaGraphSeedService
{
    private readonly INebulaGraphClient _client;
    private readonly string _spaceName;
    private readonly ILogger<NebulaGraphSeedService> _logger;

    public NebulaGraphSeedService(
        INebulaGraphClient client,
        ILogger<NebulaGraphSeedService> logger,
        string spaceName = "social_network")
    {
        _client = client;
        _logger = logger;
        _spaceName = spaceName;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSpaceAsync(cancellationToken);
        await _client.UseSpaceAsync(_spaceName, cancellationToken);

        // 创建 Tags
        await CreateTagIfNotExistsAsync("person",
            new Dictionary<string, string>
            {
                { "name", "string" },
                { "age", "int" },
                { "gender", "string" }
            }, cancellationToken);

        await CreateTagIfNotExistsAsync("post",
            new Dictionary<string, string>
            {
                { "title", "string" },
                { "content", "string" },
                { "created_at", "timestamp" }
            }, cancellationToken);

        await CreateTagIfNotExistsAsync("comment",
            new Dictionary<string, string>
            {
                { "content", "string" },
                { "created_at", "timestamp" },
                { "likes", "int" }
            }, cancellationToken);

        // 创建 Edges
        await CreateEdgeIfNotExistsAsync("follows",
            new Dictionary<string, string>
            {
                { "since", "timestamp" }
            }, cancellationToken);

        await CreateEdgeIfNotExistsAsync("authored",
            new Dictionary<string, string>
            {
                { "timestamp", "timestamp" },
                { "publish_platform", "string" }
            }, cancellationToken);

        await CreateEdgeIfNotExistsAsync("replied",
            new Dictionary<string, string>
            {
                { "timestamp", "timestamp" },
                { "is_edited", "bool" }
            }, cancellationToken);

        // 创建索引 —— 为 string 字段指定索引长度 (64)，非字符串字段不指定
        // 显式指定元组类型为 (string, int?)，避免编译错误
        await CreateTagIndexIfNotExistsAsync("person_index", "person",
            new (string, int?)[] { ("name", 64) }, cancellationToken);

        await CreateTagIndexIfNotExistsAsync("post_index", "post",
            new (string, int?)[] { ("created_at", null) }, cancellationToken);

        await CreateEdgeIndexIfNotExistsAsync("follows_index", "follows",
            new (string, int?)[] { ("since", null) }, cancellationToken);
    }

    private async Task EnsureSpaceAsync(CancellationToken cancellationToken)
    {
        if (!await _client.SpaceExistsAsync(_spaceName, cancellationToken))
        {
            await _client.CreateSpaceAsync(_spaceName, cancellationToken);
            while (!await _client.SpaceReadyAsync(_spaceName, cancellationToken))
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    // ========== Tag 创建（带同步等待）==========
    private async Task CreateTagIfNotExistsAsync(
        string tagName,
        IDictionary<string, string> properties,
        CancellationToken cancellationToken)
    {
        if (await _client.TagExistsAsync(tagName, cancellationToken))
            return;

        var propDefs = string.Join(", ", properties.Select(p => $"`{p.Key}` {p.Value}"));
        var stmt = $"CREATE TAG IF NOT EXISTS `{tagName}` ({propDefs})";
        await _client.ExecuteAsync(stmt, cancellationToken);

        _logger.LogInformation("Tag {TagName} created, waiting for meta sync...", tagName);

        // 轮询确认 Tag 真正可见（最多等待 10 秒）
        for (int i = 0; i < 10; i++)
        {
            if (await _client.TagExistsAsync(tagName, cancellationToken))
            {
                _logger.LogInformation("Tag {TagName} is now visible.", tagName);
                return;
            }
            await Task.Delay(1000, cancellationToken);
        }
        throw new Exception($"Tag '{tagName}' not visible after creation (timeout).");
    }

    // ========== Edge 创建（带同步等待）==========
    private async Task CreateEdgeIfNotExistsAsync(
        string edgeName,
        IDictionary<string, string> properties,
        CancellationToken cancellationToken)
    {
        if (await _client.EdgeExistsAsync(edgeName, cancellationToken))
            return;

        var propDefs = string.Join(", ", properties.Select(p => $"`{p.Key}` {p.Value}"));
        var stmt = $"CREATE EDGE IF NOT EXISTS `{edgeName}` ({propDefs})";
        await _client.ExecuteAsync(stmt, cancellationToken);

        _logger.LogInformation("Edge {EdgeName} created, waiting for meta sync...", edgeName);

        for (int i = 0; i < 10; i++)
        {
            if (await _client.EdgeExistsAsync(edgeName, cancellationToken))
            {
                _logger.LogInformation("Edge {EdgeName} is now visible.", edgeName);
                return;
            }
            await Task.Delay(1000, cancellationToken);
        }
        throw new Exception($"Edge '{edgeName}' not visible after creation (timeout).");
    }

    // ========== Tag 索引（带长度支持 + 增强重试）==========
    private async Task CreateTagIndexIfNotExistsAsync(
        string indexName,
        string tagName,
        IEnumerable<(string Field, int? Length)> fields,
        CancellationToken cancellationToken)
    {
        if (await _client.IndexExistsAsync(indexName, cancellationToken))
            return;

        // 构建字段列表，如果指定了长度则追加 (长度)
        var fieldList = string.Join(", ", fields.Select(f =>
            $"`{f.Field}`{(f.Length.HasValue ? $"({f.Length.Value})" : "")}"));
        var stmt = $"CREATE TAG INDEX IF NOT EXISTS `{indexName}` ON `{tagName}` ({fieldList})";

        const int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _client.ExecuteAsync(stmt, cancellationToken);
                _logger.LogInformation("Tag index {IndexName} created successfully.", indexName);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries &&
                (ex.Message.Contains("Invalid param", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("not existed", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning(ex, "Retry creating tag index {IndexName}, attempt {Attempt}/{MaxRetries}",
                    indexName, attempt, maxRetries);
                await Task.Delay(3000, cancellationToken);
            }
        }

        // 重试失败后，再次检查索引是否已存在（可能被其他进程创建）
        if (await _client.IndexExistsAsync(indexName, cancellationToken))
            return;

        throw new Exception($"Failed to create tag index '{indexName}' after {maxRetries} retries.");
    }

    // ========== Edge 索引（带长度支持 + 增强重试）==========
    private async Task CreateEdgeIndexIfNotExistsAsync(
        string indexName,
        string edgeName,
        IEnumerable<(string Field, int? Length)> fields,
        CancellationToken cancellationToken)
    {
        if (await _client.IndexExistsAsync(indexName, cancellationToken))
            return;

        var fieldList = string.Join(", ", fields.Select(f =>
            $"`{f.Field}`{(f.Length.HasValue ? $"({f.Length.Value})" : "")}"));
        var stmt = $"CREATE EDGE INDEX IF NOT EXISTS `{indexName}` ON `{edgeName}` ({fieldList})";

        const int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _client.ExecuteAsync(stmt, cancellationToken);
                _logger.LogInformation("Edge index {IndexName} created successfully.", indexName);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries &&
                (ex.Message.Contains("Invalid param", StringComparison.OrdinalIgnoreCase) ||
                 ex.Message.Contains("not existed", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning(ex, "Retry creating edge index {IndexName}, attempt {Attempt}/{MaxRetries}",
                    indexName, attempt, maxRetries);
                await Task.Delay(3000, cancellationToken);
            }
        }

        if (await _client.IndexExistsAsync(indexName, cancellationToken))
            return;

        throw new Exception($"Failed to create edge index '{indexName}' after {maxRetries} retries.");
    }
}