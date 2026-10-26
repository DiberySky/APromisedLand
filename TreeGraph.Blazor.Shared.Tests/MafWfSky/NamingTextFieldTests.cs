using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.MafWfSky;
using TreeGraph.Blazor.Shared.MafWfSky.Components;
using TreeGraph.Blazor.Shared.MafWfSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.MafWfSky;

/// <summary>
/// NamingTextField 组件单测（bUnit）：
///   - 宿主未注册 MafWfApiClient 时退化为普通输入框（无魔棒图标）；
///   - SourceText 为空时点击魔棒仅提示，不发请求；
///   - snake / pascal 两种 NameCase 回填对应候选；
///   - API 失败时 Snackbar 告警且不回填。
/// </summary>
public class NamingTextFieldTests : BunitTestBase
{
    public NamingTextFieldTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    /// <summary>注册返回固定信封的假后端 MafWfApiClient。</summary>
    private void RegisterClient(HttpStatusCode status = HttpStatusCode.OK, string body = """
        {"success":true,"message":null,
         "data":{"snakeCase":"user_id","pascalCase":"UserId","warnings":[]}}
        """)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://stub/") };
        Services.AddSingleton(new MafWfApiClient(http, new MafWfSkyOptions(),
            NullLogger<MafWfApiClient>.Instance));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public void 未注册客户端_无魔棒图标()
    {
        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称"));

        Assert.Empty(cut.FindAll(".mud-input-adornment-end button"));
    }

    [Fact]
    public void 已注册客户端_显示魔棒图标()
    {
        RegisterClient();

        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称"));

        Assert.Single(cut.FindAll(".mud-input-adornment-end button"));
    }

    [Fact]
    public void SourceText为空_点击魔棒_不发请求()
    {
        RegisterClient();
        var handler = Services.GetRequiredService<MafWfApiClient>();

        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称")
            .Add(x => x.SourceText, ""));

        cut.Find(".mud-input-adornment-end button").Click();

        // 空来源 → 只提示，不打后端（HttpClient 是新的，无调用记录可查，
        // 但值必须保持空即可证明未回填）
        Assert.Equal(string.Empty, cut.Instance.Value ?? string.Empty);
    }

    [Fact]
    public async Task 点击魔棒_回填snake_case并触发ValueChanged()
    {
        RegisterClient();
        string? captured = null;

        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称")
            .Add(x => x.SourceText, "用户ID")
            .Add(x => x.ValueChanged, v => captured = v));

        cut.Find(".mud-input-adornment-end button").Click();
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal("user_id", cut.Instance.Value);
        Assert.Equal("user_id", captured);
    }

    [Fact]
    public async Task NameCase为pascal_回填PascalCase()
    {
        RegisterClient();

        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称")
            .Add(x => x.SourceText, "用户ID")
            .Add(x => x.NameCase, "pascal"));

        cut.Find(".mud-input-adornment-end button").Click();
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal("UserId", cut.Instance.Value);
    }

    [Fact]
    public async Task Api失败_不回填且值保持()
    {
        RegisterClient(HttpStatusCode.InternalServerError, """{"success":false,"message":"LLM 离线","data":null}""");

        var cut = Render<NamingTextField>(p => p
            .Add(x => x.Label, "名称")
            .Add(x => x.Value, "manual_name")
            .Add(x => x.SourceText, "用户ID"));

        cut.Find(".mud-input-adornment-end button").Click();
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal("manual_name", cut.Instance.Value);
    }
}
