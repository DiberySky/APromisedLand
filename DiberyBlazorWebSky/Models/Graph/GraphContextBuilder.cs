using System.Text;

namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>
/// 把图数据序列化为 LLM 友好的文本，用于 GraphChat 页面
/// 将图上下文注入用户消息。
/// </summary>
public static class GraphContextBuilder
{
    /// <summary>超过此节点数则不附加图上下文。</summary>
    public const int MaxNodeCount = 80;

    public static string Build(
        string graphName,
        IReadOnlyList<NodeDto> nodes,
        IReadOnlyList<EdgeDto> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\">");

        sb.AppendLine("<nodes>");
        if (nodes.Count == 0)
        {
            sb.AppendLine("(无节点)");
        }
        else
        {
            foreach (var n in nodes)
            {
                var labels = n.Labels is { Count: > 0 }
                    ? $" [{string.Join(", ", n.Labels)}]"
                    : "";
                sb.AppendLine($"- {n.Name}{labels} (GUID: {n.Guid})");
            }
        }
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0)
        {
            sb.AppendLine("(无边)");
        }
        else
        {
            var nodeLookup = nodes.ToDictionary(x => x.Guid, x => x.Name);
            foreach (var e in edges)
            {
                var fromName = nodeLookup.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName   = nodeLookup.TryGetValue(e.To, out var t)   ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        }
        sb.AppendLine("</edges>");

        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    public static string BuildPrompt(
        string graphName,
        IReadOnlyList<NodeDto> nodes,
        IReadOnlyList<EdgeDto> edges,
        string userMessage)
    {
        var context = Build(graphName, nodes, edges);

        return $"""
以下是当前图的上下文。请基于这些真实数据回答用户问题。
如果上下文中不包含答案，请明确说明，不要编造。

{context}

用户问题：{userMessage}
""";
    }
}