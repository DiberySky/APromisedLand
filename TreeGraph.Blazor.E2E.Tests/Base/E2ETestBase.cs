using Microsoft.Playwright;
using System.Net.Http.Json;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Base;

[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly PlaywrightFixture Fixture;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected E2ETestBase(PlaywrightFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Context = await Fixture.NewContextAsync();

        // CI 中录制 Playwright Trace（失败时可在线回放）
        var isCi = Environment.GetEnvironmentVariable("CI") == "true";
        if (isCi)
        {
            await Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            });
        }

        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(Fixture.Settings.DefaultTimeoutMs);
    }

    public async Task DisposeAsync()
    {
        try
        {
            // ★ CI 下保存 trace 到文件（文件名含测试类名，便于定位）
            if (Environment.GetEnvironmentVariable("CI") == "true")
            {
                Directory.CreateDirectory("test-results/traces");
                var path = $"test-results/traces/" +
                           $"{GetType().Name}-{Guid.NewGuid():N}.zip";

                await Context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = path
                });
            }
        }
        catch
        {
            // trace 保存失败不阻断测试清理
        }

        if (Page is not null) await Page.CloseAsync();
        if (Context is not null) await Context.DisposeAsync();
    }

    /// <summary>生成唯一 iNode ID（避免测试间干扰）。</summary>
    protected static string NewInodeId() => Guid.NewGuid().ToString("D");

    /// <summary>
    /// 通过 API 建实体类型（供 E2E 前置准备）。
    /// 直接调 API 而不是 UI，避免"类型管理"页面成为 E2E 的前置依赖。
    /// 不指定 entityType → 服务端自动生成 et_xxx 标识并返回。
    /// </summary>
    protected async Task<(string Id, string Name)> CreateEntityTypeWithNameAsync(
        string displayName)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

        var resp = await http.PostAsJsonAsync("/api/eav/entity-types", new
        {
            displayName
        });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<TreeGraph.Shared.NodeEavSky.Dtos.ApiResponse<CreateTypeResponse>>();
        return (result!.Data!.EntityTypeId, result.Data.EntityType);
    }

    /// <summary>
    /// 通过 API 建属性（供 E2E 前置准备）。
    /// </summary>
    protected async Task CreateAttributeViaApiAsync(
        string entityType, string attributeName, string displayName,
        string dataType = "string",
        bool isSearchable = true)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

        var resp = await http.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType,
            attributeName,
            displayName,
            dataType,
            isSearchable,
            displayOrder = 1
        });
        resp.EnsureSuccessStatusCode();
    }

    private record CreateTypeResponse(string EntityTypeId, string EntityType);
}
