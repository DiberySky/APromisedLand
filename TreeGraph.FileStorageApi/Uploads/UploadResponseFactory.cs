using TreeGraph.FileStorageApi.Entities;
using TreeGraph.Shared.FileStorageSky.Contracts;

namespace TreeGraph.FileStorageApi.Uploads;

/// <summary>
/// 从 UploadSessionEntity 构造响应 DTO 的工厂方法集合。
/// 原本以静态方法形式挂在 DTO 类上（如 InitiateUploadResponse.FromSession），
/// DTO 迁移到 TreeGraph.Shared 后，这些方法依赖 Entity，必须留在 Api 项目。
/// </summary>
public static class UploadResponseFactory
{
    public static InitiateUploadResponse FromSession(
        UploadSessionEntity session, bool resumed = false)
        => new(session.Id, session.ChunkSize, session.TotalChunks,
            session.ExpiresAt, resumed);

    public static CompleteUploadResponse FromSession(UploadSessionEntity session)
    {
        if (session.Status != "completed")
            throw new InvalidOperationException(
                $"会话状态为 '{session.Status}'，无法构造完成响应。");

        if (string.IsNullOrEmpty(session.DocId) ||
            session.Version is null ||
            string.IsNullOrEmpty(session.ObjectKey))
            throw new InvalidOperationException(
                "会话缺少 DocId / Version / ObjectKey，无法构造完成响应。");

        return new CompleteUploadResponse(
            session.Id,
            session.DocId,
            session.Version.Value,
            session.ObjectKey,
            session.TotalSize,
            session.Sha256);
    }

    public static UploadStatusResponse FromSession(
        UploadSessionEntity session,
        IReadOnlyList<int> receivedChunks)
    {
        var received = receivedChunks
            .Where(i => i >= 0 && i < session.TotalChunks)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
        var receivedSet = new HashSet<int>(received);
        var missing = Enumerable.Range(0, session.TotalChunks)
            .Where(i => !receivedSet.Contains(i))
            .ToList();

        return new UploadStatusResponse(
            session.Id, session.Status,
            session.TotalSize, session.ChunkSize,
            session.FileName, session.Fingerprint,
            session.TotalChunks, received.Count,
            received, missing, session.ExpiresAt);
    }
}
