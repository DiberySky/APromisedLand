using DiberyBlazorWebSky.Services;
using Microsoft.Net.Http.Headers;

namespace DiberyBlazorWebSky.Endpoints;

/// <summary>
/// 浏览器直连的下载端点。
///
/// 为什么要这么做：
///   Blazor Server 模式下，DotNetStreamReference 会把整个文件从
///   服务端经 SignalR 传给浏览器 JS，1 GB 文件会 OOM 或静默失败。
///   改为浏览器直接 GET 本端点，服务端流式代理 FileStorageApi。
///
/// 路由：
///   GET /download/{id:guid}         → 全量下载
///   GET /download/{id:guid}?name=x  → 覆盖文件名（可选）
/// </summary>
public static class DownloadEndpoints
{
    public static IEndpointRouteBuilder MapDownloadEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/download/{id:guid}",
            async (
                Guid id,
                FileStorageApiClient fileApi,
                HttpContext http,
                CancellationToken ct) =>
            {
                // 1. 拿元数据（决定 Content-Type / 文件名）
                var meta = await fileApi.GetByIdAsync(id, ct);
                if (meta is null) return Results.NotFound();

                // 2. 打开上游流
                var upstream = await fileApi.DownloadStreamAsync(id, ct);
                if (upstream is null) return Results.NotFound();

                // 3. 设置响应头
                var contentType = string.IsNullOrWhiteSpace(meta.ContentType)
                    ? "application/octet-stream"
                    : meta.ContentType;

                http.Response.ContentType = contentType;

                if (!string.IsNullOrWhiteSpace(meta.FileName))
                {
                    var cd = new ContentDispositionHeaderValue("attachment");
                    cd.SetHttpFileName(meta.FileName);
                    http.Response.Headers.ContentDisposition = cd.ToString();
                }

                // 4. 透传流（服务端边读边写，内存占用与文件大小无关）
                await using (upstream)
                {
                    await upstream.CopyToAsync(http.Response.Body, ct);
                }

                return Results.Empty;
            })
            // 允许匿名访问；如将来加认证，改成 .RequireAuthorization()
            .AllowAnonymous();

        return endpoints;
    }
}