namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 文件存储级联删除接口。
///
/// 设计意图：
///   - EavWriteService 在删除或替换 file 类型 EAV 值后，
///     需要通知 FileStorageApi 删除对应的文件对象（防僵尸）。
///   - 此接口隔离基础设施依赖——EavWriteService 不直接持有 HttpClient，
///     符合"接口下沉到 Domain + 实现在 Infrastructure"的依赖方向原则。
///
/// 实现：在 Program.cs 中通过 AddHttpClient 注册的 FileStorageDeleter。
/// </summary>
public interface IFileStorageDeleter
{
    /// <summary>
    /// 按 docId + 可选 version 级联删除文件对象。
    /// version 为 null 时删该 docId 的最新版本。
    /// 文件不存在（已删或从未有）返回 false，不是异常。
    /// </summary>
    Task<bool> DeleteByDocIdAsync(string docId, int? version = null, CancellationToken ct = default);

    /// <summary>
    /// 批量级联删除：每个待删项带 docId + version。
    /// 内部逐个调用 DeleteByDocIdAsync，单个失败只记日志不中断整体流程
    /// （SaveChanges 已成功，DB 回滚不了；补偿由定期清理任务兜底）。
    /// </summary>
    Task DeleteBatchAsync(IEnumerable<(string DocId, int? Version)> items,
        CancellationToken ct = default);
}
