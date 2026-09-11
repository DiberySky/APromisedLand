using MAFRagService.Models;

namespace MAFRagService.Agents;

/// <summary>
/// /rag/ask 的完整返回结构。
/// 从 OrchestratorAgent 透传到 AskController，让调用方能直接看到检索结果。
///
/// <para><b>字段：</b></para>
/// <list type="bullet">
///   <item><see cref="Answer"/> —— LLM 生成的最终回答；</item>
///   <item><see cref="Sources"/> —— 检索命中的文档片段（含 DocId / Content / Score）；</item>
///   <item><see cref="Tenant"/> —— 请求解析出的租户标识。</item>
/// </list>
/// </summary>
public sealed record AskResult(
    string Answer,
    List<Source> Sources,
    string Tenant);