using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Dialogs;
using TreeGraph.Shared.EdgeSky;
using TreeGraph.Shared.EdgeSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.EdgeSky;

/// <summary>
/// EdgePropAddDialog 组件测试（新增属性对话框）。
///
/// 覆盖：空属性名拦截、NOT NULL 必配默认值（Nebula 约束）、非法默认值拦截、
/// 合法提交返回完整 EdgeTypePropDto（含默认值按类型解析）。
/// </summary>
public class EdgePropAddDialogTests : BunitTestBase
{
    public EdgePropAddDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddMudServices();
    }

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Ref)> OpenAsync()
    {
        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var reference = await ds.ShowAsync<EdgePropAddDialog>(string.Empty, new DialogOptions());
        provider.Render();
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(20);
            provider.Render();
        }
        return (provider, reference);
    }

    private static void SetField(IRenderedComponent<MudDialogProvider> provider, string field, object? value)
    {
        var instance = provider.FindComponent<EdgePropAddDialog>().Instance;
        typeof(EdgePropAddDialog)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);
    }

    private static void ClickSubmit(IRenderedComponent<MudDialogProvider> provider)
        => provider.FindAll("button").First(b => b.TextContent.Trim() == "添加").Click();

    [Fact]
    public async Task EmptyPropName_BlocksSubmit()
    {
        var (provider, reference) = await OpenAsync();

        ClickSubmit(provider);

        Assert.False(reference.Result.IsCompleted);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, s => s.Message.Contains("属性名不能为空"));
    }

    [Fact]
    public async Task NotNullWithoutDefault_BlocksSubmit()
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "level");
        SetField(provider, "_nullable", false);
        provider.Render();

        ClickSubmit(provider);

        Assert.False(reference.Result.IsCompleted);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars,
            s => s.Message.Contains("NOT NULL") && s.Message.Contains("默认值"));
    }

    [Fact]
    public async Task InvalidIntDefault_BlocksSubmit()
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "level");
        SetField(provider, "_dataType", EdgePropDataTypes.Int);
        SetField(provider, "_nullable", false);
        SetField(provider, "_defaultText", "abc");
        provider.Render();

        ClickSubmit(provider);

        Assert.False(reference.Result.IsCompleted);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, s => s.Message.Contains("不是合法的 INT"));
    }

    [Fact]
    public async Task Valid_ReturnsPropDto_WithParsedDefault()
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "speed");
        SetField(provider, "_displayName", "限速");
        SetField(provider, "_dataType", EdgePropDataTypes.Int);
        SetField(provider, "_nullable", false);
        SetField(provider, "_defaultText", "60");
        provider.Render();

        ClickSubmit(provider);

        var result = await reference.Result;
        var prop = Assert.IsType<EdgeTypePropDto>(result.Data);
        Assert.Equal("speed", prop.PropName);
        Assert.Equal("限速", prop.DisplayName);
        Assert.Equal(EdgePropDataTypes.Int, prop.DataType);
        Assert.False(prop.Nullable);
        Assert.Equal(60, prop.DefaultValue!.Value.GetInt64());
    }

    [Fact]
    public async Task Nullable_NoDefault_ReturnsNullDefault()
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "remark");
        SetField(provider, "_dataType", EdgePropDataTypes.String);
        provider.Render();

        ClickSubmit(provider);

        var result = await reference.Result;
        var prop = Assert.IsType<EdgeTypePropDto>(result.Data);
        Assert.Equal("remark", prop.PropName);
        Assert.Null(prop.DisplayName);
        Assert.True(prop.Nullable);
        Assert.Null(prop.DefaultValue);
    }

    [Theory]
    [InlineData("3.14159265358979", 3.14159265358979)]  // 双精度：小数位不丢失
    [InlineData("1.5e3", 1500.0)]                        // 科学计数法
    [InlineData("-0.5", -0.5)]                           // 负数
    public async Task DoubleDefault_PreservesPrecision(string text, double expected)
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "ratio");
        SetField(provider, "_dataType", EdgePropDataTypes.Double);
        SetField(provider, "_nullable", false);
        SetField(provider, "_defaultText", text);
        provider.Render();

        ClickSubmit(provider);

        var result = await reference.Result;
        var prop = Assert.IsType<EdgeTypePropDto>(result.Data);
        Assert.Equal(expected, prop.DefaultValue!.Value.GetDouble(), precision: 15);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    public async Task BoolDefault_AcceptsAnyCase(string text, bool expected)
    {
        var (provider, reference) = await OpenAsync();
        SetField(provider, "_propName", "enabled");
        SetField(provider, "_dataType", EdgePropDataTypes.Bool);
        SetField(provider, "_nullable", false);
        SetField(provider, "_defaultText", text);
        provider.Render();

        ClickSubmit(provider);

        var result = await reference.Result;
        var prop = Assert.IsType<EdgeTypePropDto>(result.Data);
        Assert.Equal(expected, prop.DefaultValue!.Value.GetBoolean());
    }
}
