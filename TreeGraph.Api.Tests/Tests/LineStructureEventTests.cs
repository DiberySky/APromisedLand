using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R5 线路入图：LineSky 结构变更事件发布（真实 PG 容器 + 记录型 IEventPublisher）。
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
    public async Task CreateStructure_PublishesLineStructureChangedEvent()
    {
        var (lineId, nodeIds) = await CreateLineWithNodesAsync(2);
        _factory.Publisher.Clear();

        var req = new
        {
            topologyMode = TopologyMode.Linear,
            segments = new[] { new { fromNodeId = nodeIds[0], toNodeId = nodeIds[1] } }
        };
        var resp = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure", req);
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        var events = _factory.Publisher.Of<LineStructureChangedEvent>();
        var evt = Assert.Single(events);
        Assert.Equal(lineId, evt.LineId);
    }

    [Fact]
    public async Task ClearStructure_PublishesLineStructureChangedEvent()
    {
        var (lineId, nodeIds) = await CreateLineWithNodesAsync(2);

        // 先建结构
        var create = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new
            {
                topologyMode = TopologyMode.Linear,
                segments = new[] { new { fromNodeId = nodeIds[0], toNodeId = nodeIds[1] } }
            });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        _factory.Publisher.Clear();

        // 清空结构（PUT 空段）
        var clear = await _client.PutAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new { topologyMode = TopologyMode.Network, segments = Array.Empty<object>() });
        Assert.True(clear.IsSuccessStatusCode, await clear.Content.ReadAsStringAsync());

        var events = _factory.Publisher.Of<LineStructureChangedEvent>();
        var evt = Assert.Single(events);
        Assert.Equal(lineId, evt.LineId);
    }

    [Fact]
    public async Task AddSegment_PublishesLineStructureChangedEvent()
    {
        var (lineId, nodeIds) = await CreateLineWithNodesAsync(3);

        var create = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure",
            new
            {
                topologyMode = TopologyMode.Network,
                segments = new[] { new { fromNodeId = nodeIds[0], toNodeId = nodeIds[1] } }
            });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        _factory.Publisher.Clear();

        var add = await _client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new
            {
                fromNodeId = nodeIds[1],
                toNodeId = nodeIds[2],
                weight = 3.5m,
                displayName = "新增段"
            });
        Assert.True(add.IsSuccessStatusCode, await add.Content.ReadAsStringAsync());

        var events = _factory.Publisher.Of<LineStructureChangedEvent>();
        var evt = Assert.Single(events);
        Assert.Equal(lineId, evt.LineId);
    }

    // ============================================================
    // helpers
    // ============================================================

    private async Task<(string LineId, List<string> NodeIds)> CreateLineWithNodesAsync(
        int count, string? existingLineId = null)
    {
        var lineId = existingLineId ?? Guid.NewGuid().ToString("N");
        if (existingLineId is null)
        {
            var lineResp = await _client.PostAsJsonAsync(
                "/api/Lines", new Line { Id = lineId, Name = "事件测试线路" });
            Assert.True(lineResp.IsSuccessStatusCode,
                $"创建线路失败: {(int)lineResp.StatusCode} {await lineResp.Content.ReadAsStringAsync()}");
        }

        var nodeIds = new List<string>(count);
        for (var i = 0; i < count; i++)
            nodeIds.Add(await AddLineNodeAsync(lineId, $"节点{i}"));

        return (lineId, nodeIds);
    }

    private async Task<string> AddLineNodeAsync(string lineId, string name)
    {
        var nodeId = Guid.NewGuid().ToString("N");
        var resp = await _client.PostAsJsonAsync(
            "/api/LineNode",
            new LineNode { Id = nodeId, LineId = lineId, Name = name });
        Assert.True(resp.IsSuccessStatusCode,
            $"创建线路节点失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return nodeId;
    }
}
