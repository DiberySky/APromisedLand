using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Components;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.FieldRenderers;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// relation 字段前端（R6）：RelationField 渲染 / 只读回显 / 清空，
/// 以及 DynamicForm 对 relation 的初始值解析与提交格式。
/// </summary>
public class RelationFieldTests : BunitTestBase
{
    private static readonly JsonSerializerOptions WebOpt =
        new(JsonSerializerDefaults.Web);

    private const string TargetId = "11111111-2222-3333-4444-555555555555";

    public RelationFieldTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new EavApiClient(
            new HttpClient(new NodeStub()) { BaseAddress = new Uri("http://api.test/") },
            NullLogger<EavApiClient>.Instance));
        // RelationField 的「语义搜索选节点」按钮依赖对话框服务（本测试不触发点击）
        Services.AddSingleton<IDialogService>(Mock.Of<IDialogService>());
        Services.AddSingleton<BlazorService>();
    }

    private static AttributeSchemaDto RelationAttr() =>
        new("rel", "关联设备", EavDataTypes.Relation,
            false, false, false, 10, null, null, null,
            RelationType: "belongs_to");

    private static EavRelationValue Value(string name = "主供") =>
        new() { ToNodeId = TargetId, Name = name };

    [Fact]
    public void Editable_RendersLabelAndTargetPicker()
    {
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
        });

        Assert.Contains("关联设备", cut.Markup);
        Assert.Contains("（图边：belongs_to）", cut.Markup);
        Assert.Contains("目标节点", cut.Markup);
        Assert.Contains("关系名称（可选）", cut.Markup);
        Assert.Contains("清空", cut.Markup);
    }

    [Fact]
    public void ReadOnly_WithValue_ShowsNameAndTargetName()
    {
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
            p.Add(x => x.Value, Value());
            p.Add(x => x.ReadOnly, true);
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("名称：主供", cut.Markup);
            Assert.Contains("→ 目标：一号站", cut.Markup);
        });
        Assert.DoesNotContain("保存", cut.Markup);
    }

    [Fact]
    public void ReadOnly_WithoutValue_ShowsNotSet()
    {
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
            p.Add(x => x.ReadOnly, true);
        });

        Assert.Contains("未设置关系", cut.Markup);
    }

    [Fact]
    public void ClearButton_InvokesValueChangedWithNull()
    {
        EavRelationValue? received = Value();
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
            p.Add(x => x.Value, Value());
            p.Add(x => x.ValueChanged,
                EventCallback.Factory.Create<EavRelationValue?>(this, v => received = v));
        });

        cut.FindAll("button")
            .Single(b => b.TextContent.Contains("清空"))
            .Click();

        Assert.Null(received);
    }

    [Fact]
    public void DynamicForm_RelationInitialValue_RoundTripsOnSubmit()
    {
        Dictionary<string, object?>? submitted = null;
        var initial = new Dictionary<string, JsonElement>
        {
            ["rel"] = JsonSerializer.SerializeToElement(
                new { valueId = "v-1", toNodeId = TargetId, name = "主供" }, WebOpt)
        };

        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "reltest");
            p.Add(x => x.EntityId, "e-1");
            p.Add(x => x.Schema, new[] { RelationAttr() });
            p.Add(x => x.InitialValues, initial);
            p.Add(x => x.OnSubmit,
                EventCallback.Factory.Create<Dictionary<string, object?>>(
                    this, values => submitted = values));
        });

        cut.FindAll("button")
            .Single(b => b.TextContent.Contains("保存"))
            .Click();

        cut.WaitForAssertion(() => Assert.NotNull(submitted));
        var rel = Assert.IsType<EavRelationValue>(submitted!["rel"]);
        Assert.Equal(TargetId, rel.ToNodeId);
        Assert.Equal("主供", rel.Name);
    }

    [Fact]
    public void Editable_WithValue_ShowsSelectedTargetAndName()
    {
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
            p.Add(x => x.Value, Value());
        });

        cut.WaitForAssertion(() =>
        {
            var inputs = cut.FindAll("input");
            Assert.Contains(inputs, i => i.GetAttribute("value") == "一号站");
            Assert.Contains(inputs, i => i.GetAttribute("value") == "主供");
        });
    }

    [Fact]
    public void ClearButton_IsDisabled_WhenNothingSet()
    {
        var cut = Render<RelationField>(p =>
        {
            p.Add(x => x.Attr, RelationAttr());
        });

        var clear = cut.FindAll("button")
            .Single(b => b.TextContent.Contains("清空"));
        Assert.True(clear.HasAttribute("disabled"));
    }

    [Fact]
    public void DynamicForm_ReadOnly_ShowsRelationWithoutSave()
    {
        var initial = new Dictionary<string, JsonElement>
        {
            ["rel"] = JsonSerializer.SerializeToElement(
                new { valueId = "v-1", toNodeId = TargetId, name = "主供" }, WebOpt)
        };

        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "reltest");
            p.Add(x => x.EntityId, "e-1");
            p.Add(x => x.Schema, new[] { RelationAttr() });
            p.Add(x => x.InitialValues, initial);
            p.Add(x => x.ReadOnly, true);
        });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("→ 目标：一号站", cut.Markup);
            Assert.DoesNotContain(
                "保存", cut.FindAll("button").Select(b => b.TextContent).Aggregate("", (a, b) => a + b));
        });
    }

    /// <summary>节点查询桩：api/node/{id} 与 api/node?search= 返回统一信封。</summary>
    private sealed class NodeStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            object payload = path.StartsWith("/api/node")
                ? new
                {
                    success = true,
                    message = (string?)null,
                    data = path.Contains("/api/node/")
                        ? (object)new { id = TargetId, name = "一号站", hostType = "Node" }
                        : new[]
                        {
                            new { id = TargetId, name = "一号站", hostType = "Node" }
                        }
                }
                : new { success = true, message = (string?)null, data = (object?)null };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            });
        }
    }
}
