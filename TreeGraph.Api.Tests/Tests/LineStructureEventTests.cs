using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Messaging;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R7 线路结构事件：段边即 relation 值，结构变更经 EAV 写入链发布
/// RelationChangedEvent（Insert/Update/Delete）；原线路结构专用事件已删除。
/// 只验证「事件是否发出、载荷是否正确」，图内投影由 LineStructureSyncTests 覆盖。
/// </summary>
[Collection("LineStructureEvents")]
public class LineStructureEventTests
{
    private readonly RecordingApiFactory _factory;
    private readonly HttpClient _client;

    public LineStructureEventTests(RecordingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateStructure_PublishesRelationChangedInsert()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        _factory.Publisher.Clear();

        var resp = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new
            {
                segments = new[]
                {
                    new { fromNodeId = n[0], toNodeId = n[1] },
                    new { fromNodeId = n[1], toNodeId = n[2] }
                }
            });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        var events = _factory.Publisher.Of<RelationChangedEvent>();
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(RelationChangeKind.Insert, e.Kind);
            Assert.Equal("line_segment", e.RelationType);
            Assert.False(string.IsNullOrWhiteSpace(e.ValueId));
            Assert.False(string.IsNullOrWhiteSpace(e.AttributeId));
        });
        Assert.Contains(events, e => e.FromNodeId == n[0] && e.ToNodeId == n[1]);
        Assert.Contains(events, e => e.FromNodeId == n[1] && e.ToNodeId == n[2]);
    }

    [Fact]
    public async Task AddSegment_PublishesRelationChangedInsert()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        var create = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new { segments = new[] { new { fromNodeId = n[0], toNodeId = n[1] } } });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        _factory.Publisher.Clear();

        var add = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new { fromNodeId = n[1], toNodeId = n[2], displayName = "新增段" });
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());

        var evt = Assert.Single(_factory.Publisher.Of<RelationChangedEvent>());
        Assert.Equal(RelationChangeKind.Insert, evt.Kind);
        Assert.Equal("line_segment", evt.RelationType);
        Assert.Equal(n[1], evt.FromNodeId);
        Assert.Equal(n[2], evt.ToNodeId);
        Assert.Equal("新增段", evt.Name);
    }

    [Fact]
    public async Task DeleteSegment_PublishesRelationChangedDelete()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        _factory.Publisher.Clear();

        var create = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new { segments = new[] { new { fromNodeId = n[0], toNodeId = n[1] } } });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        // 取边实例 id（value_id）作为 edgeId
        var inserted = Assert.Single(_factory.Publisher.Of<RelationChangedEvent>());
        _factory.Publisher.Clear();

        var del = await _client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{inserted.ValueId}");
        Assert.True(del.IsSuccessStatusCode, await del.Content.ReadAsStringAsync());

        var evt = Assert.Single(_factory.Publisher.Of<RelationChangedEvent>());
        Assert.Equal(RelationChangeKind.Delete, evt.Kind);
        Assert.Equal("line_segment", evt.RelationType);
        Assert.Equal(inserted.ValueId, evt.ValueId);
        Assert.Equal(n[0], evt.FromNodeId);
        Assert.Equal(n[1], evt.ToNodeId);
    }

    [Fact]
    public async Task LineNodeCreate_WithPredecessor_PublishesRelationInsertAndInodeSaved()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(1);
        _factory.Publisher.Clear();

        var nodeId = $"{Guid.NewGuid()}";
        var resp = await _client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name = "次节点",
            description = (string?)null, predecessorId = n[0]
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        var rel = Assert.Single(_factory.Publisher.Of<RelationChangedEvent>());
        Assert.Equal(RelationChangeKind.Insert, rel.Kind);
        Assert.Equal("line_segment", rel.RelationType);
        Assert.Equal(n[0], rel.FromNodeId);
        Assert.Equal(nodeId, rel.ToNodeId);

        Assert.Contains(_factory.Publisher.Of<InodeChangedEvent>(),
            e => e.InodeId == nodeId && e.Kind == InodeChangeKind.Saved);
    }

    [Fact]
    public async Task LineNodeCreate_FirstNode_NoRelationEvent()
    {
        var lineId = await CreateLineAsync();
        _factory.Publisher.Clear();

        var nodeId = $"{Guid.NewGuid()}";
        var resp = await _client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name = "首节点",
            description = (string?)null, predecessorId = (string?)null
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        Assert.Empty(_factory.Publisher.Of<RelationChangedEvent>());
        Assert.Contains(_factory.Publisher.Of<InodeChangedEvent>(),
            e => e.InodeId == nodeId && e.Kind == InodeChangeKind.Saved);
    }

    // ============================================================
    // helpers
    // ============================================================

    private async Task<string> CreateLineAsync()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var resp = await _client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "事件测试线路" });
        Assert.True(resp.IsSuccessStatusCode,
            $"创建线路失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return lineId;
    }

    private async Task<(string LineId, List<string> NodeIds)> CreateLineWithNodesAsync(int count)
    {
        var lineId = await CreateLineAsync();
        var nodeIds = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var nodeId = $"{Guid.NewGuid()}";
            var resp = await _client.PostAsJsonAsync("/api/LineNode", new
            {
                id = nodeId, lineId, name = $"节点{i}",
                description = (string?)null, predecessorId = (string?)null
            });
            Assert.True(resp.IsSuccessStatusCode,
                $"创建线路节点失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
            nodeIds.Add(nodeId);
        }
        return (lineId, nodeIds);
    }
}
