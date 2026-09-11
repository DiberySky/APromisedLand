using APromisedLand.Api.Data;
using MAFRagService.Startup.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MAFRagService.Initializers;

public class DatabaseInitializer
{
    private readonly MafRagContext _context;
    private readonly IHostEnvironment _env;
    private readonly IOptions<DatabaseOptions> _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        MafRagContext context,
        IHostEnvironment env,
        IOptions<DatabaseOptions> options,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _env     = env;
        _options = options;
        _logger  = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var strategy = ResolveStrategy();

        if (strategy == DatabaseStrategy.None)
        {
            _logger.LogInformation("数据库初始化已禁用（Strategy=None）。");
            return;
        }

        var conn = _context.Database.GetConnectionString();
        _logger.LogInformation(
            "MafRagContext target = {Conn}, Strategy = {Strategy}",
            Summarize(conn), strategy);

        switch (strategy)
        {
            case DatabaseStrategy.EnsureCreated:
                await EnsureCreatedAsync(ct);
                break;
            case DatabaseStrategy.Migrate:
                await MigrateAsync(ct);
                break;
        }

        _logger.LogInformation("MafRagContext database initialized");
    }

    private DatabaseStrategy ResolveStrategy()
    {
        var configured = _options.Value.Strategy;
        if (configured != DatabaseStrategy.Auto) return configured;

        return _env.IsDevelopment()
            ? DatabaseStrategy.EnsureCreated
            : DatabaseStrategy.Migrate;
    }

    private async Task EnsureCreatedAsync(CancellationToken ct)
    {
        // ★ 已修复：⚠️ → [WARN]，避免 Windows 控制台（CP936）显示为 ??
        _logger.LogInformation(
            "使用 EnsureCreatedAsync（开发/测试策略）。[WARN] 与 EF Migrations 不共存，切换前需 DROP 数据库或 baseline。");
        await _context.Database.EnsureCreatedAsync(ct);
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        var migrations = _context.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            throw new InvalidOperationException(
                "Strategy=Migrate 但项目中没有 migrations。" +
                "请先运行 `dotnet ef migrations add Initial`。" +
                "或在开发环境改用 Strategy=EnsureCreated / Auto。");
        }

        var pending = (await _context.Database.GetPendingMigrationsAsync(ct)).ToList();
        _logger.LogInformation(
            "已注册 {Total} 个 migrations，待应用 {Pending} 个：{Names}",
            migrations.Count, pending.Count,
            pending.Count == 0 ? "<none>" : string.Join(", ", pending));

        await _context.Database.MigrateAsync(ct);
    }

    private static string Summarize(string? connStr)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return "<unset>";

        var parts = connStr
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)
                        && !p.TrimStart().StartsWith("Pwd",      StringComparison.OrdinalIgnoreCase));
        return string.Join(';', parts);
    }
}