using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Shared.Platform;

namespace TreeGraph.Blazor.Shared.Tests.NodeEav;

/// <summary>
/// NodeEav 响应式测试共享基座：
/// 假 HttpMessageHandler 按 URL 路径返回 canned JSON；
/// 真实 EavApiClient + FakePlatformContext。
/// </summary>
internal static class NodeEavTestSetup
{
    /// <summary>注册 NodeEav 页面测试所需全部服务。</summary>
    public static FakePlatformContext RegisterServices(
        IServiceCollection services,
        Func<string, HttpMethod, string?> route)
    {
        var platform = new FakePlatformContext();
        services.AddSingleton<IPlatformContext>(platform);

        var http = new HttpClient(new FakeHandler(route))
        {
            BaseAddress = new Uri("https://test/")
        };
        services.AddSingleton(new EavApiClient(
            http, NullLogger<EavApiClient>.Instance));
        services.AddScoped<EntityTypeDisplayService>();
        services.AddScoped<IEavFieldValidator, EavFieldValidator>();

        return platform;
    }

    /// <summary>常用 canned JSON 片段。</summary>
    public static class Json
    {
        private static readonly JsonSerializerOptions Web =
            new(JsonSerializerDefaults.Web);

        public static string Schema() => Serialize(new
        {
            attributes = new object[]
            {
                new
                {
                    attributeName = "name",
                    displayName = "名称",
                    dataType = "string",
                    isRequired = false,
                    isSearchable = true,
                    isSortable = true,
                    displayOrder = 0
                }
            }
        });

        public static string Entity() => Serialize(new
        {
            entityId = "e1",
            entityType = "item",
            properties = new Dictionary<string, object?>(),
            updatedAt = (DateTimeOffset?)null
        });

        public static string Paged() => Serialize(new
        {
            items = new object[]
            {
                new
                {
                    entityId = "e1",
                    entityType = "item",
                    properties = new Dictionary<string, object?>
                    {
                        ["name"] = "测试项"
                    }
                }
            },
            total = 1,
            page = 1,
            pageSize = 20
        });

        public static string EmptyArray() => "[]";

        public static string Serialize(object obj)
            => JsonSerializer.Serialize(obj, Web);
    }

    private sealed class FakeHandler(
        Func<string, HttpMethod, string?> route) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = route(path, request.Method);
            return Task.FromResult(new HttpResponseMessage(
                body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    body ?? "", Encoding.UTF8, "application/json")
            });
        }
    }
}
