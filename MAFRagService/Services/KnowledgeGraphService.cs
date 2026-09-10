using MAFRagService.Models;

namespace MAFRagService.Services;

public class KnowledgeGraphService
{
    private readonly NebulaGraphExecutor _executor;
    private readonly ILogger<KnowledgeGraphService> _logger;

    public KnowledgeGraphService(NebulaGraphExecutor executor, ILogger<KnowledgeGraphService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<GraphContext> GetGraphContextAsync(List<string> entities, string tenant)
    {
        var context = new GraphContext();
        if (entities.Count == 0) return context;

        var entityList = string.Join(", ", entities.Select(e => $"\"{e}\""));
        var ngql = $@"
            MATCH (e:Entity)-[:MENTIONS*1..2]-(related)
            WHERE e.name IN [{entityList}] AND e.tenant == ""{tenant}""
            RETURN e.name AS source, related.name AS target, type(edge) AS rel
            LIMIT 100
        ";

        var result = await _executor.ExecuteWithRetryAsync(ngql);
        if (result.IsSucceeded)
        {
            foreach (var record in result.Records)
            {
                context.Relations.Add(new GraphRelation
                {
                    Source = record.Values["source"].AsString(),
                    Target = record.Values["target"].AsString(),
                    Type = record.Values["rel"].AsString()
                });
                if (!context.PathNodes.Contains(record.Values["source"].AsString()))
                    context.PathNodes.Add(record.Values["source"].AsString());
                if (!context.PathNodes.Contains(record.Values["target"].AsString()))
                    context.PathNodes.Add(record.Values["target"].AsString());
            }
        }
        else
        {
            _logger.LogError("Graph query failed: {Error}", result.ErrorMessage);
        }
        return context;
    }

    public async Task<object> ExecuteQueryAsync(string query, string tenant)
    {
        var result = await _executor.ExecuteWithRetryAsync(query);
        return new { Success = result.IsSucceeded, Data = result.Records };
    }

    public async Task<object> DetectCommunitiesAsync(string tenant, CancellationToken ct)
    {
        var ngql = $@"
            CALL louvain('Entity', 'CO_OCCURS_WITH', '{tenant}')
            YIELD cluster_id
            RETURN cluster_id, count(*) AS size
            ORDER BY size DESC
            LIMIT 10
        ";
        var result = await _executor.ExecuteWithRetryAsync(ngql, ct);
        return new { Success = result.IsSucceeded, Communities = result.Records };
    }
}
