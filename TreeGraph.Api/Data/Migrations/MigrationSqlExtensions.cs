using Microsoft.EntityFrameworkCore.Migrations;

namespace TreeGraph.Api.Data.Migrations;

/// <summary>
/// 手写 SQL 迁移操作的集中封装。
///
/// 为什么需要它：标了 <see cref="RelationalEntityTypeBuilderExtensions.ExcludeFromMigrations"/>
/// 的实体（当前唯一一处：TreeGraphDbContext.ConfigureAuditLog 里的按月分区表 attribute_audit_log），
/// EF 不会为其生成任何 DDL，必须由迁移手写 SQL 补回。若这些语句只以字面量形式散落在某个迁移体的
/// Up() 里，一旦 `dotnet ef migrations add` 重建 Initial（本项目开发期既定做法：drop 库 + 重建迁移），
/// 手写 SQL 就会连同旧迁移文件一起被删除，且不会有任何编译或快照层面的报错 ——
/// 直到应用启动执行 DDL 时才以 42P01 之类的错误暴露。
///
/// 因此：所有此类语句一律写成扩展方法，重建迁移时只需调用一次，SQL 本体始终留在代码库中、
/// 可搜索（搜 `ExcludeFromMigrations` 或本类）且与实体配置相邻可见。
///
/// ★ 维护约定：新增任何 ExcludeFromMigrations 实体时，必须在本类里补上对应的建表/删表扩展方法，
///   并在迁移中调用；重建 Initial 迁移后，逐一确认这些调用仍存在。
/// </summary>
public static class MigrationSqlExtensions
{
    /// <summary>
    /// 创建 attribute_audit_log 分区父表 + DEFAULT 分区 + 两个索引。
    /// 对应 TreeGraphDbContext.ConfigureAuditLog 中被 ExcludeFromMigrations 的实体 AttributeAuditLog。
    /// 月度分区由 AuditPartitionMaintenance 在运行时按需创建（CREATE TABLE IF NOT EXISTS ... PARTITION OF）。
    /// </summary>
    public static void CreateAuditPartitionTable(this MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE attribute_audit_log (
                audit_id text NOT NULL DEFAULT gen_random_uuid()::text,
                changed_at timestamp with time zone NOT NULL,
                entity_id text NOT NULL,
                entity_type text NOT NULL,
                attribute_id text NOT NULL,
                attribute_name text NOT NULL,
                old_value text,
                new_value text,
                change_type text NOT NULL,
                changed_by text NOT NULL,
                correlation_id text,
                client_ip text,
                CONSTRAINT pk_attribute_audit_log PRIMARY KEY (audit_id, changed_at)
            ) PARTITION BY RANGE (changed_at);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE attribute_audit_log_default
                PARTITION OF attribute_audit_log DEFAULT;
            """);

        migrationBuilder.Sql("""
            CREATE INDEX ix_audit_entity ON attribute_audit_log
                (entity_type, entity_id, changed_at DESC);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX ix_audit_time ON attribute_audit_log (changed_at);
            """);
    }

    /// <summary>删除 attribute_audit_log 分区父表（CASCADE 连带删除全部子分区）。</summary>
    public static void DropAuditPartitionTable(this MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP TABLE IF EXISTS attribute_audit_log CASCADE;");
}
