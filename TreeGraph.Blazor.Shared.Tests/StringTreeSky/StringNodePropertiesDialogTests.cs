using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.Tests.NodeEavSky;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.StringTreeSky;

/// <summary>
/// StringTree 节点 EAV 属性对话框测试。
/// 真实 EavApiClient + 按路由 canned JSON 的桩 Handler，
/// 验证空 schema / 有 schema / 已有值回显三种渲染状态。
/// 保存写链路由 Api 集成测试 + 真实 HTTP 端到端覆盖，此处不重复。
/// </summary>
public class StringNodePropertiesDialogTests : BunitTestBase
{
    private const string NodeId = "11111111-2222-3333-4444-555555555555";

    public StringNodePropertiesDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(
        string schemaBody, string entityBody)
    {
        NodeEavTestSetup.RegisterServices(Services, (path, method) =>
        {
            if (path.EndsWith("/schema", StringComparison.Ordinal))
                return schemaBody;
            if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                return entityBody;
            return null;
        });
        Services.AddScoped<NodeSchemaCache>();

        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var p = new DialogParameters
        {
            { nameof(StringNodePropertiesDialog.NodeId), NodeId },
            { nameof(StringNodePropertiesDialog.NodeName), "物品总类" }
        };
        await ds.ShowAsync<StringNodePropertiesDialog>("节点属性", p,
            new DialogOptions { FullWidth = true });
        provider.Render();
        return provider;
    }

    private static string SchemaJson(params (string Name, string Display, string Type)[] attrs)
        => NodeEavTestSetup.Json.Serialize(new
        {
            attributes = attrs.Select(a => new
            {
                attributeName = a.Name,
                displayName = a.Display,
                dataType = a.Type,
                isRequired = false,
                isSearchable = false,
                isSortable = false,
                displayOrder = 0
            }).ToArray()
        });

    private static string EmptySchemaJson() =>
        NodeEavTestSetup.Json.Serialize(new { attributes = Array.Empty<object>() });

    private static string EntityJson(string? note) =>
        NodeEavTestSetup.Json.Serialize(new
        {
            entityId = NodeId,
            entityType = "StringTreeNode",
            properties = note is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["note"] = note },
            updatedAt = (DateTimeOffset?)null
        });

    [Fact]
    public async Task EmptySchema_ShowsConfigHint()
    {
        var provider = await OpenAsync(EmptySchemaJson(), EntityJson(null));

        Assert.Contains("未定义动态属性", provider.Markup, StringComparison.Ordinal);
        Assert.Contains("StringTreeNode", provider.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithSchema_RendersFormWithSaveButton()
    {
        var provider = await OpenAsync(
            SchemaJson(("note", "备注", "string")), EntityJson(null));

        Assert.DoesNotContain("未定义动态属性", provider.Markup, StringComparison.Ordinal);
        var buttons = provider.FindAll("button");
        Assert.Contains(buttons, b => b.TextContent.Contains("保存", StringComparison.Ordinal));
        // 至少渲染出一个可编辑文本输入
        Assert.NotEmpty(provider.FindAll("input"));
    }

    [Fact]
    public async Task ExistingValue_IsLoadedIntoForm()
    {
        var provider = await OpenAsync(
            SchemaJson(("note", "备注", "string")), EntityJson("已有备注"));

        var input = provider.Find("input[type='text'], input:not([type])");
        Assert.Equal("已有备注", input.GetAttribute("value"));
    }

    [Fact]
    public async Task Schema_RequestedOnce_AcrossTwoDialogOpens()
    {
        var schemaCalls = 0;
        NodeEavTestSetup.RegisterServices(Services, (path, method) =>
        {
            if (path.EndsWith("/schema", StringComparison.Ordinal))
            {
                schemaCalls++;
                return SchemaJson(("note", "备注", "string"));
            }
            if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                return EntityJson(null);
            return null;
        });
        Services.AddScoped<NodeSchemaCache>();

        var provider = Render<MudDialogProvider>();
        var ds = Services.GetRequiredService<IDialogService>();
        var p = new DialogParameters
        {
            { nameof(StringNodePropertiesDialog.NodeId), NodeId },
            { nameof(StringNodePropertiesDialog.NodeName), "物品总类" }
        };

        await ds.ShowAsync<StringNodePropertiesDialog>("节点属性", p);
        provider.Render();
        Assert.Equal(1, schemaCalls);

        // 同一 Circuit 内第二次打开：命中共享 NodeSchemaCache，不再请求 schema
        await ds.ShowAsync<StringNodePropertiesDialog>("节点属性", p);
        provider.Render();
        Assert.Equal(1, schemaCalls);

        Assert.DoesNotContain("未定义动态属性", provider.Markup, StringComparison.Ordinal);
    }
}
