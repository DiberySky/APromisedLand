namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 文件存储"库存"查询客户端。
/// 只有一个目的：让 TreeGraph.Api 的 OrphanedFileCleanupService
/// 能拿到 FileStorageApi 当前持有的全部 docId 列表，
/// 与 EAV value_file_meta 中被引用的 docId 做差集对比。
/// </summary>
public interface IFileInventoryClient
{
    /// <summary>获取 FileStorageApi 中所有去重后的 docId。</summary>
    Task<HashSet<string>> GetAllDocIdsAsync(CancellationToken ct = default);
}
