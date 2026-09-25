using System.Linq;
using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce;

public partial class GraphVectorSearch : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphVectorSearch> Logger { get; set; } = default!;

    private bool _isBusy;
    private string? _errorMessage;

    // 向量搜索
    private string _vectorQueryText = "";
    private List<VectorSearchDisplayResult> _vectorResults = new();
    private string _vectorProgress = "";
    private IntentResultDto? _lastIntent;
    private string? _lastHint;
    private List<SuggestedRelationDto> _lastSuggestions = new();
    private bool _useReranker = true;
    private string? _pendingDirectionOverride;

    // ★ 边名 → 中文映射
    private static readonly Dictionary<string, string> EdgeNameZh = new()
    {
        ["PARENT_OF"] = "父亲/父节点",
        ["CHILD_OF"] = "子节点",
        ["SUBFIELD"] = "子领域",
        ["BASED_ON"] = "基于",
        ["USES"] = "使用",
        ["APPLIED_TO"] = "应用于",
        ["IMPLEMENTED_IN"] = "实现于",
        ["VARIANT"] = "变体",
        ["FRAMEWORK"] = "框架",
        ["EXAMPLE"] = "例子",
        ["METHOD"] = "方法",
        ["RELATED_TO"] = "相关",
    };

    protected override async Task OnInitializedAsync()
    {
        Context.StateChanged += OnContextStateChanged;
        if (Context.Graphs.Count == 0)
        {
            await Context.LoadGraphsAsync();
        }
    }

    private Task OnContextStateChanged()
    {
        _vectorResults.Clear();
        _vectorProgress = "";
        _lastIntent = null;
        _lastHint = null;
        _lastSuggestions = new();
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════
    // 向量搜索
    // ══════════════════════════════════════════════════════

    private async Task<List<float>?> GetEmbeddingAsync(string text)
    {
        var vector = await GraphApi.GetEmbeddingAsync(text);
        if (vector == null)
            _errorMessage = "生成 embedding 失败，请查看后端日志。";
        return vector;
    }

    private async Task HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !_isBusy)
            await SearchVectorAsync();
    }

    private async Task SearchVectorAsync()
    {
        if (string.IsNullOrWhiteSpace(_vectorQueryText) ||
            string.IsNullOrEmpty(Context.SelectedGraphGuid))
            return;

        _isBusy = true;
        _errorMessage = null;
        _vectorResults.Clear();
        _lastIntent = null;
        _lastHint = null;
        _lastSuggestions = new List<SuggestedRelationDto>();
        _vectorProgress = "解析意图 + 语义搜索...";
        StateHasChanged();

        var directionOverride = _pendingDirectionOverride;
        _pendingDirectionOverride = null;

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var query = _vectorQueryText.Trim();

            var resp = await GraphApi.SemanticSearchAsync(
                graphGuid, query,
                topK: 20,
                directionOverride: directionOverride,
                useReranker: _useReranker);

            if (resp is null)
            {
                _errorMessage = "语义搜索失败，请查看后端日志。";
                return;
            }

            _lastIntent = resp.Intent;
            _lastHint = resp.Hint;
            _lastSuggestions = resp.Suggestions ?? new List<SuggestedRelationDto>();

            _vectorResults = resp.Hits.Select(h => new VectorSearchDisplayResult
            {
                NodeName = h.NodeName,
                Score = (float)h.Score,
                ViaEdge = h.ViaEdgeName,
                Direction = h.Direction,
                MatchedContent = h.MatchedContent,
                VectorScore = h.VectorScore.HasValue ? (float)h.VectorScore.Value : null,
                Bm25Score = h.Bm25Score.HasValue ? (float)h.Bm25Score.Value : null,
                HopDistance = h.HopDistance,
                IsMultiHop = h.IsMultiHop
            }).ToList();

            _vectorProgress = $"返回 {_vectorResults.Count} 条结果";

            Logger.LogInformation(
                "语义搜索完成：Query={Q}, Hits={N}, Intent={R}/{D}, Strategy={S}, Override={Ov}",
                query, _vectorResults.Count,
                resp.Intent.Relation, resp.Intent.Direction, resp.Intent.Strategy,
                directionOverride ?? "null");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "语义搜索失败");
            _errorMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task RunSuggestionAsync(SuggestedRelationDto s)
    {
        if (s is null || string.IsNullOrWhiteSpace(s.Query)) return;

        _vectorQueryText = s.Query;
        _pendingDirectionOverride = s.Direction;
        StateHasChanged();

        await SearchVectorAsync();
    }

    private async Task GenerateAllVectorsAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;

        if (!await ConfirmAsync(
                "将重建当前图的所有向量：\n" +
                "  · 节点向量（纯节点名）\n" +
                "  · 边向量（from --REL(中文)--> to）\n\n" +
                "旧的向量会先被清空。继续？"))
            return;

        _isBusy = true;
        _errorMessage = null;
        _vectorProgress = "";
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);

            _vectorProgress = "清理旧向量...";
            StateHasChanged();

            var existingVectors = await GraphApi.ListAllVectorsAsync(graphGuid);
            Logger.LogInformation("开始清空 {Count} 条旧向量", existingVectors.Count);

            foreach (var v in existingVectors)
            {
                try { await GraphApi.DeleteVectorAsync(graphGuid, v.Guid); }
                catch (Exception ex) { Logger.LogWarning(ex, "删除旧向量 {Guid} 失败", v.Guid); }
            }

            _vectorProgress = "加载节点和边...";
            StateHasChanged();

            var allNodes = await GraphApi.ListAllNodesAsync(graphGuid);
            var allEdges = await GraphApi.ListAllEdgesAsync(graphGuid);

            var nodeMap = allNodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Name))
                .GroupBy(n => n.Guid)
                .ToDictionary(g => g.Key, g => g.First().Name);

            int total = allNodes.Count + allEdges.Count;
            int done = 0;
            int nodeOk = 0, edgeOk = 0, fail = 0;

            foreach (var node in allNodes)
            {
                done++;
                _vectorProgress = $"({done}/{total}) 节点 {node.Name}";
                StateHasChanged();

                if (string.IsNullOrWhiteSpace(node.Name)) { fail++; continue; }

                var vec = await GetEmbeddingAsync(node.Name);
                if (vec is null) { fail++; continue; }

                try
                {
                    var ok = await GraphApi.CreateVectorAsync(graphGuid, node.Guid, model: null, vec);
                    if (ok) nodeOk++; else fail++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "创建节点向量失败：{Name}", node.Name);
                    fail++;
                }
            }

            foreach (var edge in allEdges)
            {
                done++;
                var rel = string.IsNullOrWhiteSpace(edge.Name) ? "RELATED_TO" : edge.Name;
                _vectorProgress = $"({done}/{total}) 边 {rel}";
                StateHasChanged();

                var fromName = nodeMap.TryGetValue(edge.From, out var f) ? f : edge.From.ToString("N")[..8];
                var toName = nodeMap.TryGetValue(edge.To, out var t) ? t : edge.To.ToString("N")[..8];

                var zh = EdgeNameZh.TryGetValue(rel, out var z) ? z : null;
                var relLabel = string.IsNullOrEmpty(zh) ? rel : $"{rel}({zh})";

                var content = $"{fromName} --{relLabel}--> {toName}";

                var vec = await GetEmbeddingAsync(content);
                if (vec is null) { fail++; continue; }

                try
                {
                    var ok = await GraphApi.CreateEdgeVectorAsync(graphGuid, edge.Guid, content, vec);
                    if (ok) edgeOk++; else fail++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "创建边向量失败：{Content}", content);
                    fail++;
                }
            }

            _vectorProgress = $"✅ 完成：节点 {nodeOk} + 边 {edgeOk} = {nodeOk + edgeOk}，失败 {fail}";
            Logger.LogInformation("向量生成完成：{Msg}", _vectorProgress);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "生成向量失败");
            _errorMessage = $"生成向量失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        try { return await Js.InvokeAsync<bool>("confirm", message); }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "JS confirm 调用失败，默认允许操作");
            return true;
        }
    }

    public void Dispose()
    {
        Context.StateChanged -= OnContextStateChanged;
    }

    // ─── 向量搜索显示 DTO ──────────────────────────────
    private sealed class VectorSearchDisplayResult
    {
        public string NodeName { get; set; } = "";
        public float Score { get; set; }
        public string? ViaEdge { get; set; }
        public string? Direction { get; set; }
        public string? MatchedContent { get; set; }
        public float? VectorScore { get; set; }
        public float? Bm25Score { get; set; }
        public int? HopDistance { get; set; }
        public bool IsMultiHop { get; set; }
    }
}
