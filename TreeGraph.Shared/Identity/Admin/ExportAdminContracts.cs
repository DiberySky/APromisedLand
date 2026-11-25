namespace TreeGraph.Shared.Identity.Admin;

/// <summary>realm partial export 选项。</summary>
public sealed record RealmExportOptions(
    bool ExportGroupsAndRoles = true,
    bool ExportClients = true,
    bool ExportClientsIncludingSecrets = false);

/// <summary>导出结果。partial export 中的 secret 会被掩码，不能作为完整恢复源。</summary>
public sealed record RealmExportResult(string Json, string SuggestedFileName, DateTimeOffset ExportedAt);

/// <summary>realm 导出服务。</summary>
public interface IExportAdminService
{
    Task<AdminResult<RealmExportResult>> ExportAsync(AdminActor actor, RealmExportOptions options, CancellationToken ct = default);
}
