using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.Blazor.Shared.MafWfSky;
using TreeGraph.Blazor.Shared.MafWfSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.MafWfSky;

/// <summary>
/// 命名生成客户端集成测试：TestServer 内存后端 + 真实信封解包链路，
/// 守护 NamingPath 路由、请求体形状与 ApiResponse 解包。
/// </summary>
public class NamingClientIntegrationTests
{
    private static (MafWfApiClient Client, IHost Host) CreateClient()
    {
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(_ => { });
            web.Configure(app =>
            {
                app.Run(async ctx =>
                {
                    if (ctx.Request.Path == "/api/naming/generate" &&
                        ctx.Request.Method == "POST")
                    {
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("""
                            {"success":true,"message":null,
                             "data":{"snakeCase":"user_id","pascalCase":"UserId",
                                     "warnings":["测试告警"]}}
                            """);
                        return;
                    }

                    ctx.Response.StatusCode = 404;
                });
            });
        });

        var host = builder.Start();
        var server = (TestServer)host.Services.GetRequiredService<IServer>();
        var http = server.CreateClient();
        var client = new MafWfApiClient(http, new MafWfSkyOptions(),
            NullLogger<MafWfApiClient>.Instance);

        return (client, host);
    }

    [Fact]
    public async Task GenerateNaming_解包信封_返回snake与pascal()
    {
        var (client, host) = CreateClient();
        using (host)
        {
            var result = await client.GenerateNamingAsync("用户ID");

            Assert.Equal("user_id", result.SnakeCase);
            Assert.Equal("UserId", result.PascalCase);
            Assert.Single(result.Warnings);
            Assert.Equal("测试告警", result.Warnings[0]);
        }
    }

    [Fact]
    public async Task GenerateNaming_非2xx_抛HttpRequestException()
    {
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(_ => { });
            web.Configure(app => app.Run(ctx =>
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    """{"success":false,"message":"请求参数有误","data":null}""");
            }));
        });

        using var host = builder.Start();
        var server = (TestServer)host.Services.GetRequiredService<IServer>();
        var http = server.CreateClient();
        var client = new MafWfApiClient(http, new MafWfSkyOptions(),
            NullLogger<MafWfApiClient>.Instance);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateNamingAsync("x"));

        Assert.Contains("请求参数有误", ex.Message);
    }
}
