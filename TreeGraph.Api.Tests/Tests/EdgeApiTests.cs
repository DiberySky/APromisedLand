using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// EdgeSky 顶级边 API（镜像 NebulaGraph 语义）：
///   - EdgeType：命名规则、NOT NULL 必配 DEFAULT、TTL 成对、重名 409、ALTER 校验、
///     有实例拒删 409、名称不可改；
///   - Edge 实例：四元组 &lt;edgeType, srcVid, rank, dstVid&gt; 唯一标识、
///     UPSERT 覆盖语义、PATCH 部分更新、Schema 校验、两端节点必须存在。
/// </summary>
public class EdgeApiTests : IntegrationTestBase
{
    public EdgeApiTests(EavApiFactory factory) : base(factory) { }

    // ============================================================
    // EdgeType CRUD 与校验
    // ============================================================

    [Fact]
    public async Task EdgeType_CreateThenGet_Roundtrip()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            comment = "测试边类型",
            props = new object[]
            {
                new { propName = "name", dataType = "STRING", nullable = true },
                new { propName = "weight", dataType = "INT", nullable = false,
                      defaultValue = 0 }
            }
        });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        var dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        Assert.NotNull(dto);
        Assert.Equal(name, dto!.Name);
        Assert.Equal("测试边类型", dto.Comment);
        Assert.Equal(2, dto.Props.Count);
        var weight = Assert.Single(dto.Props, p => p.PropName == "weight");
        Assert.False(weight.Nullable);
        Assert.Equal("INT", weight.DataType);
    }

    [Fact]
    public async Task EdgeType_DuplicateName_Returns409()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var first = await Client.PostAsJsonAsync($"{EdgeBase}/types", new { name });
        first.EnsureSuccessStatusCode();

        var dup = await Client.PostAsJsonAsync($"{EdgeBase}/types", new { name });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Theory]
    [InlineData("1abc")]     // 数字开头
    [InlineData("has-dash")] // 非法字符
    [InlineData("中文名")]    // 非 ASCII
    public async Task EdgeType_InvalidName_Returns400(string badName)
    {
        var resp = await Client.PostAsJsonAsync($"{EdgeBase}/types", new { name = badName });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task EdgeType_NotNullWithoutDefault_Returns400()
    {
        var resp = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name = $"et_{Guid.NewGuid():N}",
            props = new[] { new { propName = "weight", dataType = "INT", nullable = false } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("NOT NULL", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EdgeType_TtlMustPair_AndColumnTypeRestricted()
    {
        // 只有 TTL 列没有时长 → 400
        var half = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name = $"et_{Guid.NewGuid():N}",
            ttlCol = "created",
            props = new[] { new { propName = "created", dataType = "TIMESTAMP", nullable = true } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);

        // TTL 列类型为 STRING → 400
        var badType = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name = $"et_{Guid.NewGuid():N}",
            ttlCol = "name",
            ttlDurationSeconds = 60,
            props = new[] { new { propName = "name", dataType = "STRING", nullable = true } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Contains("INT/TIMESTAMP", await badType.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EdgeType_Alter_AddAndDropProp()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            props = new[] { new { propName = "a", dataType = "STRING", nullable = true } }
        });
        create.EnsureSuccessStatusCode();

        // ADD 新属性 + DROP 旧属性
        var alter = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}", new
        {
            addProps = new[] { new { propName = "b", dataType = "DOUBLE", nullable = true } },
            dropProps = new[] { "a" }
        });
        Assert.True(alter.IsSuccessStatusCode, await alter.Content.ReadAsStringAsync());

        var dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        var prop = Assert.Single(dto!.Props);
        Assert.Equal("b", prop.PropName);

        // DROP 不存在的属性 → 400
        var bad = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}",
            new { dropProps = new[] { "not_exist" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task EdgeProp_DisplayName_PatchAndClear()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            props = new[] { new { propName = "a", dataType = "STRING", nullable = true } }
        });
        create.EnsureSuccessStatusCode();

        // 设置属性中文显示名
        var patch = await Client.PatchAsJsonAsync(
            $"{EdgeBase}/types/{name}/props/a/display-name", new { displayName = "限速" });
        patch.EnsureSuccessStatusCode();
        var dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        Assert.Equal("限速", dto!.Props.Single().DisplayName);

        // 空白 = 清除
        var clear = await Client.PatchAsJsonAsync(
            $"{EdgeBase}/types/{name}/props/a/display-name", new { displayName = " " });
        clear.EnsureSuccessStatusCode();
        dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        Assert.Null(dto!.Props.Single().DisplayName);

        // 不存在的属性 → 404
        var missing = await Client.PatchAsJsonAsync(
            $"{EdgeBase}/types/{name}/props/nope/display-name", new { displayName = "x" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task EdgeType_AlterUpdateProps_NotNullWithoutDefault_Returns400()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            props = new[] { new { propName = "a", dataType = "STRING", nullable = true } }
        });
        create.EnsureSuccessStatusCode();

        // 改 NOT NULL 但未配默认值 → 400（Nebula 约束）；类型不可变更（无对应请求字段）
        var resp = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}", new
        {
            updateProps = new[] { new { propName = "a", nullable = false } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task EdgeType_AlterUpdateProps_NullableAndDefault()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            props = new[]
            {
                new { propName = "a", dataType = "INT", nullable = true },
                new { propName = "b", dataType = "STRING", nullable = true }
            }
        });
        create.EnsureSuccessStatusCode();

        // a: NULL → NOT NULL（配默认值 0）；b: 设置默认值
        var alter = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}", new
        {
            updateProps = new object[]
            {
                new { propName = "a", nullable = false, defaultValueSet = true, defaultValue = 0L },
                new { propName = "b", defaultValueSet = true, defaultValue = "x" }
            }
        });
        alter.EnsureSuccessStatusCode();

        var dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        var a = dto!.Props.Single(p => p.PropName == "a");
        Assert.False(a.Nullable);
        Assert.Equal(0L, a.DefaultValue!.Value.GetInt64());
        var b = dto.Props.Single(p => p.PropName == "b");
        Assert.True(b.Nullable);
        Assert.Equal("x", b.DefaultValue!.Value.GetString());

        // NOT NULL 不配置默认值 → 400（Nebula 约束）
        var bad = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}", new
        {
            updateProps = new[]
            {
                new { propName = "b", nullable = false, defaultValueSet = true, defaultValue = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // b: 清除默认值（可空属性允许）
        var clear = await Client.PutAsJsonAsync($"{EdgeBase}/types/{name}", new
        {
            updateProps = new[]
            {
                new { propName = "b", defaultValueSet = true, defaultValue = (string?)null }
            }
        });
        clear.EnsureSuccessStatusCode();
        dto = await Client.GetEavAsync<EdgeTypeDto>($"{EdgeBase}/types/{name}");
        Assert.Null(dto!.Props.Single(p => p.PropName == "b").DefaultValue);
    }

    [Fact]
    public async Task EdgeType_Delete_WithInstances_Returns409_ThenDelete()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync($"{EdgeBase}/types", new { name });
        create.EnsureSuccessStatusCode();

        var (src, dst) = await CreateNodePairAsync();
        var put = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}", new { });
        put.EnsureSuccessStatusCode();

        // 仍有实例 → 409
        Assert.Equal(HttpStatusCode.Conflict,
            (await Client.DeleteAsync($"{EdgeBase}/types/{name}")).StatusCode);

        // 删实例后 → 可删；再 GET → 404
        Assert.Equal(HttpStatusCode.OK,
            (await Client.DeleteAsync($"{EdgeBase}/edges/{name}/{src}/0/{dst}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Client.DeleteAsync($"{EdgeBase}/types/{name}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"{EdgeBase}/types/{name}")).StatusCode);
    }

    // ============================================================
    // Edge 实例：UPSERT 覆盖 / PATCH / 校验 / 删除
    // ============================================================

    [Fact]
    public async Task Edge_Upsert_IsOverwriteSemantics()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, dst) = await CreateNodePairAsync();

        // 第一次写入两个属性
        var first = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}",
            new { props = new Dictionary<string, object?> { ["name"] = "一段", ["weight"] = 3 } });
        first.EnsureSuccessStatusCode();

        // 第二次只写 weight → name 落 NULL（覆盖语义，对齐 INSERT EDGE）
        var second = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}",
            new { props = new Dictionary<string, object?> { ["weight"] = 5 } });
        second.EnsureSuccessStatusCode();

        var dto = await Client.GetEavAsync<EdgeDto>(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}");
        Assert.NotNull(dto);
        Assert.Equal(5, dto!.Props!["weight"].GetInt32());
        Assert.False(dto.Props!.ContainsKey("name"));
    }

    [Fact]
    public async Task Edge_Patch_PartialUpdate_KeepsOtherProps()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, dst) = await CreateNodePairAsync();
        var put = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}",
            new { props = new Dictionary<string, object?> { ["name"] = "一段", ["weight"] = 3 } });
        put.EnsureSuccessStatusCode();

        // PATCH 只改 weight；再 PATCH name=null 清除该属性
        var patch = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Patch, $"{EdgeBase}/edges/{name}/{src}/0/{dst}")
        {
            Content = JsonContent.Create(
                new { props = new Dictionary<string, object?> { ["weight"] = 9 } })
        });
        patch.EnsureSuccessStatusCode();

        var dto = await Client.GetEavAsync<EdgeDto>(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}");
        Assert.Equal(9, dto!.Props!["weight"].GetInt32());
        Assert.Equal("一段", dto.Props!["name"].GetString());

        var clear = await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Patch, $"{EdgeBase}/edges/{name}/{src}/0/{dst}")
        {
            Content = JsonContent.Create(
                new { props = new Dictionary<string, object?> { ["name"] = null } })
        });
        clear.EnsureSuccessStatusCode();
        var after = await Client.GetEavAsync<EdgeDto>(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}");
        Assert.False(after!.Props!.ContainsKey("name"));
        Assert.Equal(9, after.Props!["weight"].GetInt32());
    }

    [Fact]
    public async Task Edge_Rank_DistinguishesParallelEdges()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, dst) = await CreateNodePairAsync();

        foreach (var rank in new[] { 0L, 1L })
        {
            var resp = await Client.PutAsJsonAsync(
                $"{EdgeBase}/edges/{name}/{src}/{rank}/{dst}",
                new { props = new Dictionary<string, object?> { ["weight"] = rank } });
            resp.EnsureSuccessStatusCode();
        }

        var list = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?edgeType={name}&srcVid={src}");
        Assert.Equal(2, list!.Count);
        Assert.Equal([0L, 1L], list.Select(e => e.Rank).ToArray());
    }

    [Fact]
    public async Task Edge_SchemaValidation_UnknownKeyOrBadType_Returns400()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, dst) = await CreateNodePairAsync();

        // 未定义属性键 → 400
        var unknown = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}",
            new { props = new Dictionary<string, object?> { ["nope"] = 1 } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        // 类型不符（INT 列给字符串）→ 400
        var badType = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{dst}",
            new { props = new Dictionary<string, object?> { ["weight"] = "abc" } });
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
    }

    [Fact]
    public async Task Edge_EndpointValidation_MissingNode_Returns400()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, _) = await CreateNodePairAsync();

        var resp = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/{name}/{src}/0/{Guid.NewGuid()}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("不存在于 nodes", await resp.Content.ReadAsStringAsync());

        // 未知边类型 → 404
        var noType = await Client.PutAsJsonAsync(
            $"{EdgeBase}/edges/et_missing_{Guid.NewGuid():N}/{src}/0/{src}", new { });
        Assert.Equal(HttpStatusCode.NotFound, noType.StatusCode);
    }

    [Fact]
    public async Task Edge_GetDelete_Unknown_Returns404()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (src, dst) = await CreateNodePairAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"{EdgeBase}/edges/{name}/{src}/0/{dst}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.DeleteAsync($"{EdgeBase}/edges/{name}/{src}/0/{dst}")).StatusCode);
    }

    [Fact]
    public async Task Edge_Query_ByNodeDirection_FiltersAndFillsNodeNames()
    {
        var name = await CreateEdgeTypeWithPropsAsync();
        var (a, b) = await CreateNodePairAsync();
        var (_, c) = await CreateNodePairAsync();

        // 关系只依赖 nodes 表存在性：a→b（用 b 作为中间节点）、b→c
        var ab = await Client.PutAsJsonAsync($"{EdgeBase}/edges/{name}/{a}/0/{b}", new { });
        ab.EnsureSuccessStatusCode();
        var bc = await Client.PutAsJsonAsync($"{EdgeBase}/edges/{name}/{b}/0/{c}", new { });
        bc.EnsureSuccessStatusCode();

        var outEdges = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?nodeId={b}&direction=out");
        Assert.Single(outEdges!);
        Assert.Equal(c, outEdges![0].DstVid);

        var inEdges = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?nodeId={b}&direction=in");
        Assert.Single(inEdges!);
        Assert.Equal(a, inEdges![0].SrcVid);

        var either = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?nodeId={b}&direction=either");
        Assert.Equal(2, either!.Count);

        // 对端名称由 join nodes 补充
        Assert.Equal($"节点{a[..4]}", either!.Single(e => e.SrcVid == a).SrcName);
        Assert.Equal($"节点{c[..4]}", either!.Single(e => e.DstVid == c).DstName);

        // 分页
        var page1 = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?nodeId={b}&direction=either&limit=1&offset=0");
        var page2 = await Client.GetEavAsync<List<EdgeDto>>(
            $"{EdgeBase}/edges?nodeId={b}&direction=either&limit=1&offset=1");
        Assert.Single(page1!);
        Assert.Single(page2!);
        Assert.NotEqual(page1![0].SrcVid, page2![0].SrcVid);
    }

    [Fact]
    public async Task Node_Space_LineNode_ResolvesToLineSpace()
    {
        var (src, _) = await CreateNodePairAsync();

        var raw = await Client.GetAsync($"/api/edge/node-space/{src}");
        var body = await raw.Content.ReadAsStringAsync();
        Assert.True(raw.IsSuccessStatusCode, $"{(int)raw.StatusCode}: {body}");

        var dto = await Client.GetEavAsync<NodeSpaceDto>($"/api/edge/node-space/{src}");
        Assert.NotNull(dto);
        Assert.Equal(TestSpaceId, dto!.SpaceId);
        Assert.False(string.IsNullOrEmpty(dto.SpaceType));

        // 未注册的节点 id → 404
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/api/edge/node-space/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task UnitTree_ParentChild_SyncsParentOfEdge_AndCascadesOnDelete()
    {
        const string unitSpaceId = "9c3a5b7f-1d4e-6c0a-3b2f-5e7d9a1c3e4f";
        var unitEdgeBase = $"/api/spaces/{unitSpaceId}/edge";

        var parentId = $"P_{Guid.NewGuid():N}";
        var newParentId = $"N_{Guid.NewGuid():N}";
        var childId = $"C_{Guid.NewGuid():N}";

        async Task CreateNodeAsync(string id, string? parentId)
        {
            var resp = await Client.PostAsJsonAsync("/UnitTree", new
            {
                id, text = "节点", parentId, sortOrder = 0, canHaveChildren = true
            });
            resp.EnsureSuccessStatusCode();
        }

        await CreateNodeAsync(parentId, null);
        await CreateNodeAsync(childId, parentId);

        // 树服务写路径直接落 parent_of 边（不经事件/GraphSync 管道），创建后立即可查
        var inEdges = await Client.GetEavAsync<List<EdgeDto>>(
            $"{unitEdgeBase}/edges?nodeId={childId}&direction=in");
        Assert.Single(inEdges!);
        Assert.Equal("parent_of", inEdges![0].EdgeType);
        Assert.Equal(parentId, inEdges[0].SrcVid);
        Assert.Equal(0L, inEdges[0].Rank);

        // 换父：旧父边删除、新父边写入
        await CreateNodeAsync(newParentId, null);
        var move = await Client.PostAsync(
            $"/UnitTree/move?nodeId={childId}&newParentId={newParentId}", null);
        Assert.True(move.IsSuccessStatusCode, await move.Content.ReadAsStringAsync());

        var moved = await Client.GetEavAsync<List<EdgeDto>>(
            $"{unitEdgeBase}/edges?nodeId={childId}&direction=in");
        Assert.Single(moved!);
        Assert.Equal(newParentId, moved![0].SrcVid);

        // 删除节点：边随节点级联删除，删除本身不被 FK 阻塞
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/UnitTree/{childId}")).StatusCode);
        var afterDelete = await Client.GetEavAsync<List<EdgeDto>>(
            $"{unitEdgeBase}/edges?nodeId={childId}&direction=either");
        Assert.Empty(afterDelete!);

        await Client.DeleteAsync($"/UnitTree/{newParentId}");
        await Client.DeleteAsync($"/UnitTree/{parentId}");
    }

    // ============================================================
    // helpers
    // ============================================================

    /// <summary>建一个含 name STRING NULL / weight INT NULL 的边类型。</summary>
    private async Task<string> CreateEdgeTypeWithPropsAsync()
    {
        var name = $"et_{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync($"{EdgeBase}/types", new
        {
            name,
            props = new[]
            {
                new { propName = "name", dataType = "STRING", nullable = true },
                new { propName = "weight", dataType = "INT", nullable = true }
            }
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
        return name;
    }

    /// <summary>建一条线路 + 两个线路节点作为合法边端点。</summary>
    private async Task<(string Src, string Dst)> CreateNodePairAsync()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var line = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "边测试线路", spaceId = TestSpaceId });
        line.EnsureSuccessStatusCode();

        var src = $"{Guid.NewGuid()}";
        var dst = $"{Guid.NewGuid()}";
        foreach (var id in new[] { src, dst })
        {
            var resp = await Client.PostAsJsonAsync("/api/LineNode", new
            {
                id, lineId, name = $"节点{id[..4]}",
                description = (string?)null, predecessorId = (string?)null
            });
            resp.EnsureSuccessStatusCode();
        }
        return (src, dst);
    }
}
