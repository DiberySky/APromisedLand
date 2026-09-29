using System.Text.Json.Nodes;

namespace MafRagApi.Services;

/// <summary>
/// 构建 vLLM <c>response_format</c> 参数，驱动模型**原生结构化输出**。
///
/// 与后置的 <see cref="JsonSchemaValidator"/> 不同：
///   - 后置校验是"生成完再检查"，不合规只能重试；
///   - response_format 是"生成时约束"（constrained decoding / 语法引导），
///     模型在 token 级别就被限制在合法 JSON / Schema 内，可靠性更高。
///
/// 两种模式：
///   1. json_schema  —— 传入 ResponseSchema，模型严格按 Schema 输出（strict=true）。
///   2. json_object  —— 仅要求合法 JSON 对象，不约束字段。
/// </summary>
public static class StructuredOutput
{
    /// <summary>挂在 ChatOptions.AdditionalProperties 上的键名。</summary>
    public const string AdditionalPropertiesKey = "response_format";

    /// <summary>
    /// 根据 schema / outputFormat 构建 vLLM response_format 对象。
    /// </summary>
    /// <param name="schema">JSON Schema 字符串（优先）。</param>
    /// <param name="outputFormat">"json" 时回退到 json_object 模式。</param>
    /// <returns>vLLM response_format 的 JsonNode；无需结构化输出时返回 null。</returns>
    public static JsonNode? BuildResponseFormat(string? schema, string? outputFormat)
    {
        // ── 1. 优先 json_schema 模式 ─────────────────────────────
        if (!string.IsNullOrWhiteSpace(schema))
        {
            JsonNode? schemaNode;
            try { schemaNode = JsonNode.Parse(schema); }
            catch { return null; }   // schema 本身不是合法 JSON，交回后置校验兜底

            return new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"]   = "structured_output",
                    ["schema"] = schemaNode,
                    ["strict"] = true,
                },
            };
        }

        // ── 2. 其次 json_object 模式（仅保证合法 JSON 对象）────────
        if (string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject { ["type"] = "json_object" };
        }

        return null;
    }
}
