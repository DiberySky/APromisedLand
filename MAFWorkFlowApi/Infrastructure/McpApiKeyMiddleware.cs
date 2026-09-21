using System.Security.Cryptography;
using System.Text;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// MCP API Key 认证中间件：
/// - 只拦截 /mcp 路径的请求
/// - 检查 Authorization: Bearer {key} 头
/// - 未配置 key 时跳过（开发模式）
/// - 使用常量时间比较防止时序攻击
/// </summary>
public sealed class McpApiKeyMiddleware
{
    private const string McpPathPrefix = "/mcp";
    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly string? _expectedApiKey;
    private readonly ILogger<McpApiKeyMiddleware> _logger;

    public McpApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<McpApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedApiKey = configuration["Mcp:ApiKey"];

        if (string.IsNullOrWhiteSpace(_expectedApiKey))
        {
            _logger.LogWarning(
                "MCP API Key 未配置（Mcp:ApiKey 为空），/mcp 端点将跳过认证。" +
                "生产环境请务必配置！");
            _expectedApiKey = null;
        }
        else
        {
            _logger.LogInformation("MCP API Key 已启用，/mcp 端点需要 Bearer Token 认证。");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只保护 /mcp 路径
        if (!context.Request.Path.StartsWithSegments(McpPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 未配置 key，跳过认证
        if (_expectedApiKey is null)
        {
            await _next(context);
            return;
        }

        // 提取 Authorization 头
        var authHeader = context.Request.Headers[AuthorizationHeader].ToString();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "MCP 请求被拒绝：缺少 Authorization: Bearer 头。Path={Path}",
                context.Request.Path);
            await WriteUnauthorizedAsync(context, "Missing or invalid Authorization header.");
            return;
        }

        var providedKey = authHeader[BearerPrefix.Length..].Trim();

        // ★ 常量时间比较（防止时序攻击）
        if (!FixedTimeEquals(providedKey, _expectedApiKey))
        {
            _logger.LogWarning(
                "MCP 请求被拒绝：API Key 不匹配。Path={Path}", context.Request.Path);
            await WriteUnauthorizedAsync(context, "Invalid API key.");
            return;
        }

        // 通过认证，继续
        await _next(context);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);

        // 长度不等直接 false（长度本身不敏感）
        if (bytesA.Length != bytesB.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "unauthorized",
            message
        });
    }
}