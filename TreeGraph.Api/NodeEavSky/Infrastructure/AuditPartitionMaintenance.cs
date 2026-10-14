using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.NodeEavSky.Infrastructure;

/// <summary>
/// attribute_audit_log 月度分区维护。
///
/// 表为 PARTITION BY RANGE (changed_at) 的声明式分区表（DDL 在迁移中手写创建，
/// 含 DEFAULT 兜底分区）。启动时确保【当月 + 下月】两个分区存在：
///   - 已有 DEFAULT 分区兜底，未建月度分区时写入不会失败（落到兜底分区），
///     此处创建分区只为让数据按月归位，便于后续按月 DETACH 归档/清理；
///   - CREATE TABLE IF NOT EXISTS ... PARTITION OF 幂等，可每次启动安全执行。
/// </summary>
public static class AuditPartitionMaintenance
{
    public static async Task EnsureMonthlyPartitionsAsync(
        TreeGraphDbContext db, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsurePartitionAsync(db, now.Year, now.Month, ct);
        var next = now.AddMonths(1);
        await EnsurePartitionAsync(db, next.Year, next.Month, ct);
    }

    private static async Task EnsurePartitionAsync(
        TreeGraphDbContext db, int year, int month, CancellationToken ct)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        // 分区名/边界均为本地生成的数字与 ISO 日期，无注入面。
        var sql = $"""
            CREATE TABLE IF NOT EXISTS attribute_audit_log_{year}_{month:D2}
                PARTITION OF attribute_audit_log
                FOR VALUES FROM ('{from:yyyy-MM-dd}') TO ('{to:yyyy-MM-dd}');
            """;

        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
