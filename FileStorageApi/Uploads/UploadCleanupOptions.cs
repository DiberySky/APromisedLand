using System.ComponentModel.DataAnnotations;

namespace FileStorageApi.Uploads;

/// <summary>
/// 上传会话清理服务的配置项。
/// 绑定自 appsettings.json 的 <c>"UploadCleanup"</c> 节。
///
/// 用途：
///   - <see cref="UploadSessionCleanupService"/> 读取调度参数
///     （Enabled / IntervalMinutes / StartupDelayMinutes）
///   - <see cref="FileUploadService.CleanupExpiredAsync"/> 读取清理阈值
///     （StaleMergingMinutes / StalePendingMinutes / CompletedRetentionMinutes）
///
/// 配置示例（appsettings.json）：
/// <code>
/// {
///   "UploadCleanup": {
///     "Enabled": true,
///     "IntervalMinutes": 60,
///     "StartupDelayMinutes": 5,
///     "StaleMergingMinutes": 60,
///     "StalePendingMinutes": 120,
///     "CompletedRetentionMinutes": 10080,
///     "BatchSize": 500,
///     "OrphanDeleteConcurrency": 4
///   }
/// }
/// </code>
///
/// 边界与兜底：
///   - 所有时间字段通过 <see cref="EffectiveInterval"/>、
///     <see cref="EffectiveStartupDelay"/> 等计算属性做下限兜底，
///     避免因误配 0 或负数导致空转或高频执行。
///   - 校验通过 <see cref="IValidatableObject"/> 实现，
///     启动时配合 <c>ValidateOnStart()</c> 可在配置错误时快速失败。
/// </summary>
public sealed class UploadCleanupOptions : IValidatableObject
{
    /// <summary>配置节名称：<c>"UploadCleanup"</c>。</summary>
    public const string SectionName = "UploadCleanup";

    // ── 常量：下限 / 上限，避免 magic number 散落 ──
    public const int MinIntervalMinutes   = 1;
    public const int MaxIntervalMinutes   = 24 * 60;           // 24 小时
    public const int MinStartupDelayMin   = 0;
    public const int MaxStartupDelayMin   = 60;                // 1 小时
    public const int MinStaleMergingMin   = 1;
    public const int MaxStaleMergingMin   = 7 * 24 * 60;       // 7 天
    public const int MinStalePendingMin   = 1;
    public const int MaxStalePendingMin   = 30 * 24 * 60;      // 30 天
    public const int MinBatchSize         = 1;
    public const int MaxBatchSize         = 10_000;
    public const int MinOrphanConcurrency = 1;
    public const int MaxOrphanConcurrency = 64;

    // ★ 新增：completed 保留期边界（0 表示禁用）
    public const int MinCompletedRetentionMin = 0;
    public const int MaxCompletedRetentionMin = 90 * 24 * 60;  // 90 天

    // ══════════════════════════════════════════════════════════
    // 调度参数（UploadSessionCleanupService 使用）
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 是否启用清理服务。默认 <c>true</c>。
    /// 关闭后 UploadSessionCleanupService 会在启动时记录日志并立即退出，
    /// 不占用任何 CPU。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 执行间隔（分钟）。默认 <c>60</c>。
    /// 有效范围 [<see cref="MinIntervalMinutes"/>, <see cref="MaxIntervalMinutes"/>]。
    /// 小于下限时由 <see cref="EffectiveInterval"/> 兜底为下限。
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 服务启动后首次执行前的延迟（分钟）。默认 <c>5</c>。
    /// 用于避开应用启动、EF 迁移、预热期。
    /// 有效范围 [<see cref="MinStartupDelayMin"/>, <see cref="MaxStartupDelayMin"/>]。
    /// </summary>
    public int StartupDelayMinutes { get; set; } = 5;

    // ══════════════════════════════════════════════════════════
    // 清理阈值（FileUploadService.CleanupExpiredAsync 使用）
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// <c>merging</c> 状态超时阈值（分钟）。默认 <c>60</c>。
    ///
    /// 含义：
    ///   若某会话的 <c>Status = "merging"</c> 且
    ///   <c>UpdatedAt &lt; now - StaleMergingMinutes</c>，
    ///   则视为合并卡死，置为 <c>"failed"</c>。
    ///
    /// 建议：
    ///   设为"正常情况下 CompleteAsync 最长耗时的 3~5 倍"。
    /// </summary>
    public int StaleMergingMinutes { get; set; } = 60;

    /// <summary>
    /// <c>pending_upload</c> 元数据超时阈值（分钟）。默认 <c>120</c>。
    ///
    /// 含义：
    ///   若某 <c>DocumentMetadata</c> 的 <c>Status = "pending_upload"</c> 且
    ///   <c>UpdatedAt &lt; now - StalePendingMinutes</c>，
    ///   则视为 S3 上传/落库失败留下的孤儿，删除 S3 对象与元数据行。
    ///
    /// 建议：
    ///   必须大于 <see cref="StaleMergingMinutes"/>，
    ///   否则会在会话仍在 merging 时就把元数据清掉。
    /// </summary>
    public int StalePendingMinutes { get; set; } = 120;

    /// <summary>
    /// <c>completed</c> 会话保留期（分钟）。默认 <c>10080</c>（7 天）。
    ///
    /// 含义：
    ///   若某会话 <c>Status = "completed"</c> 且
    ///   <c>CompletedAt &lt; now - CompletedRetentionMinutes</c>，
    ///   则删除该会话行（分块已在完成时清空）。
    ///
    /// 设为 <c>0</c> 表示禁用 completed 清理（会话永久保留）。
    ///
    /// 保留期用于支持客户端在完成后的幂等查询 / 断点恢复。
    /// </summary>
    public int CompletedRetentionMinutes { get; set; } = 7 * 24 * 60;

    // ══════════════════════════════════════════════════════════
    // 批量与并发参数（可选，用于大表场景）
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 单次清理批量大小。默认 <c>500</c>。
    /// 用于避免一次性加载过多过期会话导致内存抖动。
    /// 有效范围 [<see cref="MinBatchSize"/>, <see cref="MaxBatchSize"/>]。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 删除 S3 孤儿对象的并发度。默认 <c>4</c>。
    /// 高延迟环境下适度提高可缩短清理时间，但会增加 S3 压力。
    /// 有效范围 [<see cref="MinOrphanConcurrency"/>, <see cref="MaxOrphanConcurrency"/>]。
    /// </summary>
    public int OrphanDeleteConcurrency { get; set; } = 4;

    // ══════════════════════════════════════════════════════════
    // 计算属性：下限 / 上限兜底（避免调用方各自 clamp）
    // ══════════════════════════════════════════════════════════

    /// <summary>生效的执行间隔，已做 [Min, Max] 夹取。</summary>
    public TimeSpan EffectiveInterval =>
        TimeSpan.FromMinutes(
            Math.Clamp(IntervalMinutes, MinIntervalMinutes, MaxIntervalMinutes));

    /// <summary>生效的启动延迟，已做 [Min, Max] 夹取。</summary>
    public TimeSpan EffectiveStartupDelay =>
        TimeSpan.FromMinutes(
            Math.Clamp(StartupDelayMinutes, MinStartupDelayMin, MaxStartupDelayMin));

    /// <summary>生效的 stale merging 阈值，已做 [Min, Max] 夹取。</summary>
    public TimeSpan EffectiveStaleMerging =>
        TimeSpan.FromMinutes(
            Math.Clamp(StaleMergingMinutes, MinStaleMergingMin, MaxStaleMergingMin));

    /// <summary>生效的 stale pending_upload 阈值，已做 [Min, Max] 夹取。</summary>
    public TimeSpan EffectiveStalePending =>
        TimeSpan.FromMinutes(
            Math.Clamp(StalePendingMinutes, MinStalePendingMin, MaxStalePendingMin));

    /// <summary>
    /// 生效的 completed 保留期。
    /// <c>0</c> 表示禁用 completed 清理，返回 <see cref="TimeSpan.Zero"/>。
    /// </summary>
    public TimeSpan EffectiveCompletedRetention =>
        CompletedRetentionMinutes <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMinutes(
                Math.Min(CompletedRetentionMinutes, MaxCompletedRetentionMin));

    /// <summary>生效的批量大小。</summary>
    public int EffectiveBatchSize =>
        Math.Clamp(BatchSize, MinBatchSize, MaxBatchSize);

    /// <summary>生效的 S3 孤儿删除并发度。</summary>
    public int EffectiveOrphanDeleteConcurrency =>
        Math.Clamp(OrphanDeleteConcurrency, MinOrphanConcurrency, MaxOrphanConcurrency);

    // ══════════════════════════════════════════════════════════
    // 校验：在 ValidateOnStart 时执行，配置错误快速失败
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IntervalMinutes < MinIntervalMinutes || IntervalMinutes > MaxIntervalMinutes)
        {
            yield return new ValidationResult(
                $"{nameof(IntervalMinutes)} 必须在 " +
                $"[{MinIntervalMinutes}, {MaxIntervalMinutes}] 之间。",
                new[] { nameof(IntervalMinutes) });
        }

        if (StartupDelayMinutes < MinStartupDelayMin ||
            StartupDelayMinutes > MaxStartupDelayMin)
        {
            yield return new ValidationResult(
                $"{nameof(StartupDelayMinutes)} 必须在 " +
                $"[{MinStartupDelayMin}, {MaxStartupDelayMin}] 之间。",
                new[] { nameof(StartupDelayMinutes) });
        }

        if (StaleMergingMinutes < MinStaleMergingMin ||
            StaleMergingMinutes > MaxStaleMergingMin)
        {
            yield return new ValidationResult(
                $"{nameof(StaleMergingMinutes)} 必须在 " +
                $"[{MinStaleMergingMin}, {MaxStaleMergingMin}] 之间。",
                new[] { nameof(StaleMergingMinutes) });
        }

        if (StalePendingMinutes < MinStalePendingMin ||
            StalePendingMinutes > MaxStalePendingMin)
        {
            yield return new ValidationResult(
                $"{nameof(StalePendingMinutes)} 必须在 " +
                $"[{MinStalePendingMin}, {MaxStalePendingMin}] 之间。",
                new[] { nameof(StalePendingMinutes) });
        }

        // ★ 关键约束：pending_upload 阈值必须 > merging 阈值
        //    否则可能出现"会话仍在合并，元数据已被清理"的竞态
        if (StalePendingMinutes <= StaleMergingMinutes)
        {
            yield return new ValidationResult(
                $"{nameof(StalePendingMinutes)}（{StalePendingMinutes}）" +
                $"必须大于 {nameof(StaleMergingMinutes)}（{StaleMergingMinutes}），" +
                $"否则可能在会话合并中清理掉元数据。",
                new[] { nameof(StalePendingMinutes), nameof(StaleMergingMinutes) });
        }

        // ★ 新增：completed 保留期校验
        if (CompletedRetentionMinutes < MinCompletedRetentionMin ||
            CompletedRetentionMinutes > MaxCompletedRetentionMin)
        {
            yield return new ValidationResult(
                $"{nameof(CompletedRetentionMinutes)} 必须在 " +
                $"[{MinCompletedRetentionMin}, {MaxCompletedRetentionMin}] 之间" +
                $"（0 表示禁用 completed 清理）。",
                new[] { nameof(CompletedRetentionMinutes) });
        }

        if (BatchSize < MinBatchSize || BatchSize > MaxBatchSize)
        {
            yield return new ValidationResult(
                $"{nameof(BatchSize)} 必须在 " +
                $"[{MinBatchSize}, {MaxBatchSize}] 之间。",
                new[] { nameof(BatchSize) });
        }

        if (OrphanDeleteConcurrency < MinOrphanConcurrency ||
            OrphanDeleteConcurrency > MaxOrphanConcurrency)
        {
            yield return new ValidationResult(
                $"{nameof(OrphanDeleteConcurrency)} 必须在 " +
                $"[{MinOrphanConcurrency}, {MaxOrphanConcurrency}] 之间。",
                new[] { nameof(OrphanDeleteConcurrency) });
        }
    }
}