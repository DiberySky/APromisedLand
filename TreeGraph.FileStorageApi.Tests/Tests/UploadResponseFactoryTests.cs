using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Uploads;
using TreeGraph.Shared.FileStorageSky.Contracts;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// UploadResponseFactory 纯逻辑单测：三个 FromSession 工厂的映射与守卫。
/// </summary>
public sealed class UploadResponseFactoryTests
{
    private static UploadSessionEntity Session() => new()
    {
        Id          = Guid.NewGuid(),
        Tenant      = "t",
        FileName    = "a.pdf",
        ContentType = "application/pdf",
        TotalSize   = 10,
        ChunkSize   = 4,
        TotalChunks = 3,
        Status      = "pending",
        ExpiresAt   = DateTimeOffset.UtcNow.AddHours(24),
    };

    // ── 维度 1：Initiate 映射 ──

    [Fact]
    public void FromSession_Initiate_MapsAllFields()
    {
        var session = Session();
        session.Fingerprint = "fp";

        var response = UploadResponseFactory.FromSession(session, resumed: true);

        Assert.Equal(session.Id, response.UploadId);
        Assert.Equal(session.ChunkSize, response.ChunkSize);
        Assert.Equal(session.TotalChunks, response.TotalChunks);
        Assert.Equal(session.ExpiresAt, response.ExpiresAt);
        Assert.True(response.Resumed);
    }

    // ── 维度 2：Status 映射（received / missing 计算）──

    [Fact]
    public void FromSession_Status_ComputesMissingChunks()
    {
        var session = Session();   // TotalChunks = 3

        var response = UploadResponseFactory.FromSession(session, [0, 2, 2, 5]);

        // 重复块去重；越界块（5 >= 3）被裁剪
        Assert.Equal([0, 2], response.ReceivedChunks);
        Assert.Equal([1], response.MissingChunks);
        Assert.Equal(2, response.ReceivedCount);
        Assert.Equal(3, response.TotalChunks);
    }

    [Fact]
    public void FromSession_Status_AllReceived_MissingEmpty()
    {
        var session = Session();

        var response = UploadResponseFactory.FromSession(session, [0, 1, 2]);

        Assert.Empty(response.MissingChunks);
        Assert.True(response.IsComplete);
    }

    // ── 维度 3：Complete 映射与守卫 ──

    [Fact]
    public void FromSession_Complete_MapsAllFields()
    {
        var session = Session();
        session.Status   = "completed";
        session.DocId    = "doc-1";
        session.Version  = 2;
        session.ObjectKey = "t/doc-1/v2/a.pdf";
        session.Sha256   = "ABC";

        var response = UploadResponseFactory.FromSession(session);

        Assert.Equal("doc-1", response.DocId);
        Assert.Equal(2, response.Version);
        Assert.Equal("t/doc-1/v2/a.pdf", response.ObjectKey);
        Assert.Equal(10, response.Size);
        Assert.Equal("ABC", response.Sha256);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("uploading")]
    [InlineData("merging")]
    [InlineData("failed")]
    public void FromSession_Complete_NonCompletedStatus_Throws(string status)
    {
        var session = Session();
        session.Status = status;

        Assert.Throws<InvalidOperationException>(
            () => UploadResponseFactory.FromSession(session));
    }

    [Fact]
    public void FromSession_Complete_MissingDocIdOrVersionOrKey_Throws()
    {
        var session = Session();
        session.Status = "completed";
        // DocId / Version / ObjectKey 均为 null

        Assert.Throws<InvalidOperationException>(
            () => UploadResponseFactory.FromSession(session));
    }
}
