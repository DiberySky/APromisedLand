using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.MafWfApi.Agents;

/// <summary>
/// 从 TreeGraph.Api 通过 HTTP 加载子树（走 SubtreeController）。
/// 复用 SearchApiClient 已配置的 HttpClient（BaseAddress = TreeGraphEavApi）。
/// </summary>
public sealed class SubtreeLoader
{
    private readonly HttpClient _http;
    private readonly ILogger<SubtreeLoader> _logger;

    public SubtreeLoader(HttpClient http, ILogger<SubtreeLoader> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SubtreeNodeDto?> LoadAsync(
        string hostType, string inodeId, int maxDepth, int maxNodes,
        CancellationToken ct)
    {
        var url = $"api/inode/{Uri.EscapeDataString(hostType)}/" +
                  $"{Uri.EscapeDataString(inodeId)}/subtree" +
                  $"?maxDepth={maxDepth}&maxNodes={maxNodes}";

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();

            // 响应被 ApiEnvelopeFilter 包装为 { success, data: { root, totalNodes, truncated } }
            var env = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!env.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("root", out var root)) return null;
            if (root.ValueKind == JsonValueKind.Null) return null;

            return ParseNode(root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载子树失败：{Host}/{Id}", hostType, inodeId);
            return null;
        }
    }

    private static SubtreeNodeDto ParseNode(JsonElement el)
    {
        var children = new List<SubtreeNodeDto>();
        if (el.TryGetProperty("children", out var ch) && ch.ValueKind == JsonValueKind.Array)
            children.AddRange(ch.EnumerateArray().Select(ParseNode));

        return new SubtreeNodeDto(
            Id: el.GetProperty("id").GetString() ?? "",
            Name: el.GetProperty("name").GetString() ?? "",
            Path: el.TryGetProperty("path", out var p) ? p.GetString() : null,
            EavRendered: el.TryGetProperty("eavRendered", out var e) ? e.GetString() : null,
            Children: children);
    }
}
