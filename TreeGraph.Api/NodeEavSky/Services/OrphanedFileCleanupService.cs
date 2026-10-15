using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Shared.NodeEavSky;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 孤儿文件清理后台服务。
///
/// 核心算法：
///   1. 查 EAV：遍历 attribute_values 中 DataType="file" 的值，
///      从 value_file_meta (jsonb) 提取 docId → EavDocIds HashSet
///   2. 查 FileStorageApi：GET /Files/all-doc-ids → StorageDocIds HashSet
///   3. 差集 StorageDocIds - EavDocIds = 孤儿 docId
///   4. 逐个调 DELETE /Files/by-doc/{docId}（version=null，删最新版本）
///
/// 触发场景：
///   - 级联删除失败（SaveChanges 后 FileStorageApi HTTP 超时）
///   - 前端 FileField 直接调 DeleteFileAsync + ValueChanged(null)，
///     但 FileStorageApi 删成功了，而 EAV 层 value_file_meta 没清（理论上不会，
///     但如果用户在浏览器关闭前只完成了前一半，就会出现孤儿）
///
/// 安全网：
///   - 每次只删差集，不会误删被引用的文件
///   - 单个删除失败只记日志不中断循环
///   - 与 FileStorageApi.DeletePendingRetryService 互补
///     （后者负责"已进入 delete_pending 但 S3 还没真删掉"的，
///      本服务负责"EAV 已不引用但 FileStorageApi 里还活着"的）
/// </summary>
public sealed class OrphanedFileCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrphanedCleanupOptions _options;
    private readonly ILogger<OrphanedFileCleanupService> _logger;

    public OrphanedFileCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<OrphanedCleanupOptions> options,
        ILogger<OrphanedFileCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("孤儿文件清理服务已禁用。");
            return;
        }

        var startupDelay = _options.StartupDelay < TimeSpan.FromMinutes(2)
            ? TimeSpan.FromMinutes(2)
            : _options.StartupDelay;

        _logger.LogInformation(
            "孤儿文件清理服务已启动：启动延迟 {StartupDelay}，执行间隔 {Interval}。",
            startupDelay, _options.Interval);

        try
        {
            if (startupDelay > TimeSpan.Zero)
                await Task.Delay(startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await RunOnceAsync(stoppingToken)) break;
                await Task.Delay(_options.Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "孤儿文件清理服务遇到未预期异常，即将退出。");
        }
        finally
        {
            _logger.LogInformation("孤儿文件清理服务已停止。");
        }
    }

    private async Task<bool> RunOnceAsync(CancellationToken stoppingToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
            var inventory = scope.ServiceProvider.GetRequiredService<IFileInventoryClient>();
            var deleter = scope.ServiceProvider.GetRequiredService<IFileStorageDeleter>();

            // 1. EAV 侧：收集所有被引用的 docId
            var eavDocIds = new HashSet<string>();
            var fileAttrIds = await db.AttributeCatalog
                .Where(a => a.DataType == EavDataTypes.File)
                .Select(a => a.AttributeId)
                .ToListAsync(stoppingToken);

            if (fileAttrIds.Count > 0)
            {
                var fileValues = await db.AttributeValues
                    .Where(v => fileAttrIds.Contains(v.AttributeId)
                             && v.ValueFileMeta != null)
                    .Select(v => v.ValueFileMeta!)
                    .ToListAsync(stoppingToken);

                foreach (var meta in fileValues)
                {
                    try
                    {
                        var root = meta.RootElement;
                        if (root.TryGetProperty("docId", out var el)
                            && el.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var id = el.GetString();
                            if (!string.IsNullOrWhiteSpace(id))
                                eavDocIds.Add(id);
                        }
                    }
                    catch
                    {
                        // 损坏的 JSON，跳过
                    }
                }
            }

            // 2. FileStorageApi 侧：所有 docId
            var storageDocIds = await inventory.GetAllDocIdsAsync(stoppingToken);

            if (storageDocIds.Count == 0)
            {
                _logger.LogDebug("孤儿文件清理：FileStorageApi 为空。");
                return true;
            }

            // 3. 差集：FileStorageApi 有，EAV 没有 → 孤儿
            var orphans = storageDocIds.Except(eavDocIds).ToList();

            if (orphans.Count == 0)
            {
                sw.Stop();
                _logger.LogDebug(
                    "孤儿文件清理：无孤儿。EAV 引用 {Eav} 个，FileStorageApi 持有 {Storage} 个，耗时 {ElapsedMs} ms。",
                    eavDocIds.Count, storageDocIds.Count, sw.ElapsedMilliseconds);
                return true;
            }

            // 4. 批量删除孤儿（逐个，容错）
            var deleted = 0;
            foreach (var docId in orphans)
            {
                try
                {
                    if (await deleter.DeleteByDocIdAsync(docId, null, stoppingToken))
                        deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "孤儿文件删除跳过：docId={DocId}", docId);
                }
            }

            sw.Stop();
            _logger.LogInformation(
                "孤儿文件清理完成：发现 {Orphans} 个孤儿，删除 {Deleted} 个，EAV 引用 {Eav}，FileStorageApi 持有 {Storage}，耗时 {ElapsedMs} ms。",
                orphans.Count, deleted, eavDocIds.Count, storageDocIds.Count, sw.ElapsedMilliseconds);

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "孤儿文件清理失败（已运行 {ElapsedMs} ms），将在下一轮重试。",
                sw.ElapsedMilliseconds);
            return true;
        }
    }
}

public class OrphanedCleanupOptions
{
    public const string SectionName = "OrphanedCleanup";

    /// <summary>默认启用。设为 false 禁用整个服务。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>应用启动后多久开始首次清理（默认 5 分钟——等 FileStorageApi 完全就绪）。</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>清理执行间隔（默认 10 分钟）。</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);
}
