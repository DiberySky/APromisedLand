using MAFRagService.Memory;
using MAFRagService.Models;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;

public class OrchestratorAgent : Agent
{
    private readonly IServiceProvider _services;
    private readonly IMemoryStore _memory;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(IServiceProvider services, IMemoryStore memory, ILogger<OrchestratorAgent> logger)
    {
        _services = services;
        _memory = memory;
        _logger = logger;
    }

    [AgentFunction("AskQuestion")]
    public async Task<string> AskQuestion(
        [Parameter(Description = "User's question")] string question,
        [Parameter(Description = "Tenant ID")] string tenant,
        [Parameter(Description = "Version filter")] string? version = null,
        [Parameter(Description = "Use graph reasoning?")] bool useGraph = true,
        [Parameter(Description = "Top K results")] int topK = 5)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = cts.Token;

        var analyzer = _services.GetRequiredService<QueryAnalyzerAgent>();
        var intent = await analyzer.AnalyzeAsync(question, tenant, ct);

        var retriever = _services.GetRequiredService<RetrieverAgent>();
        var graphReasoner = _services.GetRequiredService<GraphReasonerAgent>();

        var retrievalTask = retriever.RetrieveAsync(intent, tenant, version, topK * 2, ct);
        var graphTask = useGraph ? graphReasoner.ReasonAsync(intent, tenant, ct) : Task.FromResult<GraphContext?>(null);

        await Task.WhenAll(retrievalTask, graphTask);
        var sources = await retrievalTask;
        var graphContext = await graphTask;

        var generator = _services.GetRequiredService<AnswerGeneratorAgent>();
        var answer = await generator.GenerateAsync(question, sources, graphContext, tenant, ct);

        _ = SaveToMemoryAsync(question, intent, graphContext, sources, tenant);

        return answer;
    }

    private async Task SaveToMemoryAsync(string question, QueryIntent intent, GraphContext? graph, List<Source> sources, string tenant)
    {
        try
        {
            var memoryStore = _services.GetRequiredService<INebulaGraphMemoryStore>();
            await memoryStore.SaveConversationAsync(new ConversationMemory
            {
                Question = question,
                Entities = intent.Entities,
                Tenant = tenant,
                Timestamp = DateTime.UtcNow,
                Sources = sources.Select(s => s.DocId).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save memory");
        }
    }
}
