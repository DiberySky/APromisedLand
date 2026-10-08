using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.FileStorageApi.Data;
using TreeGraph.FileStorageApi.Entities;
using TreeGraph.FileStorageApi.Security;
using TreeGraph.FileStorageApi.Storage;
using TreeGraph.FileStorageApi.Tests.Fixtures;
using TreeGraph.Shared.FileStorageSky.Contracts;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests.Integration;

/// <summary>
/// UploadsController HTTP 集成测试：异常 → 状态码映射矩阵
/// （404 / 410 / 409 / 422 / 400）与完整 HTTP 上传流程。
/// </summary>
public sealed class UploadsControllerTests : HttpTestBase
{
    public UploadsControllerTests(FileStorageApiFactory factory) : base(factory) { }

    private const int Chunk = 256 * 1024;

    private async Task<InitiateUploadResponse> InitiateAsync(
        long totalSize, string? fingerprint = null)
    {
        var response = await Client.PostAsJsonAsync("/uploads/initiate", new InitiateUploadRequest
        {
            FileName = "http.bin",
            ContentType = "application/octet-stream",
            TotalSize = totalSize,
            ChunkSize = Chunk,
            Fingerprint = fingerprint,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InitiateUploadResponse>())!;
    }

    private async Task<HttpResponseMessage> PutChunkAsync(
        Guid uploadId, int index, byte[] data, bool withSha = true)
    {
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/uploads/{uploadId}/chunks/{index}")
        {
            Content = content,
        };
        if (withSha)
            request.Headers.Add("X-Chunk-Sha256", Convert.ToHexString(SHA256.HashData(data)));

        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> CompleteAsync(Guid uploadId)
        => await Client.PostAsJsonAsync(
            $"/uploads/{uploadId}/complete", new CompleteUploadRequest());

    private async Task SeedSessionAsync(UploadSessionEntity session)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();
    }

    private static UploadSessionEntity Session(string status, DateTimeOffset expiresAt)
        => new()
        {
            Tenant = "default",
            FileName = "seed.bin",
            ContentType = "application/octet-stream",
            TotalSize = 1000,
            ChunkSize = Chunk,
            TotalChunks = 1,
            Status = status,
            ExpiresAt = expiresAt,
        };

    // ══════════ 维度 1：initiate ══════════

    [Fact]
    public async Task Initiate_TotalSizeOutOfRange_Returns400_WithValidationProblem()
    {
        // TotalSize=0 先被 DataAnnotations Range 拦截（模型验证 → 400）
        var response = await Client.PostAsJsonAsync("/uploads/initiate", new InitiateUploadRequest
        {
            FileName = "bad.bin",
            ContentType = "application/octet-stream",
            TotalSize = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Initiate_FingerprintSizeMismatch_Returns422_WithProblemDetails()
    {
        // 先建一个 3000 字节的会话，再用相同 fingerprint 请求 9999 字节 → 服务层 422
        var first = await InitiateAsync(3000, fingerprint: "fp-http-1");

        var mismatch = await Client.PostAsJsonAsync("/uploads/initiate", new InitiateUploadRequest
        {
            FileName = "bad.bin",
            ContentType = "application/octet-stream",
            TotalSize = 9999,
            ChunkSize = Chunk,
            Fingerprint = "fp-http-1",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);
        var problem = await mismatch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UploadValidationException", problem.GetProperty("title").GetString());
        Assert.NotEqual(first.UploadId, Guid.Empty);
    }

    [Fact]
    public async Task Initiate_Valid_ReturnsSession()
    {
        var response = await Client.PostAsJsonAsync("/uploads/initiate", new InitiateUploadRequest
        {
            FileName = "ok.bin",
            ContentType = "application/octet-stream",
            TotalSize = 1000,
            ChunkSize = Chunk,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InitiateUploadResponse>();
        Assert.NotEqual(Guid.Empty, body!.UploadId);
        Assert.Equal(1, body.TotalChunks);
        Assert.False(body.Resumed);
    }

    // ══════════ 维度 2：状态码映射矩阵 ══════════

    [Fact]
    public async Task GetStatus_UnknownSession_Returns404()
    {
        var response = await Client.GetAsync($"/uploads/{Guid.NewGuid()}/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutChunk_MissingBody_Returns400()
    {
        var session = await InitiateAsync(1000);
        var response = await PutChunkAsync(session.UploadId, 0, Array.Empty<byte>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutChunk_UnknownSession_Returns404()
    {
        var response = await PutChunkAsync(Guid.NewGuid(), 0, new byte[100]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutChunk_WrongSha_Returns422()
    {
        var session = await InitiateAsync(1000);

        // 无 X-Chunk-Sha256 头 → 服务端不校验 → 200
        var noSha = await PutChunkAsync(session.UploadId, 0, new byte[100], withSha: false);
        Assert.Equal(HttpStatusCode.OK, noSha.StatusCode);

        // 带错误 SHA 头 → 422（且该分块不会被写入）
        var bad = new HttpRequestMessage(
            HttpMethod.Put, $"/uploads/{session.UploadId}/chunks/1")
        {
            Content = new ByteArrayContent(new byte[100]),
        };
        bad.Headers.Add("X-Chunk-Sha256", new string('0', 64));
        bad.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var wrongSha = await Client.SendAsync(bad);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongSha.StatusCode);
    }

    [Fact]
    public async Task PutChunk_OnCompletedSession_Returns409()
    {
        var session = await InitiateAsync(1000);
        await PutChunkAsync(session.UploadId, 0, new byte[1000]);
        var completed = await CompleteAsync(session.UploadId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var response = await PutChunkAsync(session.UploadId, 0, new byte[1000]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Complete_UnknownSession_Returns404()
    {
        var response = await CompleteAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Complete_ExpiredSession_Returns410()
    {
        var expired = Session("pending", DateTimeOffset.UtcNow.AddHours(-1));
        await SeedSessionAsync(expired);

        var response = await CompleteAsync(expired.Id);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Complete_MergingSession_Returns409()
    {
        var merging = Session("merging", DateTimeOffset.UtcNow.AddHours(1));
        await SeedSessionAsync(merging);

        var response = await CompleteAsync(merging.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ══════════ 维度 3：heartbeat / cancel 状态码矩阵 ══════════

    [Fact]
    public async Task Heartbeat_UnknownSession_Returns404()
    {
        var response = await Client.PostAsync($"/uploads/{Guid.NewGuid()}/heartbeat", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_ActiveSession_Returns204()
    {
        var session = await InitiateAsync(1000);

        var response = await Client.PostAsync($"/uploads/{session.UploadId}/heartbeat", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_CompletedSession_Returns409()
    {
        var session = await InitiateAsync(1000);
        await PutChunkAsync(session.UploadId, 0, new byte[1000]);
        await CompleteAsync(session.UploadId);

        var response = await Client.PostAsync($"/uploads/{session.UploadId}/heartbeat", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_ExpiredSession_Returns410()
    {
        var expired = Session("pending", DateTimeOffset.UtcNow.AddHours(-1));
        await SeedSessionAsync(expired);

        var response = await Client.PostAsync($"/uploads/{expired.Id}/heartbeat", null);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_UnknownSession_Returns404()
    {
        var response = await Client.DeleteAsync($"/uploads/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ActiveSession_Returns204()
    {
        var session = await InitiateAsync(1000);

        var response = await Client.DeleteAsync($"/uploads/{session.UploadId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_CompletedSession_Returns409()
    {
        var session = await InitiateAsync(1000);
        await PutChunkAsync(session.UploadId, 0, new byte[1000]);
        await CompleteAsync(session.UploadId);

        var response = await Client.DeleteAsync($"/uploads/{session.UploadId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ══════════ 维度 4：完整 HTTP 流程 ══════════

    [Fact]
    public async Task FullHttpFlow_InitiateTwoChunksComplete_Succeeds()
    {
        // TotalSize=300000，ChunkSize=256KB → 2 个逻辑分块（各 150000 字节）
        var payload = new byte[300_000];
        new Random(7).NextBytes(payload);

        var session = await InitiateAsync(payload.Length);
        Assert.Equal(2, session.TotalChunks);

        var chunk1 = await PutChunkAsync(session.UploadId, 0, payload[..150_000]);
        var chunk2 = await PutChunkAsync(session.UploadId, 1, payload[150_000..]);
        Assert.Equal(HttpStatusCode.OK, chunk1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chunk2.StatusCode);
        Assert.Equal("2", (await chunk2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("receivedCount").GetInt32().ToString());

        var status = await Client.GetAsync($"/uploads/{session.UploadId}/status");
        var statusBody = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("uploading", statusBody.GetProperty("status").GetString());

        var completed = await CompleteAsync(session.UploadId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var body = await completed.Content.ReadFromJsonAsync<CompleteUploadResponse>();
        Assert.Equal(300_000, body!.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)), body.Sha256);
        Assert.Equal([body.ObjectKey], Factory.Storage.Keys);
    }

    // ═══════════ 维度 1 续：initiate 边界 ═══════════

    [Fact]
    public async Task Initiate_TotalSizeAboveMax_Returns400_WithValidationProblem()
    {
        // TotalSize = 2GB + 1，超过 InitiateUploadRequest.TotalSize 上的
        // [Range(1, 2GB)] 上限 → 模型验证在 [ApiController] 下先于服务层拦截 → 400。
        // （服务层 FileUploadService 的 MaxTotalSize 检查对应的 422 路径经 HTTP 不可达，
        //   因 Range 上限与 MaxTotalSize 相等。）
        var response = await Client.PostAsJsonAsync("/uploads/initiate", new InitiateUploadRequest
        {
            FileName = "big.bin",
            ContentType = "application/octet-stream",
            TotalSize = 2L * 1024 * 1024 * 1024 + 1,
            ChunkSize = Chunk,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    // ═══════════ 维度 2 续：状态码映射矩阵 ═══════════

    [Fact]
    public async Task PutChunk_ExpiredSession_Returns410()
    {
        var expired = Session("pending", DateTimeOffset.UtcNow.AddHours(-1));
        await SeedSessionAsync(expired);

        var response = await PutChunkAsync(expired.Id, 0, new byte[100]);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task PutChunk_SizeAboveSessionChunk_Returns422()
    {
        // session.ChunkSize = 256KB，PUT 一个 256KB+1 字节的块 → 422
        var session = await InitiateAsync(1000);
        var data = new byte[Chunk + 1];

        var response = await PutChunkAsync(session.UploadId, 0, data);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Complete_ShaMismatch_Returns422()
    {
        var session = await InitiateAsync(1000);
        await PutChunkAsync(session.UploadId, 0, new byte[1000]);

        // Complete 带错误 SHA → 服务层校验失败 → 删对象 + MarkFailed + 422
        var response = await Client.PostAsJsonAsync(
            $"/uploads/{session.UploadId}/complete",
            new CompleteUploadRequest { Sha256 = new string('f', 64) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ═══════════ 维度 4 续：完整 HTTP 流程 ═══════════

    [Fact]
    public async Task Complete_IdempotentReplay_ReturnsSameResponse()
    {
        var session = await InitiateAsync(1000);
        await PutChunkAsync(session.UploadId, 0, new byte[1000]);

        var first = await CompleteAsync(session.UploadId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<CompleteUploadResponse>();

        var second = await CompleteAsync(session.UploadId);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<CompleteUploadResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody!.DocId, secondBody!.DocId);
        Assert.Equal(firstBody.Version, secondBody.Version);
        Assert.Equal(firstBody.ObjectKey, secondBody.ObjectKey);
    }

    // ═══════════ 维度 5：全局异常中间件 ═══════════

    [Fact]
    public async Task OversizedTenantHeader_TriggersInvalidTenantMiddleware_Returns400()
    {
        // 临时切回 HttpCallerContext（带 128 字节上限）；TestCallerContext 不做长度校验，
        // 所以必须用 WithWebHostBuilder 覆盖 ICallerContext 注册。
        var httpFactory = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICallerContext>();
            services.AddScoped<ICallerContext, HttpCallerContext>();
        }));
        var client = httpFactory.CreateClient();

        // 走 POST /uploads/initiate：FileUploadService.InitiateAsync 在构造会话时
        // 直接读取 _caller.Tenant（new UploadSessionEntity { Tenant = _caller.Tenant }），
        // 这是 LINQ 表达式之外的原始属性访问，超长 X-Tenant-Id 会抛出原始
        // InvalidTenantException（不被 EF 包装），中间件捕获 → 400。
        // 注意：GET /files 的 _caller.Tenant 在 LINQ Where 内被求值，会被 EF
        // 包装成 InvalidOperationException，无法命中中间件的 catch 子句。
        var request = new HttpRequestMessage(HttpMethod.Post, "/uploads/initiate")
        {
            Content = JsonContent.Create(new InitiateUploadRequest
            {
                FileName = "ok.bin",
                ContentType = "application/octet-stream",
                TotalSize = 1000,
                ChunkSize = Chunk,
            }),
        };
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", new string('x', 200));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvalidTenant", body.GetProperty("title").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }
}
