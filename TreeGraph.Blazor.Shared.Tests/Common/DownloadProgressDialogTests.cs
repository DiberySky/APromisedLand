using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
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
/// DownloadProgressDialog.razor 单测（MudDialogProvider + IDialogService.ShowAsync 打开真实对话框，
/// 与 UploadProgressDialogTests 同一模式）。
///
/// 覆盖：成功下载（流式读取 + JS 保存）、部分读流、元数据缺失、流缺失、存储异常、
///       标题/进度条渲染。
/// </summary>
public class DownloadProgressDialogTests : BunitTestBase
{
    private readonly Mock<IFileStorageClient> _storage = new();

    public DownloadProgressDialogTests()
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
        var dialog = await ds.ShowAsync<DownloadProgressDialog>(string.Empty, parameters, new DialogOptions());
        provider.Render();
        return (provider, dialog);
    }

    private static DialogParameters Params(string docId = "doc-1", int version = 1,
        string fileName = "a.bin", long size = 5) => new()
    {
        { nameof(DownloadProgressDialog.Request),
            new DownloadProgressDialog.DownloadRequest(docId, version, fileName, size) }
    };

    private void SetupSuccess(Guid fileGuid, Stream stream,
        string docId = "doc-1", int version = 1, string fileName = "a.bin", long size = 5)
    {
        _storage.Setup(s => s.GetFileByDocIdAsync(docId, version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid, DocId = docId, Version = version,
                FileName = fileName, ContentType = "application/octet-stream",
                Size = size, Status = "active"
            });
        _storage.Setup(s => s.DownloadFileAsync(
                fileGuid, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);
    }

    // ============================================================
    // 1. 成功：GetFileByDocId → DownloadFileAsync → JS 保存 → Close(Error=null)
    // ============================================================

    [Fact]
    public async Task Success_DownloadsAndInvokesJs_ClosesWithNullError()
    {
        var fileGuid = Guid.NewGuid();
        SetupSuccess(fileGuid, new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        var (_, dialog) = await OpenAsync(Params());

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.Null(downloadResult.Error);

        _storage.Verify(s => s.GetFileByDocIdAsync("doc-1", 1, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DownloadFileAsync(
            fileGuid, null, null, It.IsAny<CancellationToken>()), Times.Once);

        var invocations = JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.downloadFromStream")
            .ToList();
        Assert.Single(invocations);
        Assert.Equal("a.bin", invocations[0].Arguments[0] as string);
        Assert.IsType<DotNetStreamReference>(invocations[0].Arguments[1]);
    }

    // ============================================================
    // 2. 部分读流（每次 ReadAsync 最多返回 2 字节）→ 循环读完后仍能成功保存
    // ============================================================

    [Fact]
    public async Task PartialReadStream_DownloadsAllBytes_ThenSaves()
    {
        var fileGuid = Guid.NewGuid();
        SetupSuccess(fileGuid,
            new PartialReadStream(Encoding.UTF8.GetBytes("hello"), maxBytesPerRead: 2));

        var (_, dialog) = await OpenAsync(Params(size: 5));

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.Null(downloadResult.Error);

        Assert.Single(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.downloadFromStream");
    }

    // ============================================================
    // 3. 元数据不存在 → Close(Error="文件不存在")，不请求流
    // ============================================================

    [Fact]
    public async Task MetadataMissing_ClosesWithFileNotFound_SkipsStream()
    {
        _storage.Setup(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileMetadataDto?)null);

        var (_, dialog) = await OpenAsync(Params());

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.Equal("文件不存在", downloadResult.Error);

        _storage.Verify(s => s.DownloadFileAsync(
            It.IsAny<Guid>(), It.IsAny<long?>(), It.IsAny<long?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 4. 流不存在 → Close(Error="文件不存在")，不调 JS
    // ============================================================

    [Fact]
    public async Task StreamNull_ClosesWithFileNotFound_SkipsJs()
    {
        var fileGuid = Guid.NewGuid();
        _storage.Setup(s => s.GetFileByDocIdAsync("doc-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid, DocId = "doc-1", Version = 1,
                FileName = "a.bin", ContentType = "application/octet-stream",
                Size = 5, Status = "active"
            });
        _storage.Setup(s => s.DownloadFileAsync(
                fileGuid, It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var (_, dialog) = await OpenAsync(Params());

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.Equal("文件不存在", downloadResult.Error);

        Assert.Empty(JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.downloadFromStream"));
    }

    // ============================================================
    // 5. 存储异常 → Close(Error 以"下载失败"开头)
    // ============================================================

    [Fact]
    public async Task StorageThrows_ClosesWithDownloadError()
    {
        _storage.Setup(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("network down"));

        var (_, dialog) = await OpenAsync(Params());

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.NotNull(downloadResult.Error);
        Assert.StartsWith("下载失败", downloadResult.Error);
        Assert.Contains("network down", downloadResult.Error);
    }

    // ============================================================
    // 6. 标题固定为 "下载文件"
    // ============================================================

    [Fact]
    public async Task Title_IsFixed_DownloadFile()
    {
        // 让元数据请求挂起，对话框停留在"下载中"态以便检查静态标题
        var tcs = new TaskCompletionSource<FileMetadataDto?>();
        _storage.Setup(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var (provider, _) = await OpenAsync(Params());

        var title = provider.Find(".mud-dialog-title");
        Assert.Contains("下载文件", title.TextContent);
    }

    // ============================================================
    // 7. 进度条元素存在（MudProgressLinear）
    // ============================================================

    [Fact]
    public async Task ProgressBar_IsRendered()
    {
        var tcs = new TaskCompletionSource<FileMetadataDto?>();
        _storage.Setup(s => s.GetFileByDocIdAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var (provider, _) = await OpenAsync(Params());

        var progress = provider.Find(".mud-progress-linear");
        Assert.NotNull(progress);
    }

    // ============================================================
    // 8. 取消流程：下载进行中点"取消" → 解除阻塞读取 → Close(Cancel)
    // ============================================================

    [Fact]
    public async Task Cancel_DuringDownload_ClosesWithCancelledResult()
    {
        var fileGuid = Guid.NewGuid();
        SetupSuccess(fileGuid, new BlockingReadStream());

        var (provider, dialog) = await OpenAsync(Params());

        var cancelButton = provider.FindAll("button")
            .First(b => b.TextContent.Contains("取消"));
        await cancelButton.TriggerEventAsync("onclick", new MouseEventArgs());

        var result = await dialog.Result;
        Assert.True(result.Canceled);

        Assert.Empty(JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.downloadFromStream"));
    }

    // ============================================================
    // 9. 进度文本：读到第一批字节后实时更新 "已下载 X / Y"
    // ============================================================

    [Fact]
    public async Task Progress_UpdatesDownloadedBytesText()
    {
        var fileGuid = Guid.NewGuid();
        // 第一次 ReadAsync 返回 2 字节后阻塞，便于观察中间态
        SetupSuccess(fileGuid, new StepStream(Encoding.UTF8.GetBytes("he")), size: 5);

        var (provider, dialog) = await OpenAsync(Params(size: 5));

        provider.WaitForAssertion(
            () => Assert.Contains("2 / 5", provider.Markup),
            TimeSpan.FromSeconds(5));

        // 收尾：取消以解除阻塞读取
        var cancelButton = provider.FindAll("button")
            .First(b => b.TextContent.Contains("取消"));
        await cancelButton.TriggerEventAsync("onclick", new MouseEventArgs());
        var result = await dialog.Result;
        Assert.True(result.Canceled);
    }

    // ============================================================
    // 10. 零字节文件：_totalBytes=0 时进度计算除零防护 → 仍成功保存
    // ============================================================

    [Fact]
    public async Task ZeroSizeFile_NoDivideByZero_ClosesSuccessfully()
    {
        var fileGuid = Guid.NewGuid();
        SetupSuccess(fileGuid, new MemoryStream(Array.Empty<byte>()), size: 0);

        var (_, dialog) = await OpenAsync(Params(size: 0));

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.Null(downloadResult.Error);

        Assert.Single(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.downloadFromStream");
    }

    // ============================================================
    // 11. JS 互操作失败（宿主漏引 fileDownload.js）→ Close(Error="下载失败…")
    // ============================================================

    [Fact]
    public async Task JsInteropThrows_ClosesWithDownloadError()
    {
        var fileGuid = Guid.NewGuid();
        SetupSuccess(fileGuid, new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        JSInterop.SetupVoid("treegraphFile.downloadFromStream", _ => true)
            .SetException(new JSException("treegraphFile.downloadFromStream is not defined"));

        var (_, dialog) = await OpenAsync(Params());

        var result = await dialog.Result;
        var downloadResult = Assert.IsType<DownloadProgressDialog.DownloadDialogResult>(result.Data);
        Assert.NotNull(downloadResult.Error);
        Assert.StartsWith("下载失败", downloadResult.Error);
        Assert.Contains("downloadFromStream", downloadResult.Error);
    }

    /// <summary>每次 ReadAsync 最多返回 maxBytesPerRead 字节，模拟渐进网络流。</summary>
    private sealed class PartialReadStream : MemoryStream
    {
        private readonly int _maxBytesPerRead;

        public PartialReadStream(byte[] buffer, int maxBytesPerRead) : base(buffer)
        {
            _maxBytesPerRead = maxBytesPerRead;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            var limited = destination.Length > _maxBytesPerRead
                ? destination[.._maxBytesPerRead] : destination;
            return await base.ReadAsync(limited, cancellationToken);
        }
    }

    /// <summary>ReadAsync 永久阻塞直到 CancellationToken 取消，模拟慢速网络。</summary>
    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>第一次 ReadAsync 返回第一批字节，之后阻塞直到取消（用于观察进度中间态）。</summary>
    private sealed class StepStream : Stream
    {
        private readonly byte[] _firstBatch;
        private bool _firstBatchReturned;

        public StepStream(byte[] firstBatch)
        {
            _firstBatch = firstBatch;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_firstBatchReturned)
            {
                _firstBatchReturned = true;
                _firstBatch.CopyTo(buffer);
                return _firstBatch.Length;
            }
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
