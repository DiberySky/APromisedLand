using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.FileStorageApi.Data;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Security;
using TreeGraph.FileStorageApi.Storage;
using TreeGraph.FileStorageApi.Tests.Fixtures;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Integration;

/// <summary>
/// FilesController HTTP 集成测试：Download 的 Range 语义矩阵
/// （200 全量 / 206 区间 / 416 越界 / 多区间回退全量）、
/// 查询端点与 404 语义。测试对象租户为 HTTP 默认租户 "default"。
/// </summary>
public sealed class FilesControllerTests : HttpTestBase
{
    public FilesControllerTests(FileStorageApiFactory factory) : base(factory) { }

    private static readonly byte[] Payload =
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private async Task<DocumentMetadataEntity> SeedAsync(
        string docId, int version = 1, string status = "active",
        byte[]? bytes = null)
    {
        var meta = new DocumentMetadataEntity
        {
            DocId = docId,
            Tenant = "default",
            Version = version,
            FileName = $"{docId}-v{version}.txt",
            ContentType = "text/plain",
            Size = (bytes ?? Payload).Length,
            ObjectKey = $"default/{docId}/v{version}/f.txt",
            Status = status,
        };
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        db.DocumentMetadata.Add(meta);
        await db.SaveChangesAsync();

        if (bytes is not null)
            await Storage.PutAsync(
                meta.ObjectKey, new MemoryStream(bytes), bytes.Length, "text/plain");

        return meta;
    }

    private async Task<HttpResponseMessage> DownloadAsync(Guid id, string? range = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/files/{id}/download");
        if (range is not null)
            request.Headers.TryAddWithoutValidation("Range", range);
        return await Client.SendAsync(request);
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var general) ? general.FirstOrDefault()
         : response.Content.Headers.TryGetValues(name, out var content) ? content.FirstOrDefault()
         : null;

    // ══════════ 维度 1：全量下载 ══════════

    [Fact]
    public async Task Download_Full_Returns200_WithDisposition()
    {
        var meta = await SeedAsync("doc-full", bytes: Payload);

        var response = await DownloadAsync(meta.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Payload.Length, response.Content.Headers.ContentLength);

        var disposition = Header(response, "Content-Disposition");
        Assert.NotNull(disposition);
        Assert.Contains("attachment", disposition);
        Assert.Contains("doc-full-v1.txt", disposition);

        Assert.Equal(Payload, await response.Content.ReadAsByteArrayAsync());
    }

    // ══════════ 维度 2：206 区间 ══════════

    [Fact]
    public async Task Download_PrefixRange_Returns206()
    {
        var meta = await SeedAsync("doc-range1", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=0-4");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(5, response.Content.Headers.ContentLength);
        Assert.Equal("bytes 0-4/10", Header(response, "Content-Range"));
        Assert.Equal([1, 2, 3, 4, 5], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Download_OpenEndedRange_Returns206()
    {
        var meta = await SeedAsync("doc-range2", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=5-");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 5-9/10", Header(response, "Content-Range"));
        Assert.Equal([6, 7, 8, 9, 10], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Download_SuffixRange_Returns206()
    {
        var meta = await SeedAsync("doc-range3", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=-3");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 7-9/10", Header(response, "Content-Range"));
        Assert.Equal([8, 9, 10], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Download_EndBeyondSize_Clamped()
    {
        var meta = await SeedAsync("doc-range4", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=8-100");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 8-9/10", Header(response, "Content-Range"));
        Assert.Equal([9, 10], await response.Content.ReadAsByteArrayAsync());
    }

    // ══════════ 维度 3：416 Range 不可满足 ══════════

    [Fact]
    public async Task Download_StartBeyondSize_Returns416()
    {
        var meta = await SeedAsync("doc-416a", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=10-12");

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal("bytes */10", Header(response, "Content-Range"));
    }

    [Fact]
    public async Task Download_EndBeforeStart_Returns416()
    {
        var meta = await SeedAsync("doc-416b", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=5-2");

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
    }

    // ══════════ 维度 4：多区间回退全量 ══════════

    [Fact]
    public async Task Download_MultiRangeSpec_FallsBackToFull200()
    {
        var meta = await SeedAsync("doc-multi", bytes: Payload);

        var response = await DownloadAsync(meta.Id, range: "bytes=0-1,3-4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Payload, await response.Content.ReadAsByteArrayAsync());
    }

    // ══════════ 维度 5：404 语义 ══════════

    [Fact]
    public async Task Download_UnknownId_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await DownloadAsync(Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Download_ActiveButObjectMissing_Returns404()
    {
        var meta = await SeedAsync("doc-noobject");   // 未写入对象

        Assert.Equal(HttpStatusCode.NotFound,
            (await DownloadAsync(meta.Id)).StatusCode);
    }

    [Fact]
    public async Task Download_NonActiveStatus_Returns404()
    {
        var meta = await SeedAsync("doc-pending", status: "pending_upload", bytes: Payload);

        Assert.Equal(HttpStatusCode.NotFound,
            (await DownloadAsync(meta.Id)).StatusCode);
    }

    // ══════════ 维度 6：查询端点 ══════════

    [Fact]
    public async Task GetById_Unknown_Returns404_Known_ReturnsDto()
    {
        var meta = await SeedAsync("doc-get", bytes: Payload);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/files/{Guid.NewGuid()}")).StatusCode);

        var found = await Client.GetAsync($"/files/{meta.Id}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await found.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("doc-get", body.GetProperty("docId").GetString());
    }

    [Fact]
    public async Task GetByDocId_LatestVersion_OrSpecific()
    {
        var v1 = await SeedAsync("doc-versions", version: 1, bytes: Payload);
        var v2 = await SeedAsync("doc-versions", version: 2, bytes: Payload);

        var latest = await Client.GetAsync("/files/by-doc/doc-versions");
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.Equal("doc-versions-v2.txt",
            (await latest.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("fileName").GetString());

        var specific = await Client.GetAsync("/files/by-doc/doc-versions?version=1");
        Assert.Equal(HttpStatusCode.OK, specific.StatusCode);
        Assert.Equal(v1.Id,
            (await specific.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync("/files/by-doc/doc-versions?version=99")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync("/files/by-doc/missing")).StatusCode);
    }

    // ═══════════ 维度 7：List 端点 ═══════════

    [Fact]
    public async Task List_ReturnsActiveMetadata_OrdersByCreatedDesc()
    {
        var now = DateTimeOffset.UtcNow;
        var metaA = MetaWithCreatedAt("doc-a", 1, "active", now - TimeSpan.FromMinutes(10));
        var metaB = MetaWithCreatedAt("doc-b", 1, "active", now - TimeSpan.FromMinutes(1));
        var metaC = MetaWithCreatedAt("doc-c", 1, "deleted", now - TimeSpan.FromMinutes(2));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "DocumentMetadata";""");
        db.DocumentMetadata.AddRange(metaA, metaB, metaC);
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("doc-b", items[0].GetProperty("docId").GetString());
        Assert.Equal("doc-a", items[1].GetProperty("docId").GetString());
    }

    private static DocumentMetadataEntity MetaWithCreatedAt(
        string docId, int version, string status,
        DateTimeOffset createdAt, byte[]? bytes = null)
    {
        var b = bytes ?? Payload;
        return new DocumentMetadataEntity
        {
            DocId = docId,
            Tenant = "default",
            Version = version,
            FileName = $"{docId}-v{version}.txt",
            ContentType = "text/plain",
            Size = b.Length,
            ObjectKey = $"default/{docId}/v{version}/f.txt",
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    // ═══════════ 维度 8：Delete 端点 ═══════════

    [Fact]
    public async Task Delete_ExistingActive_Returns204_AndMarksDeleted()
    {
        var meta = await SeedAsync("doc-del", bytes: Payload);

        var response = await Client.DeleteAsync($"/files/{meta.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        var status = await db.DocumentMetadata.AsNoTracking()
            .Where(m => m.Id == meta.Id)
            .Select(m => m.Status)
            .SingleAsync();
        Assert.Equal("deleted", status);
        Assert.Null(Storage.GetBytes(meta.ObjectKey));
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await Client.DeleteAsync($"/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════ 维度 3 续：零字节 suffix range 416 ═══════════

    [Fact]
    public async Task Download_SuffixRange_OnZeroSizeMetadata_Returns416()
    {
        var meta = await SeedAsync("doc-zero", bytes: Array.Empty<byte>());

        var response = await DownloadAsync(meta.Id, range: "bytes=-3");

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal("bytes */0", Header(response, "Content-Range"));
    }

    // ═══════════ 维度 9：全局异常中间件回归 ═══════════

    [Fact]
    public async Task List_WithOversizedTenantHeader_Returns400_Not500()
    {
        // 回归：FileMetadataService.ListAsync 把 _caller.Tenant 提到 LINQ 外求值后，
        // 超长 X-Tenant-Id 抛 InvalidTenantException → 全局中间件捕获 → 400。
        // 修复前：Tenant 在 Where lambda 内被 EF ExpressionTreeFuncletizer
        // 包装成 InvalidOperationException，绕过中间件 → 500。
        var httpFactory = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICallerContext>();
            services.AddScoped<ICallerContext, HttpCallerContext>();
        }));
        var client = httpFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/files");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", new string('x', 200));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidTenant", body.GetProperty("title").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }
}
