using Microsoft.EntityFrameworkCore;
using TreeGraph.Contracts.Messaging;
using TreeGraph.Persistence.Data;

namespace TreeGraph.Api.MessagingSky;

/// <summary>仅在墓碑仍匹配空间、来源、Tag 与版本时物理删除；重复或乱序命令均为无操作。</summary>
public static class TagProjectionCleanupHandler
{
    public static Task Handle(
        TagProjectionCleanupRequested message, TreeGraphDbContext db, CancellationToken ct)
        => db.TagProjections
            .Where(p => p.SpaceId == message.SpaceId
                     && p.SourceAttributeId == message.SourceAttributeId
                     && p.TagName == message.TagName
                     && p.Version == message.Version
                     && p.IsDeleted)
            .ExecuteDeleteAsync(ct);
}
