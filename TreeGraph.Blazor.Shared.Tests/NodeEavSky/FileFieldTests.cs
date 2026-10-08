using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.FileStorageSky.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.FieldRenderers;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Shared.FileStorageSky.Contracts;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// FileField.razor 组件单测：验证 EAV file 属性接入 IFileStorageClient 后的
/// 渲染、上传、下载、删除四个核心流程。
///
/// 使用 bUnit + Mock&lt;IFileStorageClient&gt;：JSInterop 设为 Loose，
/// 不触发真实 JS（downloadFromStream 被桩掉）。
/// </summary>
public class FileFieldTests : BunitTestBase
{
    private static readonly JsonSerializerOptions WebOpt =
        new(JsonSerializerDefaults.Web);

    private readonly Mock<IFileStorageClient> _storage = new();

    public FileFieldTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
        Services.AddSingleton(_storage.Object);
    }

    private static AttributeSchemaDto FileAttr() =>
        new("attachment", "附件", EavDataTypes.File,
            false, false, false, 150, null, null, null);

    private static JsonElement BuildValue(string docId, string fileName,
        string contentType, long size, string? sha256, int version, string status) =>
        JsonSerializer.SerializeToElement(new
        {
            docId,
            fileName,
            contentType,
            size,
            sha256,
            version,
            status
        }, WebOpt);

    // ============================================================
    // 1. 渲染：seeder 写入的 7 字段 FileMetadataDto JSON 正确显示
    // ============================================================

    [Fact]
    public void Renders_SeededFileMetadataDto_ShowsAllFields()
    {
        var value = BuildValue("demo-doc-001", "手机使用说明书.pdf",
            "application/pdf", 524288L,
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
            1, "active");

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        var markup = cut.Markup;
        Assert.Contains("手机使用说明书.pdf", markup);
        Assert.Contains("application/pdf", markup);
        Assert.Contains("512.0 KB", markup);
        Assert.Contains("a1b2c3d4e5f60718", markup);
        Assert.Contains("v1", markup);

        _storage.VerifyNoOtherCalls();
    }

    // ============================================================
    // 2. 上传流程：Initiate → UploadChunk → Complete → GetFileByDocId
    //    并写回 7 字段 JSON
    // ============================================================

    [Fact]
    public async Task Upload_Flow_CallsInitiateChunkComplete_AndWritesBack7FieldJson()
    {
        var uploadId = Guid.NewGuid();
        var fileGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(
                uploadId, 1024, 1, DateTimeOffset.Now, false));

        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(
                uploadId, "up-1", 1, "obj", 1024, "sha-1"));

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "up-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "up-1",
                Version = 1,
                FileName = "t.pdf",
                ContentType = "application/pdf",
                Size = 1024,
                Sha256 = "sha-1",
                Status = "active"
            });

        JsonElement? captured = null;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v => captured = v));
        });

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("t.pdf", 1024, "application/pdf");

        await cut.InvokeAsync(async () =>
        {
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile }));
        });

        _storage.Verify(s => s.InitiateUploadAsync(
            It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UploadChunkAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Stream>(),
            It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storage.Verify(s => s.CompleteUploadAsync(
            It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.GetFileByDocIdAsync(
            "up-1", 1, It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(captured);
        var root = captured!.Value;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("up-1",            root.GetProperty("docId").GetString());
        Assert.Equal("t.pdf",           root.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf",  root.GetProperty("contentType").GetString());
        Assert.Equal(1024L,             root.GetProperty("size").GetInt64());
        Assert.Equal("sha-1",           root.GetProperty("sha256").GetString());
        Assert.Equal(1,                 root.GetProperty("version").GetInt32());
        Assert.Equal("active",          root.GetProperty("status").GetString());
    }

    // ============================================================
    // 3. 下载：GetFileByDocId → DownloadFileAsync 两步调用
    // ============================================================

    [Fact]
    public async Task Download_CallsGetFileByDocIdThenDownloadFileAsync()
    {
        var fileGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var value = BuildValue("d1", "report.pdf", "application/pdf",
            100L, "hash1", 1, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync("d1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "d1",
                Version = 1,
                FileName = "report.pdf",
                ContentType = "application/pdf",
                Size = 100,
                Status = "active"
            });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(Encoding.UTF8.GetBytes("payload")));

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        // 删除按钮之前的是下载按钮（markup 中 download 在 delete 之前渲染）
        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count >= 2, "应至少有下载+删除两个按钮");
        await cut.InvokeAsync(() => buttons[0].Click());

        _storage.Verify(s => s.GetFileByDocIdAsync("d1", 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _storage.Verify(s => s.DownloadFileAsync(fileGuid, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================
    // 4. 删除：确认 → DeleteFileAsync → ValueChanged(null)
    // ============================================================

    [Fact]
    public async Task Delete_Confirms_AndClearsValue()
    {
        var fileGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var value = BuildValue("d2", "to-delete.pdf", "application/pdf",
            50L, "hash2", 1, "active");

        var dialogMock = new Mock<IDialogService>();
        dialogMock.Setup(d => d.ShowMessageBoxAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)true);
        Services.AddSingleton<IDialogService>(dialogMock.Object);

        _storage.Setup(s => s.GetFileByDocIdAsync("d2", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "d2",
                Version = 1,
                FileName = "to-delete.pdf",
                ContentType = "application/pdf",
                Size = 50,
                Status = "active"
            });
        _storage.Setup(s => s.DeleteFileAsync(fileGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        JsonElement? captured = null;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v => captured = v));
        });

        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count >= 2, "应至少有下载+删除两个按钮");
        // 删除按钮是第二个（download 在前）
        await cut.InvokeAsync(() => buttons[1].Click());

        _storage.Verify(s => s.DeleteFileAsync(fileGuid, It.IsAny<CancellationToken>()),
            Times.Once);

        // ValueChanged 被调用且参数为 null（清空）
        Assert.Null(captured);

        // 删除后不再显示文件名
        Assert.DoesNotContain("to-delete.pdf", cut.Markup);
    }

    // ============================================================
    // 5. 下载边界：GetFileByDocId 返回 null → 提示“文件不存在”，不调下载
    // ============================================================

    [Fact]
    public async Task Download_WhenMetadataMissing_ShowsFileNotFound_SkipsDownload()
    {
        var value = BuildValue("missing", "ghost.pdf", "application/pdf",
            10L, null, 1, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "missing", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileMetadataDto?)null);

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());

        _storage.Verify(s => s.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("文件不存在", cut.Markup);
    }

    // ============================================================
    // 6. 下载边界：元数据存在但流为 null → 提示“文件不存在”，不触发 JS
    // ============================================================

    [Fact]
    public async Task Download_WhenStreamIsNull_ShowsFileNotFound_SkipsJs()
    {
        var fileGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var value = BuildValue("d3", "empty.pdf", "application/pdf",
            10L, null, 1, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d3", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "d3", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(
                fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());

        Assert.Contains("文件不存在", cut.Markup);
        Assert.DoesNotContain(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.downloadFromStream");
    }

    // ============================================================
    // 7. 下载边界：存储客户端抛异常 → 显示“下载失败”，不继续下载
    // ============================================================

    [Fact]
    public async Task Download_WhenStorageThrows_ShowsDownloadError()
    {
        var value = BuildValue("d4", "boom.pdf", "application/pdf",
            10L, null, 1, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d4", 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());

        Assert.Contains("下载失败", cut.Markup);
        Assert.Contains("network down", cut.Markup);
        _storage.Verify(s => s.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 8. ★ 回归：DownloadFileAsync 的 rangeStart/rangeEnd 必须为 null，
    //    CancellationToken 只能走第 4 个参数（修复前曾误把 ct 传给 rangeStart）；
    //    并验证 JS 互操作参数 = (fileName, DotNetStreamReference)
    // ============================================================

    [Fact]
    public async Task Download_PassesNullRanges_AndInvokesJsWithStreamReference()
    {
        var fileGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var value = BuildValue("d5", "data.bin", "application/octet-stream",
            7L, null, 3, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d5", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "d5",
                Version = 3,
                FileName = "data.bin",
                ContentType = "application/octet-stream",
                Size = 7,
                Status = "active"
            });
        _storage.Setup(s => s.DownloadFileAsync(
                fileGuid,
                It.Is<long?>(v => v == null),
                It.Is<long?>(v => v == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(Encoding.UTF8.GetBytes("payload")));

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());

        _storage.Verify(s => s.DownloadFileAsync(
                fileGuid,
                It.Is<long?>(v => v == null),
                It.Is<long?>(v => v == null),
                It.IsAny<CancellationToken>()), Times.Once);

        var invocations = JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.downloadFromStream")
            .ToList();
        Assert.Single(invocations);
        Assert.Equal("data.bin", invocations[0].Arguments[0] as string);
        Assert.IsType<DotNetStreamReference>(invocations[0].Arguments[1]);
    }

    // ============================================================
    // 9. 删除边界：用户在确认框点取消 → 不查元数据、不删二进制、不清空
    // ============================================================

    [Fact]
    public async Task Delete_WhenUserCancels_DoesNothing()
    {
        var value = BuildValue("d6", "keep.pdf", "application/pdf",
            10L, null, 1, "active");

        var dialogMock = new Mock<IDialogService>();
        dialogMock.Setup(d => d.ShowMessageBoxAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)false);
        Services.AddSingleton<IDialogService>(dialogMock.Object);

        var changed = false;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, _ => changed = true));
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[1].Click());

        _storage.Verify(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _storage.Verify(s => s.DeleteFileAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(changed);
        Assert.Contains("keep.pdf", cut.Markup);
    }

    // ============================================================
    // 10. 删除边界：确认后元数据查不到（二进制可能已被外部删除）
    //     → 不调 DeleteFileAsync，但仍清空本地 EAV 值
    // ============================================================

    [Fact]
    public async Task Delete_WhenMetadataMissing_StillClearsValueWithoutBinaryDelete()
    {
        var value = BuildValue("d7", "orphan.pdf", "application/pdf",
            10L, null, 1, "active");

        var dialogMock = new Mock<IDialogService>();
        dialogMock.Setup(d => d.ShowMessageBoxAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)true);
        Services.AddSingleton<IDialogService>(dialogMock.Object);

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d7", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileMetadataDto?)null);

        JsonElement? captured = null;
        var changed = false;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v =>
                {
                    changed = true;
                    captured = v;
                }));
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[1].Click());

        _storage.Verify(s => s.DeleteFileAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(changed, "ValueChanged 应被触发以清空 EAV 值");
        Assert.Null(captured);
        Assert.DoesNotContain("orphan.pdf", cut.Markup);
    }

    // ============================================================
    // 11. 上传：< 256KB 的小文件 ChunkSize 传 null（服务端默认），
    //     fingerprint = name:size
    // ============================================================

    [Fact]
    public async Task Upload_SmallFile_RequestsNullChunkSize()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "small-doc", 1, "obj", 1024, "s"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "small-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                DocId = "small-doc",
                Version = 1,
                FileName = "small.bin",
                ContentType = "application/octet-stream",
                Size = 1024,
                Sha256 = "s",
                Status = "active"
            });

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("small.bin", 1024, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        _storage.Verify(s => s.InitiateUploadAsync(
            It.Is<InitiateUploadRequest>(r =>
                r.ChunkSize == null
                && r.TotalSize == 1024
                && r.Fingerprint == "small.bin:1024"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 12. 上传：1MB（>=256KB 且 <8MB）文件 ChunkSize = 自身大小
    //     （Math.Min(8MB, file.Size) 分支）
    // ============================================================

    [Fact]
    public async Task Upload_LargeFile_RequestsFileSizeAsChunkSize()
    {
        var uploadId = Guid.NewGuid();
        const long oneMb = 1024L * 1024;

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(
                uploadId, (int)oneMb, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "big-doc", 1, "obj", oneMb, "b"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "big-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                DocId = "big-doc",
                Version = 1,
                FileName = "big.bin",
                ContentType = "application/octet-stream",
                Size = oneMb,
                Sha256 = "b",
                Status = "active"
            });

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("big.bin", oneMb, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        _storage.Verify(s => s.InitiateUploadAsync(
            It.Is<InitiateUploadRequest>(r => r.ChunkSize == (int)oneMb),
            It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), oneMb,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 13. 上传续传：Resumed=true 时先查状态，从服务端 ReceivedCount
    //     对应的分块索引开始补传（已收的 index 0 不再上传）
    // ============================================================

    [Fact]
    public async Task Upload_Resumed_StartsFromServerReceivedCount()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 2, DateTimeOffset.Now, true));
        _storage.Setup(s => s.GetUploadStatusAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStatusResponse
            {
                UploadId = uploadId,
                Status = "active",
                TotalSize = 2048,
                ChunkSize = 1024,
                FileName = "r.bin",
                TotalChunks = 2,
                ReceivedCount = 1,
                ReceivedChunks = new[] { 0 },
                MissingChunks = new[] { 1 }
            });
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 1, 2, 2));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "resumed-doc", 1, "obj", 2048, "rs"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "resumed-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                DocId = "resumed-doc",
                Version = 1,
                FileName = "r.bin",
                ContentType = "application/octet-stream",
                Size = 2048,
                Sha256 = "rs",
                Status = "active"
            });

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("r.bin", 2048, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        _storage.Verify(s => s.GetUploadStatusAsync(
                uploadId, It.IsAny<CancellationToken>()), Times.Once);
        // 只补传缺失的 index 1；index 0 已被服务端接收，不再上传
        _storage.Verify(s => s.UploadChunkAsync(
                uploadId, 1, It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 14. 上传：Complete 后 GetFileByDocId 查不到权威元数据时，
    //     回退用 CompleteUploadResponse + 请求字段拼装 7 字段 JSON
    // ============================================================

    [Fact]
    public async Task Upload_WhenMetadataLookupReturnsNull_FallsBackToCompleteResponse()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "fb-doc", 2, "obj", 1024, "fb-sha"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "fb-doc", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileMetadataDto?)null);

        JsonElement? captured = null;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v => captured = v));
        });

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("fallback.doc", 1024, "application/pdf");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        Assert.NotNull(captured);
        var root = captured!.Value;
        Assert.Equal("fb-doc",         root.GetProperty("docId").GetString());
        Assert.Equal("fallback.doc",   root.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", root.GetProperty("contentType").GetString());
        Assert.Equal(1024L,           root.GetProperty("size").GetInt64());
        Assert.Equal("fb-sha",        root.GetProperty("sha256").GetString());
        Assert.Equal(2,               root.GetProperty("version").GetInt32());
        Assert.Equal("active",        root.GetProperty("status").GetString());
    }

    // ============================================================
    // 15. 上传：Initiate 抛异常 → 显示“上传失败”，后续分块/完成均不调用
    // ============================================================

    [Fact]
    public async Task Upload_WhenInitiateThrows_ShowsErrorAndAborts()
    {
        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("init boom"));

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("x.bin", 10, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        Assert.Contains("上传失败", cut.Markup);
        Assert.Contains("init boom", cut.Markup);
        _storage.Verify(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ============================================================
    // 16. 渲染边界：Value 为 null，或 JSON 是对象但缺 docId/ docId 空白
    //     → 显示上传入口（InputFile），不渲染文件卡与下载/删除按钮
    // ============================================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Renders_NullOrDocIdLessValue_ShowsUploadEntry(bool valueIsNull)
    {
        JsonElement? value = valueIsNull
            ? null
            : JsonSerializer.SerializeToElement(
                new { fileName = "no-doc.pdf", size = 1L }, WebOpt);

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        Assert.Contains("上传附件", cut.Markup);
        Assert.Empty(cut.FindAll("button"));
        Assert.NotNull(cut.FindComponent<InputFile>().Instance);
    }

    // ============================================================
    // 17. FormatSize 三档：B / KB / MB（通过文件卡文案间接验证私有格式化）
    // ============================================================

    [Theory]
    [InlineData(512L,            "512 B")]
    [InlineData(1536L,           "1.5 KB")]
    [InlineData(2L * 1024 * 1024, "2.0 MB")]
    public void Renders_SizeInCorrectUnit(long size, string expected)
    {
        var value = BuildValue("size-doc", "f.bin", "application/octet-stream",
            size, null, 1, "active");

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        Assert.Contains(expected, cut.Markup);
    }

    // ============================================================
    // 18. 部分字段缺失：size→0、version→1、sha256→不显示 SHA 行
    //     （docId 存在即视为有效文件元数据）
    // ============================================================

    [Fact]
    public void Renders_PartialMetadata_FillsDefaultsAndHidesSha()
    {
        var value = JsonSerializer.SerializeToElement(
            new { docId = "partial-doc", fileName = "partial.bin" }, WebOpt);

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        var markup = cut.Markup;
        Assert.Contains("partial.bin", markup);
        Assert.Contains("0 B", markup);      // size 缺失 → 0
        Assert.Contains("v1", markup);       // version 缺失 → 1
        Assert.DoesNotContain("SHA256", markup); // sha256 缺失 → 整行不渲染
    }

    // ============================================================
    // 19. SHA256 长度 ≤16 时不截断、不追加省略号
    // ============================================================

    [Fact]
    public void Renders_ShortSha256_NotTruncated()
    {
        var value = BuildValue("short-sha", "f.bin", "application/octet-stream",
            1L, "abc12345", 1, "active");

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        Assert.Contains("abc12345", cut.Markup);
        Assert.DoesNotContain("…", cut.Markup);
    }

    // ============================================================
    // 20. 必填属性（IsRequired=true）在空值态显示“必填”标记
    // ============================================================

    [Fact]
    public void Renders_RequiredAttribute_ShowsRequiredMarker()
    {
        var requiredAttr = new AttributeSchemaDto(
            "attachment", "附件", EavDataTypes.File,
            true, false, false, 150, null, null, null);

        var cut = Render<FileField>(p => p.Add(x => x.Attr, requiredAttr));

        Assert.Contains("必填", cut.Markup);
        Assert.Contains("上传附件", cut.Markup);
    }

    // ============================================================
    // 21. 删除边界：DeleteFileAsync 抛异常 → 显示“删除失败”，
    //     文件卡保留、ValueChanged 不触发（不写回 null）
    // ============================================================

    [Fact]
    public async Task Delete_WhenStorageThrows_ShowsErrorAndKeepsValue()
    {
        var fileGuid = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var value = BuildValue("d8", "locked.pdf", "application/pdf",
            10L, null, 1, "active");

        var dialogMock = new Mock<IDialogService>();
        dialogMock.Setup(d => d.ShowMessageBoxAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)true);
        Services.AddSingleton<IDialogService>(dialogMock.Object);

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d8", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "d8",
                Version = 1,
                FileName = "locked.pdf",
                ContentType = "application/pdf",
                Size = 10,
                Status = "active"
            });
        _storage.Setup(s => s.DeleteFileAsync(
                fileGuid, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("object lock"));

        var changed = false;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, _ => changed = true));
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[1].Click());

        Assert.Contains("删除失败", cut.Markup);
        Assert.Contains("object lock", cut.Markup);
        Assert.False(changed, "删除失败时不应清空 EAV 值");
        Assert.Contains("locked.pdf", cut.Markup);
    }

    // ============================================================
    // 22. 上传：浏览器未给出 ContentType（空字符串）时
    //     Initiate 请求兜底为 application/octet-stream
    // ============================================================

    [Fact]
    public async Task Upload_WithBlankContentType_DefaultsToOctetStream()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "ct-doc", 1, "obj", 10, "c"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "ct-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                DocId = "ct-doc",
                Version = 1,
                FileName = "noct.bin",
                ContentType = "application/octet-stream",
                Size = 10,
                Sha256 = "c",
                Status = "active"
            });

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("noct.bin", 10, "");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        _storage.Verify(s => s.InitiateUploadAsync(
            It.Is<InitiateUploadRequest>(r =>
                r.ContentType == "application/octet-stream"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 23. 上传成功后 UI 从“上传入口”切换为“文件卡”：
    //     InputFile 消失、下载/删除按钮出现、文件名展示
    // ============================================================

    [Fact]
    public async Task Upload_Success_SwitchesUiFromEntryToFileCard()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse(uploadId, "ui-doc", 1, "obj", 1024, "u"));
        _storage.Setup(s => s.GetFileByDocIdAsync(
                "ui-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(),
                DocId = "ui-doc",
                Version = 1,
                FileName = "done.bin",
                ContentType = "application/octet-stream",
                Size = 1024,
                Sha256 = "u",
                Status = "active"
            });

        var cut = Render<FileField>(p => p.Add(x => x.Attr, FileAttr()));

        // 初始空值态：有 InputFile，无按钮
        Assert.True(cut.HasComponent<InputFile>());
        Assert.Empty(cut.FindAll("button"));

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("done.bin", 1024, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        // 完成态：上传入口消失，文件卡 + 下载/删除按钮 + 文件名
        Assert.False(cut.HasComponent<InputFile>());
        Assert.Equal(2, cut.FindAll("button").Count);
        Assert.Contains("done.bin", cut.Markup);
        Assert.DoesNotContain("上传附件", cut.Markup);
    }

    // ============================================================
    // 24. 下载边界：存储侧成功，但 JS 互操作失败（如宿主漏引 fileDownload.js
    //     导致 treegraphFile.downloadFromStream 未定义）→ 显示“下载失败”
    // ============================================================

    [Fact]
    public async Task Download_WhenJsInteropThrows_ShowsDownloadError()
    {
        var fileGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var value = BuildValue("d9", "js-fail.pdf", "application/pdf",
            10L, null, 1, "active");

        _storage.Setup(s => s.GetFileByDocIdAsync(
                "d9", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid,
                DocId = "d9",
                Version = 1,
                FileName = "js-fail.pdf",
                ContentType = "application/pdf",
                Size = 10,
                Status = "active"
            });
        _storage.Setup(s => s.DownloadFileAsync(
                fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(Encoding.UTF8.GetBytes("payload")));

        // 模拟浏览器端函数缺失/执行失败（matcher 匹配任意参数调用）
        JSInterop.SetupVoid("treegraphFile.downloadFromStream", _ => true)
            .SetException(new JSException("treegraphFile.downloadFromStream is not defined"));

        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.Value, value);
        });

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());

        Assert.Contains("下载失败", cut.Markup);
        Assert.Contains("downloadFromStream", cut.Markup);
    }

    // ============================================================
    // 25. 上传：分块传输中途抛异常 → 显示“上传失败”，
    //     Complete/GetFileByDocId 不再调用，ValueChanged 不写回
    // ============================================================

    [Fact]
    public async Task Upload_WhenChunkUploadThrows_AbortsWithErrorAndNoCommit()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("chunk connection lost"));

        JsonElement? captured = null;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v => captured = v));
        });

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("mid.bin", 1024, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        Assert.Contains("上传失败", cut.Markup);
        Assert.Contains("chunk connection lost", cut.Markup);
        _storage.Verify(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(captured);
        // 失败后回到上传入口态，可重新选择文件
        Assert.True(cut.HasComponent<InputFile>());
    }

    // ============================================================
    // 26. 上传：Complete 合并阶段抛异常 → 显示“上传失败”，
    //     不再查询元数据、不写回 7 字段 JSON
    // ============================================================

    [Fact]
    public async Task Upload_WhenCompleteThrows_ShowsErrorAndDoesNotCommit()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse(uploadId, 1024, 1, DateTimeOffset.Now, false));
        _storage.Setup(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));
        _storage.Setup(s => s.CompleteUploadAsync(
                It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("merge failed"));

        JsonElement? captured = null;
        var cut = Render<FileField>(p =>
        {
            p.Add(x => x.Attr, FileAttr());
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<JsonElement?>(this, v => captured = v));
        });

        var input = cut.FindComponent<InputFile>();
        var fakeFile = new FakeBrowserFile("merge.bin", 1024, "application/octet-stream");
        await cut.InvokeAsync(async () =>
            await input.Instance.OnChange.InvokeAsync(
                new InputFileChangeEventArgs(new IBrowserFile[] { fakeFile })));

        Assert.Contains("上传失败", cut.Markup);
        Assert.Contains("merge failed", cut.Markup);
        _storage.Verify(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(captured);
    }

    // ============================================================
    // 27. ★ RCL 静态资产契约：fileDownload.js 必须被打包为
    //     _content/TreeGraph.Blazor.Shared/js/fileDownload.js，
    //     且暴露的全局函数名与 FileField.razor 的 JS 互操作 identifier 一致。
    //     防止“文件被误删/改名/函数改名后宿主引用失效”一类回归。
    // ============================================================

    [Fact]
    public void FileDownloadJs_IsPackagedAsStaticAsset_AndExposesExpectedApi()
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "TreeGraph.Blazor.Shared.staticwebassets.runtime.json");
        Assert.True(File.Exists(manifestPath),
            $"缺少 RCL 静态资产 manifest: {manifestPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var contentRoots = doc.RootElement.GetProperty("ContentRoots");
        var asset = FindAssetBySubPath(doc.RootElement.GetProperty("Root"), "js/fileDownload.js");
        Assert.True(asset.HasValue, "manifest 中未注册 js/fileDownload.js 静态资产");

        var rootIndex = asset.Value.GetProperty("ContentRootIndex").GetInt32();
        var subPath = asset.Value.GetProperty("SubPath").GetString();
        var physicalPath = Path.Combine(contentRoots[rootIndex].GetString()!, subPath!);

        Assert.True(File.Exists(physicalPath),
            $"manifest 声明的资产物理文件不存在: {physicalPath}");

        var js = File.ReadAllText(physicalPath);
        // 精确到挂载赋值点（带边界）：函数重命名为 downloadFromStreamX 之类会被抓住，
        // 该名称必须与 FileField.razor 的 JS 互操作 identifier 完全一致。
        Assert.Contains("treegraphFile.downloadFromStream =", js);
        // 必须消费 DotNetStreamReference 的 arrayBuffer()，否则下载得到空内容
        Assert.Contains("arrayBuffer", js);
    }

    // ============================================================
    // 辅助：最小可工作的 IBrowserFile 假实现
    // ============================================================

    private sealed class FakeBrowserFile : IBrowserFile
    {
        private readonly byte[] _data;

        public FakeBrowserFile(string name, long size, string contentType)
        {
            Name = name;
            Size = size;
            ContentType = contentType;
            _data = new byte[size];
        }

        public string Name { get; }
        public long Size { get; }
        public string ContentType { get; }
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public Stream OpenReadStream(
            long maxAllowedSize = 512000,
            CancellationToken cancellationToken = default)
            => new MemoryStream(_data);
    }

    /// <summary>在 staticwebassets.runtime.json 的 Root 树中按 SubPath 递归查找资产节点。</summary>
    private static JsonElement? FindAssetBySubPath(JsonElement node, string subPath)
    {
        if (node.ValueKind != JsonValueKind.Object) return null;

        if (node.TryGetProperty("Asset", out var asset)
            && asset.ValueKind == JsonValueKind.Object
            && asset.TryGetProperty("SubPath", out var sp)
            && string.Equals(sp.GetString(), subPath, StringComparison.OrdinalIgnoreCase))
        {
            return asset;
        }

        if (node.TryGetProperty("Children", out var children)
            && children.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in children.EnumerateObject())
            {
                var hit = FindAssetBySubPath(child.Value, subPath);
                if (hit.HasValue) return hit;
            }
        }

        return null;
    }
}
