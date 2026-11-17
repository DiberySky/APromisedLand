using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

public class EavApiClientSyncServicesTests
{
    [Fact]
    public async Task 搜索向量对账与图同步走SyncServices_业务查询仍走核心()
    {
        var coreHandler = new RecordingHandler();
        var syncHandler = new RecordingHandler();
        using var coreHttp = new HttpClient(coreHandler)
        {
            BaseAddress = new Uri("http://core.local:5773/")
        };
        using var syncHttp = new HttpClient(syncHandler)
        {
            BaseAddress = new Uri("http://sync.local:5775/")
        };
        var client = new EavApiClient(
            coreHttp, syncHttp, NullLogger<EavApiClient>.Instance);

        await client.SemanticSearchAsync(new SemanticSearchRequest { Query = "测试" });
        await client.ReconcileVectorsAsync(category: "tree");
        await client.StreamReconcileAsync(category: "tree");
        await client.SyncGraphCategoryAsync("tree");
        await client.SyncSpaceGraphAsync("space-1");
        await client.ListEntityTypesAsync();

        var coreRequest = Assert.Single(coreHandler.Requests);
        Assert.Equal("core.local", coreRequest.Host);
        Assert.Equal(5773, coreRequest.Port);
        Assert.Equal("/api/eav/entity-types", coreRequest.AbsolutePath);

        Assert.Equal(
            [
                "/api/search/semantic",
                "/api/search/reconcile",
                "/api/search/reconcile/stream",
                "/api/graph-sync/category/tree",
                "/api/graph-sync/space/space-1"
            ],
            syncHandler.Requests.Select(r => r.AbsolutePath));
        Assert.All(syncHandler.Requests, request =>
        {
            Assert.Equal("sync.local", request.Host);
            Assert.Equal(5775, request.Port);
        });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var isStream = request.RequestUri.AbsolutePath.EndsWith(
                "/reconcile/stream", StringComparison.Ordinal);
            var body = isStream
                ? """{"stage":"summary","active":0,"existing":0,"missing":0,"orphaned":0,"fixedCount":0,"deletedCount":0,"failedCount":0}"""
                : """{"success":true,"data":null}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
