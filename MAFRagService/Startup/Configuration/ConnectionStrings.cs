namespace MAFRagService.Startup.Configuration;

/// <summary>
/// 集中承载所有基础设施连接串，避免散落的 GetConnectionString 调用。
/// - Required: 缺失即抛异常。
/// - Optional: 缺失时在 Development 环境回退到默认值，否则抛异常。
/// - 所有回退日志经 <see cref="Sanitizer.Url"/> 脱敏。
/// </summary>
public sealed record ConnectionStrings(
    string HangfireDb,
    string MetadataDb,
    string Redis,
    string Nebula,
    string SeaweedFs,
    string Weaviate)
{
    /// <summary>
    /// 从 IConfiguration 解析全部连接串。
    /// </summary>
    /// <param name="config">应用配置。</param>
    /// <param name="env">宿主环境。</param>
    /// <param name="warn">可选：警告输出回调（如 Console.WriteLine 或 ILogger）。</param>
    public static ConnectionStrings Resolve(
        IConfiguration config,
        IHostEnvironment env,
        Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(env);

        string Required(string name) =>
            config.GetConnectionString(name)
            ?? throw new InvalidOperationException(
                $"缺少 ConnectionStrings:{name}。必须由 AppHost 注入，或显式配置。");

        string Optional(string name, string devFallback)
        {
            var value = config.GetConnectionString(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            if (!env.IsDevelopment())
                throw new InvalidOperationException(
                    $"缺少 ConnectionStrings:{name}。生产环境必须由 AppHost 注入。");

            warn?.Invoke(
                $"[WARN] ConnectionStrings:{name} 缺失，回退到 {Sanitizer.Url(devFallback)}。" +
                "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
            return devFallback;
        }

        return new ConnectionStrings(
            HangfireDb: Required("HangfireDb"),
            MetadataDb: Required("MetadataDb"),
            Redis:      Required("redis"),
            Nebula:     Optional("nebula",    "http://localhost:9669"),
            SeaweedFs:  Optional("seaweedfs", "http://localhost:8333"),
            Weaviate:   Optional("weaviate",  "http://localhost:8081"));
    }
}