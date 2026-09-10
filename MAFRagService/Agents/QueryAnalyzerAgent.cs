
using MAFRagService.Models;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;

public class QueryAnalyzerAgent : Agent
{
    private readonly IAgentModel _model;
    private readonly ILogger<QueryAnalyzerAgent> _logger;

    public QueryAnalyzerAgent(IAgentModel model, ILogger<QueryAnalyzerAgent> logger)
    {
        _model = model;
        _logger = logger;
    }

    [AgentFunction("AnalyzeQuestion")]
    public async Task<QueryIntent> AnalyzeAsync(
        [Parameter(Description = "Original question")] string question,
        [Parameter(Description = "Tenant ID")] string tenant,
        CancellationToken ct)
    {
        var prompt = $@"
分析以下问题，提取关键实体、意图类型和改写后的查询。
输出 JSON 格式：
{{
    ""Query"": ""改写后的查询"",
    ""Entities"": [""实体1"", ""实体2""],
    ""Intent"": ""查询类型""
}}
问题：{question}
";
        var response = await _model.GenerateAsync(prompt, ct);
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<QueryIntent>(response);
            result.OriginalQuestion = question;
            result.Tenant = tenant;
            return result ?? new QueryIntent { Query = question };
        }
        catch
        {
            _logger.LogWarning("Failed to parse LLM response, using fallback");
            return new QueryIntent { Query = question, OriginalQuestion = question, Tenant = tenant };
        }
    }
}
