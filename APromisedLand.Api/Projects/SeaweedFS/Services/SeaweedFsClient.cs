using System.Text.Json;
using APromisedLand.Api.Projects.SeaweedFS.Models;
using Microsoft.Extensions.Options;

namespace APromisedLand.Api.Projects.SeaweedFS.Services;

public class SeaweedFsClient(HttpClient httpClient, IOptions<SeaweedFsOptions> options) : ISeaweedFsClient
{
    private readonly SeaweedFsOptions _options = options.Value;

    /// <summary>
    /// 上传文件 - 两步法：先向 Master 分配 FID，再直接 PUT 到 Volume
    /// </summary>
    public async Task<string> UploadAsync(Stream fileStream, string fileName, string? path = null)
    {
        // 1. 向 Master 申请一个 FID
        var assignUrl = $"{_options.MasterUrl}/dir/assign";
        var assignResponse = await httpClient.GetAsync(assignUrl);
        assignResponse.EnsureSuccessStatusCode();
        var assignJson = await assignResponse.Content.ReadAsStringAsync();
        var assign = JsonSerializer.Deserialize<AssignResponse>(assignJson);
        if (assign?.FileId == null)
            throw new Exception($"Failed to assign FID from Master. Response: {assignJson}");

        var fid = assign.FileId;
        var publicUrl = assign.PublicUrl;

        // 确保 publicUrl 是绝对 URI（添加协议）
        if (!string.IsNullOrEmpty(publicUrl) &&
            !publicUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            publicUrl = $"http://{publicUrl}";
        }

        // 2. 将文件内容直接 PUT 到 Volume 的公共 URL
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var volumeUrl = _options.VolumeUrl ?? new UriBuilder(new Uri(_options.BaseUrl)) { Port = 8080 }.Uri.ToString();
        var uploadUrl = $"{volumeUrl.TrimEnd('/')}/{fid}";
        // var uploadUrl = $"{publicUrl}/{fid}";
        using var content = new ByteArrayContent(fileBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var uploadResponse = await httpClient.PutAsync(uploadUrl, content);
        uploadResponse.EnsureSuccessStatusCode();

        // 可选：如果希望记录文件在 Filer 中的路径，可以额外调用 Filer 的元数据接口，
        // 但本方案已通过 PostgreSQL 存储 fid 与业务路径的映射，无需依赖 Filer 目录结构。
        return fid;
    }

    /// <summary>
    /// 下载文件 - 使用 Filer API（通过 fid 直接访问）
    /// </summary>
    public async Task<Stream> DownloadAsync(string fid)
    {
        // 构造 Volume 地址（同上传时的 publicUrl）
        var baseUri = new Uri(_options.BaseUrl);
        var volumeUri = new UriBuilder(baseUri) { Port = 8080 }.Uri;
        var publicUrl = volumeUri.ToString().TrimEnd('/');

        var url = $"{publicUrl}/{fid}";
        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }


    /// <summary>
    /// 删除文件 - 使用 Filer API
    /// </summary>
    public async Task DeleteAsync(string fid)
    {
        var url = $"{_options.BaseUrl}/{fid}";
        var response = await httpClient.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<string> GetFidFromTusUploadAsync(string uploadId)
    {
        // 1. 从 uploadId 中提取文件 key（去掉路径前缀）
        var fileKey = uploadId.Split('/').Last();
        // 2. 向 Filer 发送 HEAD 请求，获取 X-Seaweed-Fid 头
        var headUrl = $"{_options.BaseUrl}/.tus/.uploads/{fileKey}";
        var headRequest = new HttpRequestMessage(HttpMethod.Head, headUrl);
        headRequest.Headers.Add("Tus-Resumable", "1.0.0");

        var headResponse = await httpClient.SendAsync(headRequest);
        headResponse.EnsureSuccessStatusCode();

        if (headResponse.Headers.TryGetValues("X-Seaweed-Fid", out var values))
        {
            var fid = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(fid))
                return fid;
        }
        throw new Exception($"无法从 TUS 上传 {uploadId} 获取 FID");
    }
}
