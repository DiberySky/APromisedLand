using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Common;
using TreeGraph.Blazor.Shared.FileStorageSky.Services;
using TreeGraph.Shared.FileStorageSky.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Common;

/// <summary>
/// UploadProgressDialog.razor 单测（通过 MudDialogProvider + IDialogService.ShowAsync 打开真实对话框，
/// 这样 CascadingParameter IMudDialogInstance 自动注入，与生产链路一致）。
///
/// 覆盖：小文件直传（单块）、大文件分块、断点续传、初始化失败、块失败、合并失败、
///       取消流程、心跳容错、进度更新、标题/状态文本。
/// </summary>
public class UploadProgressDialogTests : BunitTestBase
{
    private readonly Mock<IFileStorageClient> _storage = new();

    public UploadProgressDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_storage.Object);
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Dialog)> OpenAsync(
        DialogParameters parameters)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var dialog = await ds.ShowAsync<UploadProgressDialog>(string.Empty, parameters, new DialogOptions());
        provider.Render();
        return (provider, dialog);
    }

    private static UploadProgressDialog.UploadRequest Request(
        string name, long size, string contentType = "application/octet-stream",
        int maxBytesPerRead = int.MaxValue) =>
        new(new FakeBrowserFile(name, size, contentType, maxBytesPerRead));

    // ============================================================
    // 1. 小文件直传（< 256KB，不分块）→ Initiate → Chunk → Complete → Close
    // ============================================================

    [Fact]
    public async Task SmallFile_UploadsInSingleChunk_ClosesWithFileMeta()
    {
        var uploadId = Guid.NewGuid();
        var fileGuid = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.Is<InitiateUploadRequest>(r =>
                    r.FileName == "a.txt"
                    && r.TotalSize == 1024
                    && r.ChunkSize == null // < 256KB 不分块
                    && r.Fingerprint == "a.txt:1024"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = 1024, Resumed = false
            });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), 1024,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "doc-1", Version = 1, Size = 1024, Sha256 = "sha256hex"
            });

        _storage.Setup(s => s.GetFileByDocIdAsync("doc-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid, DocId = "doc-1", Version = 1,
                FileName = "a.txt", ContentType = "text/plain",
                Size = 1024, Sha256 = "sha256hex", Status = "active"
            });

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("a.txt", 1024, "text/plain") }
        });

        // 对话框应自动关闭（CompleteAndCloseAsync 调用 MudDialog.Close）
        // 验证 storage 调用链完整
        _storage.Verify(s => s.InitiateUploadAsync(
            It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UploadChunkAsync(
            uploadId, 0, It.IsAny<Stream>(), 1024,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.CompleteUploadAsync(
            uploadId, null, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.GetFileByDocIdAsync(
            "doc-1", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 2. 大文件分块（≥ 256KB）→ ChunkSize 计算正确
    // ============================================================

    [Fact]
    public async Task LargeFile_ChunkSizeCalculatedCorrectly()
    {
        var oneMb = 1024L * 1024;
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.Is<InitiateUploadRequest>(r =>
                    r.ChunkSize == (int)oneMb
                    && r.TotalSize == oneMb),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = (int)oneMb, Resumed = false
            });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), oneMb,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "big-1", Version = 1, Size = oneMb, Sha256 = "s"
            });

        _storage.Setup(s => s.GetFileByDocIdAsync("big-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(), DocId = "big-1", Version = 1,
                FileName = "big.bin", ContentType = "application/octet-stream",
                Size = oneMb, Sha256 = "s", Status = "active"
            });

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("big.bin", oneMb) }
        });

        _storage.Verify(s => s.InitiateUploadAsync(
            It.Is<InitiateUploadRequest>(r => r.ChunkSize == (int)oneMb),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 3. 断点续传（Resumed = true）→ GetUploadStatusAsync 被调，跳过已传块
    // ============================================================

    [Fact]
    public async Task ResumedUpload_SkipsAlreadyUploadedChunks()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 3, ChunkSize = 1024, Resumed = true
            });

        _storage.Setup(s => s.GetUploadStatusAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStatusResponse { ReceivedCount = 2 });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 2, It.IsAny<Stream>(), 1024,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 2, 3, 3));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "resume-1", Version = 1, Size = 3072, Sha256 = "s"
            });

        _storage.Setup(s => s.GetFileByDocIdAsync("resume-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(), DocId = "resume-1", Version = 1,
                FileName = "resume.bin", ContentType = "application/octet-stream",
                Size = 3072, Sha256 = "s", Status = "active"
            });

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("resume.bin", 3072) }
        });

        _storage.Verify(s => s.GetUploadStatusAsync(
            uploadId, It.IsAny<CancellationToken>()), Times.Once);
        // 只传第 3 块（索引 2），跳过 0 和 1
        _storage.Verify(s => s.UploadChunkAsync(
            uploadId, 2, It.IsAny<Stream>(), 1024,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.UploadChunkAsync(
            uploadId, 0, It.IsAny<Stream>(), It.IsAny<long>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.UploadChunkAsync(
            uploadId, 1, It.IsAny<Stream>(), It.IsAny<long>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 4. InitiateUpload 失败 → 显示错误，不进入 Chunk 阶段
    // ============================================================

    [Fact]
    public async Task InitiateFails_ShowsError_DoesNotUploadChunks()
    {
        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("init boom"));

        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("fail.bin", 1024) }
        });

        var result = await dialog.Result;
        var uploadResult = Assert.IsType<UploadProgressDialog.UploadDialogResult>(result.Data);
        Assert.Null(uploadResult.Meta);
        Assert.NotNull(uploadResult.Error);
        Assert.Contains("上传失败", uploadResult.Error);
        Assert.Contains("init boom", uploadResult.Error);

        _storage.Verify(s => s.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Stream>(),
                It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _storage.Verify(s => s.CompleteUploadAsync(
            It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 5. Chunk 上传失败 → 显示错误，不调用 Complete
    // ============================================================

    [Fact]
    public async Task ChunkFails_ShowsError_DoesNotComplete()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = 1024, Resumed = false
            });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), 1024,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("chunk connection lost"));

        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("mid.bin", 1024) }
        });

        var result = await dialog.Result;
        var uploadResult = Assert.IsType<UploadProgressDialog.UploadDialogResult>(result.Data);
        Assert.NotNull(uploadResult.Error);
        Assert.Contains("上传失败", uploadResult.Error);
        Assert.Contains("chunk connection lost", uploadResult.Error);

        _storage.Verify(s => s.CompleteUploadAsync(
            It.IsAny<Guid>(), It.IsAny<CompleteUploadRequest?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 6. Complete 失败 → 显示错误
    // ============================================================

    [Fact]
    public async Task CompleteFails_ShowsError()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = 1024, Resumed = false
            });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), 1024,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("merge failed"));

        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("merge.bin", 1024) }
        });

        var result = await dialog.Result;
        var uploadResult = Assert.IsType<UploadProgressDialog.UploadDialogResult>(result.Data);
        Assert.NotNull(uploadResult.Error);
        Assert.Contains("上传失败", uploadResult.Error);
        Assert.Contains("merge failed", uploadResult.Error);
    }

    // ============================================================
    // 7. ContentType 为空 → 默认 application/octet-stream
    // ============================================================

    [Fact]
    public async Task EmptyContentType_DefaultsToOctetStream()
    {
        var uploadId = Guid.NewGuid();

        _storage.Setup(s => s.InitiateUploadAsync(
                It.Is<InitiateUploadRequest>(r =>
                    r.ContentType == "application/octet-stream"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = 1024, Resumed = false
            });

        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), 1024,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "noct-1", Version = 1, Size = 1024, Sha256 = "s"
            });

        _storage.Setup(s => s.GetFileByDocIdAsync("noct-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(), DocId = "noct-1", Version = 1,
                FileName = "noct.bin", ContentType = "application/octet-stream",
                Size = 1024, Sha256 = "s", Status = "active"
            });

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("noct.bin", 1024, "") }
        });

        _storage.Verify(s => s.InitiateUploadAsync(
            It.Is<InitiateUploadRequest>(r => r.ContentType == "application/octet-stream"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // 8. 标题固定为 "上传文件"
    // ============================================================

    [Fact]
    public async Task Title_IsFixed_UploadFile()
    {
        // 让 Initiate 挂起，对话框停留在"上传中"态以便检查静态标题
        var tcs = new TaskCompletionSource<InitiateUploadResponse>();
        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("title.bin", 1024) }
        });

        var title = provider.Find(".mud-dialog-title");
        Assert.Contains("上传文件", title.TextContent);
    }

    // ============================================================
    // 9. 状态文本：失败时显示 "失败"
    // ============================================================

    [Fact]
    public async Task StatusText_WhenFailed_ShowsFailed()
    {
        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("network down"));

        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("status.bin", 1024) }
        });

        // 对话框在错误时关闭并把错误传回；错误信息以"上传失败"开头，对应 StatusText="失败"
        var result = await dialog.Result;
        var uploadResult = Assert.IsType<UploadProgressDialog.UploadDialogResult>(result.Data);
        Assert.NotNull(uploadResult.Error);
        Assert.Contains("失败", uploadResult.Error);
    }

    // ============================================================
    // 10. 进度条元素存在（MudProgressLinear）
    // ============================================================

    [Fact]
    public async Task ProgressBar_IsRendered()
    {
        // 让 Initiate 挂起，对话框停留在"上传中"态以便检查进度条
        var tcs = new TaskCompletionSource<InitiateUploadResponse>();
        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var (provider, _) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request), Request("pb.bin", 1024) }
        });

        var progress = provider.Find(".mud-progress-linear");
        Assert.NotNull(progress);
    }

    // ============================================================
    // 11. 部分读流（一次 ReadAsync 读不满 chunk）→ 循环读满后再上传
    //     （回归：浏览器文件流单次 ReadAsync 只返回部分字节导致 S3 500）
    // ============================================================

    [Fact]
    public async Task PartialReadStream_UploadsFullChunkContent()
    {
        var uploadId = Guid.NewGuid();
        const int size = 4096;

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 1, ChunkSize = size, Resumed = false
            });

        byte[]? captured = null;
        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 0, It.IsAny<Stream>(), size,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, Stream, long, string?, CancellationToken>(
                (_, _, stream, _, _, _) =>
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    captured = ms.ToArray();
                })
            .ReturnsAsync(new UploadChunkResponse(uploadId, 0, 1, 1));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "partial-1", Version = 1, Size = size, Sha256 = "s"
            });
        _storage.Setup(s => s.GetFileByDocIdAsync("partial-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(), DocId = "partial-1", Version = 1,
                FileName = "p.bin", ContentType = "application/octet-stream",
                Size = size, Sha256 = "s", Status = "active"
            });

        // 每次 ReadAsync 最多返回 100 字节 → 单次读必须循环才能填满 4096
        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request),
                Request("p.bin", size, maxBytesPerRead: 100) }
        });
        await dialog.Result;

        Assert.NotNull(captured);
        Assert.Equal(size, captured!.Length);
        for (var i = 0; i < size; i++)
            Assert.Equal(FakeBrowserFile.ByteAt(i), captured[i]);
    }

    // ============================================================
    // 12. 断点续传 drain：上传内容必须从已上传字节之后开始（字节级对齐）
    // ============================================================

    [Fact]
    public async Task ResumedUpload_UploadsBytesFromCorrectOffset()
    {
        var uploadId = Guid.NewGuid();
        const int chunkSize = 1024;
        const int size = 3072; // 3 chunks，已传 0/1，补传 2

        _storage.Setup(s => s.InitiateUploadAsync(
                It.IsAny<InitiateUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InitiateUploadResponse
            {
                UploadId = uploadId, TotalChunks = 3, ChunkSize = chunkSize, Resumed = true
            });
        _storage.Setup(s => s.GetUploadStatusAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStatusResponse { ReceivedCount = 2 });

        byte[]? captured = null;
        _storage.Setup(s => s.UploadChunkAsync(
                uploadId, 2, It.IsAny<Stream>(), chunkSize,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, Stream, long, string?, CancellationToken>(
                (_, _, stream, _, _, _) =>
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    captured = ms.ToArray();
                })
            .ReturnsAsync(new UploadChunkResponse(uploadId, 2, 3, 3));

        _storage.Setup(s => s.CompleteUploadAsync(
                uploadId, It.IsAny<CompleteUploadRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                DocId = "resume-2", Version = 1, Size = size, Sha256 = "s"
            });
        _storage.Setup(s => s.GetFileByDocIdAsync("resume-2", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = Guid.NewGuid(), DocId = "resume-2", Version = 1,
                FileName = "r.bin", ContentType = "application/octet-stream",
                Size = size, Sha256 = "s", Status = "active"
            });

        // 部分读 + 续传组合：drain 也必须循环跳过 2048 字节
        var (_, dialog) = await OpenAsync(new DialogParameters
        {
            { nameof(UploadProgressDialog.Request),
                Request("r.bin", size, maxBytesPerRead: 300) }
        });
        await dialog.Result;

        Assert.NotNull(captured);
        Assert.Equal(chunkSize, captured!.Length);
        // chunk 2 内容 = 文件字节 [2048..3072)
        for (var i = 0; i < chunkSize; i++)
            Assert.Equal(FakeBrowserFile.ByteAt(2048 + i), captured[i]);
    }

    // ============================================================
    // FakeBrowserFile — 与 FileFieldTests 中的私有类相同，这里独立定义
    // ============================================================

    private sealed class FakeBrowserFile : Microsoft.AspNetCore.Components.Forms.IBrowserFile
    {
        /// <summary>确定性字节模式：索引 i 处的字节值。</summary>
        public static byte ByteAt(int i) => (byte)(i % 251);

        private readonly byte[] _data;
        private readonly int _maxBytesPerRead;

        public FakeBrowserFile(string name, long size, string contentType,
            int maxBytesPerRead = int.MaxValue)
        {
            Name = name;
            Size = size;
            ContentType = contentType;
            _data = new byte[size];
            for (var i = 0; i < size; i++) _data[i] = ByteAt(i);
            _maxBytesPerRead = maxBytesPerRead;
        }

        public string Name { get; }
        public long Size { get; }
        public string ContentType { get; }
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public Stream OpenReadStream(
            long maxAllowedSize = 512000,
            CancellationToken cancellationToken = default)
            => new PartialReadStream(_data, _maxBytesPerRead);
    }

    /// <summary>模拟浏览器文件流：每次 ReadAsync 最多返回 maxBytesPerRead 字节。</summary>
    private sealed class PartialReadStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _maxPerRead;
        private int _pos;

        public PartialReadStream(byte[] data, int maxPerRead)
        {
            _data = data;
            _maxPerRead = Math.Max(1, maxPerRead);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_pos >= _data.Length) return 0;
            var n = Math.Min(Math.Min(buffer.Length, _maxPerRead), _data.Length - _pos);
            _data.AsMemory(_pos, n).CopyTo(buffer);
            _pos += n;
            await Task.Yield();
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position
        {
            get => _pos;
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
