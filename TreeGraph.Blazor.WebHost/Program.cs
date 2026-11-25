// 内容根固定为程序目录：开发期静态 Web 资产清单与发布产物 wwwroot 都位于此，
// 避免由 Aspire/DCP 等宿主以不同工作目录启动时解析不到 index.html 与 _framework。
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
var app = builder.Build();

string Required(string key) => app.Configuration[key]
    ?? throw new InvalidOperationException($"缺少公开浏览器配置 {key}。");
var publicEndpoints = new[]
{
    Required("PublicBrowser:KeycloakAuthority"),
    Required("PublicBrowser:TreeGraphApi"),
    Required("PublicBrowser:SyncServices"),
    Required("PublicBrowser:FileStorageApi"),
    Required("PublicBrowser:MafWfApi"),
    Required("PublicBrowser:IdentityAdminApi")
};
var connectOrigins = publicEndpoints
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Select(value => new Uri(value!).GetLeftPart(UriPartial.Authority))
    .Distinct(StringComparer.OrdinalIgnoreCase);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
            "script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; font-src 'self' data:; worker-src 'self' blob:; " +
            $"connect-src 'self' {string.Join(' ', connectOrigins)}";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XFrameOptions = "DENY";
        if (context.Request.Path == "/" || context.Request.Path.Equals("/index.html") || context.Request.Path.Equals("/config.json"))
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        else if (IsFingerprintedStaticAsset(context.Request.Path.Value))
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Task.CompletedTask;
    });
    await next();
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapGet("/config.json", () => Results.Json(new
{
    Keycloak = new
    {
        Authority = Required("PublicBrowser:KeycloakAuthority"),
        ClientId = Required("PublicBrowser:ClientId")
    },
    Apis = new
    {
        TreeGraphApi = Required("PublicBrowser:TreeGraphApi"),
        SyncServices = Required("PublicBrowser:SyncServices"),
        FileStorageApi = Required("PublicBrowser:FileStorageApi"),
        MafWfApi = Required("PublicBrowser:MafWfApi"),
        IdentityAdminApi = Required("PublicBrowser:IdentityAdminApi")
    }
}));
app.MapFallbackToFile("index.html");

app.Run();

static bool IsFingerprintedStaticAsset(string? path)
{
    var fileName = Path.GetFileName(path);
    if (fileName?.EndsWith(".br", StringComparison.OrdinalIgnoreCase) == true)
        fileName = fileName[..^3];
    else if (fileName?.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) == true)
        fileName = fileName[..^3];

    var parts = fileName?.Split('.') ?? [];
    return parts.Length >= 3
        && parts[^2].Length == 10
        && parts[^2].All(char.IsAsciiLetterOrDigit);
}
