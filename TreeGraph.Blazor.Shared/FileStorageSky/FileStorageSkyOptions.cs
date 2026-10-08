namespace TreeGraph.Blazor.Shared.FileStorageSky;

/// <summary>
/// 文件存储 API 客户端配置。
/// 默认 BasePath 为空——FileStorageApi 的控制器使用 [Route("[controller]")]，
/// 实际路由为 /Uploads/* 与 /Files/*（首字母大写，与 Controller 类名一致）。
/// </summary>
public class FileStorageSkyOptions
{
    /// <summary>上传会话路由前缀，默认 "Uploads"。</summary>
    public string UploadsPath { get; set; } = "Uploads";

    /// <summary>文件元数据路由前缀，默认 "Files"。</summary>
    public string FilesPath { get; set; } = "Files";

    /// <summary>分块上传单块上限（字节），与后端 RequestSizeLimit 16MB 对齐。</summary>
    public long MaxChunkSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>默认分块大小（8MB）。</summary>
    public int DefaultChunkSize { get; set; } = 8 * 1024 * 1024;

    /// <summary>心跳续期间隔（秒）。建议 5 分钟，避免会话过期。</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 300;
}
