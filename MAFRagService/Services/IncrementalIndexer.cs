using Hangfire;
using Microsoft.Extensions.Logging;

namespace MAFRagService.Services;

/// <summary>
/// 增量索引器。
/// 
/// <para><b>Hangfire 可调度方法约束：</b></para>
/// <list type="bullet">
///   <item>必须是 public <b>实例</b>方法；</item>
///   <item>参数必须可 JSON 序列化（string / int / Guid / 数组等）；</item>
///   <item><b>禁止</b>出现 CancellationToken 参数（Hangfire 无法序列化）；</item>
///   <item>返回 Task 走异步重载，返回 void 走同步重载。</item>
/// </list>
/// </summary>
public class IncrementalIndexer
{
    private readonly ILogger<IncrementalIndexer> _logger;
    // 其他依赖按实际项目注入，例如：
    // private readonly RagService _rag;
    // private readonly DocumentStorageService _storage;
    // private readonly DocumentMetadataService _metadata;

    public IncrementalIndexer(ILogger<IncrementalIndexer> logger /*, 其他依赖 */)
    {
        _logger = logger;
    }

    /// <summary>
    /// Hangfire 调度入口。
    /// <para>队列通过 [Queue] 特性指定为 "indexing"，与 HangfireServer 的 Queues 配置匹配。</para>
    /// </summary>
    /// <param name="docId">文档 ID（可 JSON 序列化）</param>
    /// <param name="version">版本号</param>
    /// <param name="tenant">租户 ID</param>
    [Queue("indexing")]
    public async Task IndexAsync(string docId, string version, string tenant)
    {
        // Hangfire 环境下取消令牌通过 JobCancellationToken 获取
        var ct = JobCancellationToken.Null.ShutdownToken;

        _logger.LogInformation(
            "开始增量索引 DocId={DocId}, Version={Version}, Tenant={Tenant}",
            docId, version, tenant);

        try
        {
            // ============================================================
            // TODO: 在此实现实际索引逻辑，典型步骤：
            //   1. 从 SeaweedFS 下载原文
            //   2. 分块（chunking）+ 生成嵌入（embedding）
            //   3. 写入 Weaviate
            //   4. 抽取实体/关系写入 NebulaGraph
            //   5. 更新 DocumentMetadata 的索引状态
            // ============================================================

            await Task.CompletedTask;   // 占位，替换为真实逻辑

            _logger.LogInformation(
                "增量索引完成 DocId={DocId}, Version={Version}", docId, version);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "增量索引被取消 DocId={DocId}, Version={Version}", docId, version);
            throw;   // 让 Hangfire 感知取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "增量索引失败 DocId={DocId}, Version={Version}, Tenant={Tenant}",
                docId, version, tenant);
            throw;   // 让 Hangfire 执行重试策略
        }
    }
}