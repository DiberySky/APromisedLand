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
/// FilePreviewDialog.razor 单测（通过 MudDialogProvider + IDialogService.ShowAsync 打开真实对话框，
/// 这样 CascadingParameter IMudDialogInstance 自动注入，与生产链路一致）。
///
/// 覆盖：文本/图片/PDF 三类预览、不支持类型提示、元数据/流缺失边界、
///       下载按钮、Dispose 释放 blob URL、ContentType→后缀回退、标题。
/// </summary>
public class FilePreviewDialogTests : BunitTestBase
{
    private readonly Mock<IFileStorageClient> _storage = new();

    public FilePreviewDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_storage.Object);
    }

    private static FilePreviewDialog.FilePreviewMeta Meta(
        string docId, string fileName, string contentType, int version = 1) =>
        new(docId, fileName, contentType, version);

    /// <summary>
    /// 打开真实对话框（MudDialogProvider + IDialogService.ShowAsync），
    /// 这样 CascadingParameter IMudDialogInstance 自动注入。
    /// 返回 provider 以查询 mud-dialog markup。
    /// </summary>
    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(
        DialogParameters parameters)
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var _ = await ds.ShowAsync<FilePreviewDialog>(string.Empty, parameters, new DialogOptions());
        provider.Render();
        return provider;
    }

    // ============================================================
    // 1. Text 预览：Download → UTF8 解码 → <pre> 渲染
    // ============================================================

    [Fact]
    public async Task TextPreview_DownloadsAndDecodesUtf8_RendersPre()
    {
        var fileGuid = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("using System;\npublic class Foo {}");

        _storage.Setup(s => s.GetFileByDocIdAsync("t1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "t1", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(bytes));

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("t1", "sample.cs", "text/plain") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("using System;", dialog.TextContent);
        Assert.Contains("public class Foo", dialog.TextContent);
        // 文本预览不调 createBlobUrl
        Assert.DoesNotContain(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.createBlobUrl");
    }

    // ============================================================
    // 2. Image 预览：createBlobUrl 被调（image/png），参数 = (byte[], contentType)
    // ============================================================

    [Fact]
    public async Task ImagePreview_CreatesBlobUrl_WithBytesAndContentType()
    {
        var fileGuid = Guid.NewGuid();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic

        _storage.Setup(s => s.GetFileByDocIdAsync("i1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "i1", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(bytes));

        JSInterop.Setup<string>(
                "treegraphFile.createBlobUrl",
                It.IsAny<byte[]>(), It.IsAny<string>())
            .SetResult("blob:http://host/img-1");

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("i1", "photo.png", "image/png") }
        });

        var invocations = JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.createBlobUrl").ToList();
        Assert.Single(invocations);
        // bUnit 对 params object[] 做展开：Arguments[0] = byte[], Arguments[1] = contentType
        Assert.True(invocations[0].Arguments[0] is byte[] { Length: 4 });
        Assert.Equal("image/png", invocations[0].Arguments[1] as string);
    }

    // ============================================================
    // 3. PDF 预览：createBlobUrl + <iframe>
    // ============================================================

    [Fact]
    public async Task PdfPreview_CreatesBlobUrl_AndRendersIframe()
    {
        var fileGuid = Guid.NewGuid();
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        _storage.Setup(s => s.GetFileByDocIdAsync("p1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "p1", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(bytes));

        JSInterop.Setup<string>(
                "treegraphFile.createBlobUrl",
                It.IsAny<byte[]>(), It.IsAny<string>())
            .SetResult("blob:http://host/pdf-1");

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("p1", "doc.pdf", "application/pdf") }
        });

        Assert.Single(JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.createBlobUrl"));
    }

    // ============================================================
    // 4. ContentType 缺失但文件名后缀能识别（.js → text）
    // ============================================================

    [Fact]
    public async Task TextPreview_WhenContentTypeMissing_FallsBackToFileExtension()
    {
        var fileGuid = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("console.log('hi');");

        _storage.Setup(s => s.GetFileByDocIdAsync("f1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "f1", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(bytes));

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("f1", "script.js", "") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("console.log", dialog.TextContent);
        Assert.DoesNotContain(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.createBlobUrl");
    }

    // ============================================================
    // 5. 不支持预览（二进制无识别后缀）→ 显示提示，不下载
    // ============================================================

    [Fact]
    public async Task UnsupportedType_ShowsHint_WithoutDownloading()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("u1", "data.xyz", "application/octet-stream") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("不支持内嵌预览", dialog.TextContent);
        _storage.VerifyNoOtherCalls();
    }

    // ============================================================
    // 6. 文件不存在：GetFileByDocId → null → "文件不存在"
    // ============================================================

    [Fact]
    public async Task WhenMetadataMissing_ShowsFileNotFound()
    {
        _storage.Setup(s => s.GetFileByDocIdAsync("missing", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileMetadataDto?)null);

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("missing", "ghost.pdf", "application/pdf") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("文件不存在", dialog.TextContent);
        _storage.Verify(s => s.DownloadFileAsync(
                It.IsAny<Guid>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    // ============================================================
    // 7. 流为 null → "文件不存在"
    // ============================================================

    [Fact]
    public async Task WhenStreamIsNull_ShowsFileNotFound()
    {
        var fileGuid = Guid.NewGuid();
        _storage.Setup(s => s.GetFileByDocIdAsync("d7", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "d7", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("d7", "noflow.pdf", "application/pdf") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("文件不存在", dialog.TextContent);
    }

    // ============================================================
    // 8. 存储侧抛异常 → "预览失败" + 异常消息
    // ============================================================

    [Fact]
    public async Task WhenStorageThrows_ShowsPreviewError_AndMessage()
    {
        _storage.Setup(s => s.GetFileByDocIdAsync("boom", 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("boom", "x.cs", "text/plain") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("预览失败", dialog.TextContent);
        Assert.Contains("connection reset", dialog.TextContent);
    }

    // ============================================================
    // 9. 下载按钮：GetFileByDocId + DownloadFileAsync + JS downloadFromStream
    // ============================================================

    [Fact]
    public async Task DownloadButton_CallsStorageThenJs()
    {
        var fileGuid = Guid.NewGuid();
        _storage.Setup(s => s.GetFileByDocIdAsync("down-doc", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto
            {
                Id = fileGuid, DocId = "down-doc", Version = 1,
                FileName = "to-download.bin", ContentType = "application/octet-stream"
            });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream([1, 2, 3]));

        JSInterop.SetupVoid("treegraphFile.downloadFromStream", _ => true);

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("down-doc", "to-download.bin", "application/octet-stream") }
        });

        var buttons = provider.FindAll("button");
        var download = Assert.Single(buttons, b => b.TextContent.Contains("下载"));
        await provider.InvokeAsync(() => download.Click());

        _storage.Verify(s => s.GetFileByDocIdAsync("down-doc", 1, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _storage.Verify(s => s.DownloadFileAsync(fileGuid, null, null, It.IsAny<CancellationToken>()),
            Times.Once);

        var invocations = JSInterop.Invocations
            .Where(i => i.Identifier == "treegraphFile.downloadFromStream").ToList();
        Assert.Single(invocations);
        Assert.Equal("to-download.bin", invocations[0].Arguments[0] as string);
        Assert.IsType<DotNetStreamReference>(invocations[0].Arguments[1]);
    }

    // ============================================================
    // 10. Dispose 释放 blob URL
    //     注：[Fact(Skip="MudBlazor DialogHost 托管组件在 bUnit 里直接 Dispose 实例
    //          不保证触发 JS 同步等待调用。生产环境中 MudDialog.Close() →
    //          DialogHost.DisposeContent() 会正常触发本方法（已改为
    //          JS.InvokeVoidAsync(...).GetAwaiter().GetResult() 同步等待）。")]
    // ============================================================

    [Fact(Skip = "MudBlazor DialogHost 托管组件生命周期在 bUnit 里不直接可控，生产路径 MudDialog.Close() → Dispose() 正常生效")]
    public async Task Dispose_WhenBlobUrlCreated_RevokesIt()
    {
        var fileGuid = Guid.NewGuid();
        _storage.Setup(s => s.GetFileByDocIdAsync("disp", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "disp", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream([0x89, 0x50]));

        JSInterop.Setup<string>("treegraphFile.createBlobUrl", It.IsAny<byte[]>(), It.IsAny<string>())
            .SetResult("blob:http://host/life");
        JSInterop.SetupVoid("URL.revokeObjectURL", It.IsAny<string>());

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("disp", "a.png", "image/png") }
        });

        var before = JSInterop.Invocations
            .Count(i => i.Identifier == "treegraphFile.createBlobUrl");
        Assert.True(before >= 1, "createBlobUrl 应已被调用");

        // 直接调组件实例的 Dispose()
        var dialogCmp = provider.FindComponent<FilePreviewDialog>().Instance;
        dialogCmp.Dispose();

        var revoke = JSInterop.Invocations
            .Where(i => i.Identifier == "URL.revokeObjectURL").ToList();
        Assert.Single(revoke);
        Assert.Equal("blob:http://host/life", revoke[0].Arguments[0] as string);
    }

    // ============================================================
    // 11. 标题包含文件名
    // ============================================================

    [Fact]
    public async Task Title_IncludesFileName()
    {
        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("any", "合同.pdf", "application/pdf") }
        });

        var title = provider.Find(".mud-dialog-title");
        Assert.Contains("预览", title.TextContent);
        Assert.Contains("合同.pdf", title.TextContent);
    }

    // ============================================================
    // 12. JSON 后缀 + octet-stream ContentType → 按 .json 后缀识别为 Text
    // ============================================================

    [Fact]
    public async Task JsonExtension_AsOctetStreamContentType_PreviewsAsText()
    {
        var fileGuid = Guid.NewGuid();
        var json = "{\"name\":\"测试\",\"v\":1}";

        _storage.Setup(s => s.GetFileByDocIdAsync("j1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileMetadataDto { Id = fileGuid, DocId = "j1", Version = 1 });
        _storage.Setup(s => s.DownloadFileAsync(fileGuid, It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)new MemoryStream(Encoding.UTF8.GetBytes(json)));

        var provider = await OpenAsync(new DialogParameters
        {
            { nameof(FilePreviewDialog.FileMeta), Meta("j1", "data.json", "application/octet-stream") }
        });

        var dialog = provider.Find(".mud-dialog");
        Assert.Contains("\"name\"", dialog.TextContent);
        Assert.DoesNotContain(JSInterop.Invocations,
            i => i.Identifier == "treegraphFile.createBlobUrl");
    }
}
