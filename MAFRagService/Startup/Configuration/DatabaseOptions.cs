namespace MAFRagService.Startup.Configuration;

public enum DatabaseStrategy
{
    /// <summary>Development → EnsureCreated；其他环境 → Migrate。</summary>
    Auto,
    /// <summary>强制 EnsureCreated，忽略 migrations。</summary>
    EnsureCreated,
    /// <summary>强制 Migrate，要求存在 migrations。</summary>
    Migrate,
    /// <summary>跳过数据库初始化（仅用于测试或外部托管场景）。</summary>
    None
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseStrategy Strategy { get; set; } = DatabaseStrategy.Auto;
}