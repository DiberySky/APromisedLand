using APromisedLand.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MAFRagService.Initializers;

public class DatabaseInitializer
{
    // ★ 直接注入 MafRagContext（scoped），不再用 IServiceProvider 建内层 scope
    private readonly MafRagContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(MafRagContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        // ★ 打印目标数据库，与 Weaviate / Seaweed 风格统一
        var conn = _context.Database.GetConnectionString();
        _logger.LogInformation(
            "MafRagContext target = {Conn}",
            Summarize(conn));

        // ★ 注意：EnsureCreatedAsync 与 EF Migrations 不能共存。
        //   当前处于早期开发阶段可继续用；
        //   一旦引入 migrations，改成：await _context.Database.MigrateAsync(ct);
        await _context.Database.EnsureCreatedAsync(ct);

        _logger.LogInformation("MafRagContext database initialized");
    }

    /// <summary>
    /// 隐藏连接串中的敏感字段，避免日志泄露密码。
    /// </summary>
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