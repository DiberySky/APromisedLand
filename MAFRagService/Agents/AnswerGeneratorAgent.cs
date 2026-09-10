
using MAFRagService.Models;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;

public class AnswerGeneratorAgent : Agent
{
    private readonly IAgentModel _model;

    public AnswerGeneratorAgent(IAgentModel model) => _model = model;

    [AgentFunction("GenerateAnswer")]
    public async Task<string> GenerateAsync(
        [Parameter(Description = "User question")] string question,
        [Parameter(Description = "Retrieved sources")] List<Source> sources,
        [Parameter(Description = "Graph context")] GraphContext? graphContext,
        [Parameter(Description = "Tenant")] string tenant,
        CancellationToken ct)
    {
        var contextText = string.Join("\n", sources.Select(s => s.Content));
        if (graphContext != null && graphContext.PathNodes.Any())
        {
            contextText += "\n相关知识图谱上下文：\n" +
                string.Join("\n", graphContext.PathNodes.Select(p => $"- {p}"));
        }

        var prompt = $@"
基于以下信息回答问题。如果你不知道答案，请如实说不知道。
---
{contextText}
---
问题：{question}
答案：";
        return await _model.GenerateAsync(prompt, ct);
    }
}
