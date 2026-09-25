using System.Text.Encodings.Web;
using System.Text.Json;
using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class McpGraphExplorerForceAll : ComponentBase, IDisposable
{
    // ─── 注入 ─────────────────────────────────────────
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;

    // ─── 图 / 节点 / 边状态 ──────────────────────────
    private List<GraphDto> _graphs = new();
    private List<NodeDto>  _nodes  = new();
    private List<EdgeDto>  _edges  = new();

    private string _selectedGraphGuid = "";
    private string _activeTab = "nodes";
    private bool _isLoading;
    private bool _isBusy;
    private string? _errorMessage;
    private string? _detailJson;

    // 分页
    private Guid? _nodeContinuationToken;
    private long _nodeTotal;
    private bool _nodeHasPrev;
    private bool _nodeHasNext;

    private Guid? _edgeContinuationToken;
    private long _edgeTotal;
    private bool _edgeHasPrev;
    private bool _edgeHasNext;

    private const int PageSize = 20;
    private const int MaxEnumerationResults = 1000;

    // CRUD 输入
    private string _newNodeName = "";
    private readonly HashSet<Guid> _selectedNodeGuids = new();
    private string _newEdgeFrom = "";
    private string _newEdgeTo = "";
    private string _newEdgeName = "";

    // ── 向量搜索 ─────────────────────────────────────
    private string _vectorQueryText = "";
    private List<VectorSearchDisplayResult> _vectorResults = new();
    private string _vectorProgress = "";
    private IntentResultDto? _lastIntent;

    // 拓扑图
    private List<NodeDto> _topologyNodes = new();
    private List<EdgeDto> _topologyEdges = new();
    private NodeDto? _selectedTopologyNode;
    private readonly Dictionary<Guid, (double X, double Y)> _nodePositions = new();

    private string _layoutMode = "force";
    private int _layoutSeed = 42;

    // 导入 / 导出状态
    private string? _selectedJsonFileName;
    private string? _selectedJsonContent;
    private string? _selectedNodesCsvName;
    private string? _selectedNodesCsvContent;
    private string? _selectedEdgesCsvName;
    private string? _selectedEdgesCsvContent;
    private bool _clearBeforeImport;
    private ImportResult? _importResult;

    // 行内编辑状态
    private Guid? _editingNodeGuid;
    private string _editingNodeName = "";
    private Guid? _editingEdgeGuid;
    private string _editingEdgeName = "";

    // 图管理状态
    private bool _showManageDialog;
    private bool _isManaging;
    private string _newGraphName = "";
    private bool _isCreatingGraph;
    private Guid? _renamingGraphGuid;
    private string _renamingGraphName = "";

    private string? _pendingDirectionOverride;

    private bool HasAnyImportFile =>
        !string.IsNullOrEmpty(_selectedJsonContent) ||
        !string.IsNullOrEmpty(_selectedNodesCsvContent);

    private string? _lastHint;
    private List<SuggestedRelationDto> _lastSuggestions = new();
    private bool _useReranker = true;

    // SVG 画布尺寸
    private const double CanvasWidth = 800;
    private const double CanvasHeight = 600;

    // ★ 边名 → 中文映射（生成边向量时使用）
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

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ─── 生命周期 ─────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        await LoadGraphsAsync();
    }

    // ─── 图列表 ───────────────────────────────────────
    private async Task LoadGraphsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            _graphs = await GraphApi.ListGraphsAsync();
            Logger.LogInformation("加载 {Count} 个图", _graphs.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载图列表失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnGraphChangedAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        _nodeContinuationToken = null;
        _edgeContinuationToken = null;
        _nodeHasPrev = _edgeHasPrev = false;
        _nodeHasNext = _edgeHasNext = false;
        _selectedNodeGuids.Clear();
        _editingNodeGuid = null;
        _editingNodeName = "";
        _editingEdgeGuid = null;
        _editingEdgeName = "";
        _vectorResults.Clear();
        _vectorProgress = "";
        _lastIntent = null;

        _topologyNodes.Clear();
        _topologyEdges.Clear();
        _nodePositions.Clear();
        _selectedTopologyNode = null;

        _selectedJsonFileName = null;
        _selectedJsonContent = null;
        _selectedNodesCsvName = null;
        _selectedNodesCsvContent = null;
        _selectedEdgesCsvName = null;
        _selectedEdgesCsvContent = null;
        _clearBeforeImport = false;
        _importResult = null;

        await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
    }

    // ─── 节点查询 ─────────────────────────────────────
    private async Task LoadNodesAsync() => await LoadNodesAsync(null);

    private async Task LoadNodesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        var graphGuid = Guid.Parse(_selectedGraphGuid);
        _isBusy = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var result = await GraphApi.EnumerateNodesAsync(graphGuid, PageSize, continuationToken);
            _nodes = result.Objects;
            _nodeTotal = result.TotalRecords;
            _nodeContinuationToken = result.ContinuationToken;
            _nodeHasPrev = continuationToken != null;
            _nodeHasNext = result.ContinuationToken != null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task NextNodePageAsync()
    {
        if (_nodeHasNext) await LoadNodesAsync(_nodeContinuationToken);
    }

    private async Task PrevNodePageAsync()
    {
        _nodeContinuationToken = null;
        await LoadNodesAsync(null);
    }

    // ─── 边查询 ──────────────────────────────────────
    private async Task LoadEdgesAsync() => await LoadEdgesAsync(null);

    private async Task LoadEdgesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        var graphGuid = Guid.Parse(_selectedGraphGuid);
        _isBusy = true;
        StateHasChanged();

        try
        {
            var result = await GraphApi.EnumerateEdgesAsync(graphGuid, PageSize, continuationToken);
            _edges = result.Objects;
            _edgeTotal = result.TotalRecords;
            _edgeContinuationToken = result.ContinuationToken;
            _edgeHasPrev = continuationToken != null;
            _edgeHasNext = result.ContinuationToken != null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task NextEdgePageAsync()
    {
        if (_edgeHasNext) await LoadEdgesAsync(_edgeContinuationToken);
    }

    private async Task PrevEdgePageAsync()
    {
        _edgeContinuationToken = null;
        await LoadEdgesAsync(null);
    }

    // ─── CRUD 节点 ───────────────────────────────────
    private async Task CreateNodeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newNodeName) || string.IsNullOrEmpty(_selectedGraphGuid))
            return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var ok = await GraphApi.CreateNodeAsync(graphGuid, _newNodeName.Trim());
            if (ok)
            {
                _newNodeName = "";
                await LoadNodesAsync();
            }
            else
            {
                _errorMessage = "创建节点失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteNodeAsync(Guid nodeGuid)
    {
        if (!await ConfirmAsync("确定删除该节点及其关联边？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            await GraphApi.DeleteNodeAsync(graphGuid, nodeGuid);
            _selectedNodeGuids.Remove(nodeGuid);
            await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteSelectedNodesAsync()
    {
        if (_selectedNodeGuids.Count == 0) return;
        if (!await ConfirmAsync($"确定删除选中的 {_selectedNodeGuids.Count} 个节点？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            foreach (var guid in _selectedNodeGuids)
                await GraphApi.DeleteNodeAsync(graphGuid, guid);

            _selectedNodeGuids.Clear();
            await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "批量删除节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task SaveNodeNameAsync(NodeDto node)
    {
        if (_editingNodeGuid != node.Guid) return;

        var newName = _editingNodeName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Name)
        {
            CancelEditNode();
            return;
        }

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var ok = await GraphApi.UpdateNodeAsync(graphGuid, node.Guid, newName);
            if (ok)
            {
                Logger.LogInformation("节点已重命名：{Guid} → {Name}", node.Guid, newName);
                CancelEditNode();
                await LoadNodesAsync();
            }
            else
            {
                _errorMessage = "重命名失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "重命名节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    // ─── CRUD 边 ─────────────────────────────────────
    private async Task CreateEdgeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newEdgeFrom) ||
            string.IsNullOrWhiteSpace(_newEdgeTo) ||
            string.IsNullOrEmpty(_selectedGraphGuid))
            return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var edgeName = string.IsNullOrWhiteSpace(_newEdgeName) ? "RELATED_TO" : _newEdgeName.Trim();

            var ok = await GraphApi.CreateEdgeAsync(
                graphGuid, Guid.Parse(_newEdgeFrom), Guid.Parse(_newEdgeTo), edgeName);

            if (ok)
            {
                _newEdgeFrom = _newEdgeTo = _newEdgeName = "";
                await LoadEdgesAsync();
            }
            else
            {
                _errorMessage = "创建边失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteEdgeAsync(Guid edgeGuid)
    {
        if (!await ConfirmAsync("确定删除该边？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            await GraphApi.DeleteEdgeAsync(graphGuid, edgeGuid);
            await LoadEdgesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task SaveEdgeNameAsync(EdgeDto edge)
    {
        if (_editingEdgeGuid != edge.Guid) return;

        var newName = _editingEdgeName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == edge.Name)
        {
            CancelEditEdge();
            return;
        }

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var ok = await GraphApi.UpdateEdgeAsync(graphGuid, edge.Guid, newName);
            if (ok)
            {
                CancelEditEdge();
                await LoadEdgesAsync();
            }
            else
            {
                _errorMessage = "重命名失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "重命名边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
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
            string.IsNullOrEmpty(_selectedGraphGuid))
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
            var graphGuid = Guid.Parse(_selectedGraphGuid);
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
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

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
            var graphGuid = Guid.Parse(_selectedGraphGuid);

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

            // 节点向量
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

            // 边向量
            foreach (var edge in allEdges)
            {
                done++;
                var rel = string.IsNullOrWhiteSpace(edge.Name) ? "RELATED_TO" : edge.Name;
                _vectorProgress = $"({done}/{total}) 边 {rel}";
                StateHasChanged();

                var fromName = nodeMap.TryGetValue(edge.From, out var f) ? f : edge.From.ToString("N")[..8];
                var toName   = nodeMap.TryGetValue(edge.To,   out var t) ? t : edge.To.ToString("N")[..8];

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

    // ══════════════════════════════════════════════════════
    // 拓扑图
    // ══════════════════════════════════════════════════════

    private async Task LoadTopologyAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        _isBusy = true;
        _errorMessage = null;
        _selectedTopologyNode = null;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);

            _topologyNodes = await GraphApi.ListAllNodesAsync(graphGuid);
            _topologyEdges = await GraphApi.ListAllEdgesAsync(graphGuid);

            RecomputeLayout();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载拓扑图失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private void RecomputeLayout()
    {
        try
        {
            if (_layoutMode == "circular") ComputeCircularLayout();
            else ComputeForceDirectedLayout();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "布局计算失败，回退到圆形布局");
            ComputeCircularLayout();
        }
    }

    private void SwitchLayout(string mode)
    {
        if (_layoutMode == mode) return;
        _layoutMode = mode;
        RecomputeLayout();
        StateHasChanged();
    }

    private void ShuffleLayout()
    {
        _layoutSeed = Random.Shared.Next(1, 1_000_000);
        if (_layoutMode == "force") ComputeForceDirectedLayout();
        StateHasChanged();
    }

    private void ComputeCircularLayout()
    {
        _nodePositions.Clear();
        int n = _topologyNodes.Count;
        if (n == 0) return;

        const double cx = CanvasWidth / 2;
        const double cy = CanvasHeight / 2;
        double radius = Math.Min(220, Math.Max(100, n * 8));

        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n - Math.PI / 2;
            _nodePositions[_topologyNodes[i].Guid] =
                (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
        }
    }

    private void ComputeForceDirectedLayout()
    {
        _nodePositions.Clear();
        int n = _topologyNodes.Count;

        if (n == 0) return;
        if (n == 1)
        {
            _nodePositions[_topologyNodes[0].Guid] = (CanvasWidth / 2, CanvasHeight / 2);
            return;
        }

        const double cx = CanvasWidth / 2;
        const double cy = CanvasHeight / 2;
        const double desiredEdgeLength = 180;
        const double minSeparation = 130;
        const double edgeStiffness = 0.10;
        const double centerCoef = 0.04;
        const double centerCoefIsolated = 0.10;

        var positions = new Dictionary<Guid, (double X, double Y)>();
        var rng = new Random(_layoutSeed);
        double initRadius = Math.Min(80, 30 + n * 4);

        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            double jitter = (rng.NextDouble() - 0.5) * 20;
            positions[_topologyNodes[i].Guid] =
                (cx + (initRadius + jitter) * Math.Cos(angle),
                 cy + (initRadius + jitter) * Math.Sin(angle));
        }

        var adjacency = _topologyNodes.ToDictionary(node => node.Guid, _ => new HashSet<Guid>());
        foreach (var e in _topologyEdges)
        {
            if (adjacency.ContainsKey(e.From) && adjacency.ContainsKey(e.To))
            {
                adjacency[e.From].Add(e.To);
                adjacency[e.To].Add(e.From);
            }
        }

        int iterations = Math.Clamp(n * 40, 150, 500);
        double temperature = 80.0;
        double cooling = temperature / (iterations + 1);

        for (int iter = 0; iter < iterations; iter++)
        {
            var disp = _topologyNodes.ToDictionary(node => node.Guid, _ => (Dx: 0.0, Dy: 0.0));

            // 斥力
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = _topologyNodes[i].Guid;
                    var b = _topologyNodes[j].Guid;

                    double dx = positions[a].X - positions[b].X;
                    double dy = positions[a].Y - positions[b].Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 0.1)
                    {
                        dx = rng.NextDouble() - 0.5;
                        dy = rng.NextDouble() - 0.5;
                        dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 0.01) { dx = 1; dy = 0; dist = 1; }
                    }

                    if (dist >= minSeparation) continue;

                    double overlap = (minSeparation - dist) / minSeparation;
                    double force = overlap * overlap * 60;

                    double fx = (dx / dist) * force;
                    double fy = (dy / dist) * force;

                    disp[a] = (disp[a].Dx + fx, disp[a].Dy + fy);
                    disp[b] = (disp[b].Dx - fx, disp[b].Dy - fy);
                }
            }

            // 边引力
            foreach (var edge in _topologyEdges)
            {
                if (edge.From == edge.To) continue;
                if (!positions.ContainsKey(edge.From) || !positions.ContainsKey(edge.To)) continue;

                double dx = positions[edge.From].X - positions[edge.To].X;
                double dy = positions[edge.From].Y - positions[edge.To].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 0.1) continue;

                double deviation = dist - desiredEdgeLength;
                double force = deviation * edgeStiffness;

                double fx = (dx / dist) * force;
                double fy = (dy / dist) * force;

                disp[edge.From] = (disp[edge.From].Dx - fx, disp[edge.From].Dy - fy);
                disp[edge.To]   = (disp[edge.To].Dx + fx,   disp[edge.To].Dy + fy);
            }

            // 中心引力
            foreach (var node in _topologyNodes)
            {
                double dx = cx - positions[node.Guid].X;
                double dy = cy - positions[node.Guid].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1) continue;

                bool isolated = adjacency[node.Guid].Count == 0;
                double coef = isolated ? centerCoefIsolated : centerCoef;
                double force = coef * dist;

                double fx = (dx / dist) * force;
                double fy = (dy / dist) * force;

                disp[node.Guid] = (disp[node.Guid].Dx + fx, disp[node.Guid].Dy + fy);
            }

            // 应用位移
            foreach (var node in _topologyNodes)
            {
                var d = disp[node.Guid];
                double len = Math.Sqrt(d.Dx * d.Dx + d.Dy * d.Dy);
                if (len < 0.001) continue;

                double limited = Math.Min(len, temperature);
                double newX = positions[node.Guid].X + (d.Dx / len) * limited;
                double newY = positions[node.Guid].Y + (d.Dy / len) * limited;

                newX = Math.Clamp(newX, 60, CanvasWidth - 60);
                newY = Math.Clamp(newY, 60, CanvasHeight - 60);

                positions[node.Guid] = (newX, newY);
            }

            temperature = Math.Max(temperature - cooling, 0.5);
        }

        CenterWithoutScaling(positions);

        foreach (var kv in positions)
            _nodePositions[kv.Key] = kv.Value;
    }

    private static void CenterWithoutScaling(
        Dictionary<Guid, (double X, double Y)> positions)
    {
        if (positions.Count == 0) return;

        double minX = positions.Values.Min(p => p.X);
        double maxX = positions.Values.Max(p => p.X);
        double minY = positions.Values.Min(p => p.Y);
        double maxY = positions.Values.Max(p => p.Y);

        double currentCenterX = (minX + maxX) / 2;
        double currentCenterY = (minY + maxY) / 2;

        double offsetX = CanvasWidth / 2 - currentCenterX;
        double offsetY = CanvasHeight / 2 - currentCenterY;

        foreach (var key in positions.Keys.ToList())
        {
            var p = positions[key];
            positions[key] = (p.X + offsetX, p.Y + offsetY);
        }
    }

    private (double X, double Y)? GetPosition(Guid nodeGuid)
        => _nodePositions.TryGetValue(nodeGuid, out var p) ? p : null;

    private void SelectTopologyNode(NodeDto node)
    {
        _selectedTopologyNode = _selectedTopologyNode?.Guid == node.Guid ? null : node;
        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════
    // 导入 / 导出
    // ══════════════════════════════════════════════════════

    private async Task ExportJsonAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var graphName = _graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var json = await GraphApi.ExportGraphJsonAsync(graphGuid);

            if (string.IsNullOrEmpty(json)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, json, "application/json");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出 JSON 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ExportNodesCsvAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var graphName = _graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var csv = await GraphApi.ExportNodesCsvAsync(graphGuid);

            if (string.IsNullOrEmpty(csv)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-nodes-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, csv, "text/csv;charset=utf-8");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出节点 CSV 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ExportEdgesCsvAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);
            var graphName = _graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var csv = await GraphApi.ExportEdgesCsvAsync(graphGuid);

            if (string.IsNullOrEmpty(csv)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-edges-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, csv, "text/csv;charset=utf-8");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出边 CSV 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task StartImportAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;
        if (!HasAnyImportFile) return;

        var clearWarning = _clearBeforeImport
            ? "⚠️ 将先清空图中现有节点和边！此操作不可撤销。\n\n"
            : "";
        if (!await ConfirmAsync($"{clearWarning}确定导入？"))
            return;

        _isBusy = true;
        _errorMessage = null;
        _importResult = null;
        _vectorProgress = "解析文件中...";
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);

            ImportResultDto dto;
            if (!string.IsNullOrEmpty(_selectedJsonContent))
            {
                dto = await GraphApi.ImportJsonAsync(graphGuid, _selectedJsonContent, _clearBeforeImport);
            }
            else
            {
                dto = await GraphApi.ImportCsvAsync(
                    graphGuid, _selectedNodesCsvContent!, _selectedEdgesCsvContent, _clearBeforeImport);
            }

            _importResult = new ImportResult
            {
                NodesCreated = dto.NodesCreated,
                NodesSkipped = dto.NodesSkipped,
                EdgesCreated = dto.EdgesCreated,
                EdgesSkipped = dto.EdgesSkipped,
                Errors       = dto.Errors
            };

            await LoadNodesAsync();
            await LoadEdgesAsync();

            _topologyNodes.Clear();
            _topologyEdges.Clear();
            _nodePositions.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导入失败");
            _errorMessage = $"导入失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            _vectorProgress = "";
            StateHasChanged();
        }
    }

    private async Task OnJsonFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedJsonContent = await reader.ReadToEndAsync();
            _selectedJsonFileName = $"{file.Name} ({FormatFileSize(file.Size)})";
            _importResult = null;

            _selectedNodesCsvContent = null;
            _selectedNodesCsvName = null;
            _selectedEdgesCsvContent = null;
            _selectedEdgesCsvName = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取 JSON 文件失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedJsonFileName = null;
            _selectedJsonContent = null;
        }

        StateHasChanged();
        await Task.CompletedTask;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private async Task OnNodesCsvSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedNodesCsvContent = await reader.ReadToEndAsync();
            _selectedNodesCsvName = $"{file.Name} ({file.Size / 1024} KB)";
            _importResult = null;

            _selectedJsonContent = null;
            _selectedJsonFileName = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取节点 CSV 失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedNodesCsvName = null;
            _selectedNodesCsvContent = null;
        }

        StateHasChanged();
    }

    private async Task OnEdgesCsvSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedEdgesCsvContent = await reader.ReadToEndAsync();
            _selectedEdgesCsvName = $"{file.Name} ({file.Size / 1024} KB)";
            _importResult = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取边 CSV 失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedEdgesCsvName = null;
            _selectedEdgesCsvContent = null;
        }

        StateHasChanged();
    }

    private void ResetImportState()
    {
        _selectedJsonFileName = null;
        _selectedJsonContent = null;
        _selectedNodesCsvName = null;
        _selectedNodesCsvContent = null;
        _selectedEdgesCsvName = null;
        _selectedEdgesCsvContent = null;
        _importResult = null;
        _clearBeforeImport = false;
        StateHasChanged();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    // ─── UI 辅助 ─────────────────────────────────────
    private async Task SwitchTabAsync(string tab)
    {
        _editingNodeGuid = null;
        _editingNodeName = "";
        _editingEdgeGuid = null;
        _editingEdgeName = "";

        _activeTab = tab;

        if (tab == "topology" &&
            _topologyNodes.Count == 0 &&
            !string.IsNullOrEmpty(_selectedGraphGuid))
        {
            await LoadTopologyAsync();
        }
        else if (tab == "import-export")
        {
            _importResult = null;
            StateHasChanged();
        }
        else
        {
            StateHasChanged();
        }
    }

    private void ToggleNodeSelection(Guid guid, bool selected)
    {
        if (selected) _selectedNodeGuids.Add(guid);
        else _selectedNodeGuids.Remove(guid);
    }

    private void ShowNodeJson(NodeDto node) =>
        _detailJson = JsonSerializer.Serialize(node, PrettyJson);

    private void ShowEdgeJson(EdgeDto edge) =>
        _detailJson = JsonSerializer.Serialize(edge, PrettyJson);

    private void CloseDetailModal()
    {
        _detailJson = null;
        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════
    // 图管理
    // ══════════════════════════════════════════════════════

    private void OpenManageDialog()
    {
        _showManageDialog = true;
        _newGraphName = "";
        _renamingGraphGuid = null;
        _renamingGraphName = "";
        _errorMessage = null;
        StateHasChanged();
    }

    private void CloseManageDialog()
    {
        _showManageDialog = false;
        StateHasChanged();
    }

    private async Task CreateGraphAsync()
    {
        if (string.IsNullOrWhiteSpace(_newGraphName)) return;

        _isCreatingGraph = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.CreateGraphAsync(_newGraphName.Trim());
            if (ok)
            {
                _newGraphName = "";
                await LoadGraphsAsync();
            }
            else
            {
                _errorMessage = "创建图失败。";
            }
        }
        finally
        {
            _isCreatingGraph = false;
            StateHasChanged();
        }
    }

    private async Task ConfirmRenameAsync()
    {
        if (_renamingGraphGuid is null || string.IsNullOrWhiteSpace(_renamingGraphName))
            return;

        var newName = _renamingGraphName.Trim();
        var existing = _graphs.FirstOrDefault(g => g.Guid == _renamingGraphGuid.Value);

        if (existing is null || existing.Name == newName)
        {
            CancelRename();
            return;
        }

        _isManaging = true;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.UpdateGraphAsync(existing.Guid, newName);
            if (ok)
            {
                existing.Name = newName;      // ★ 若 GraphDto.Name 无 setter，见下方说明
                CancelRename();
            }
            else
            {
                _errorMessage = "重命名失败。";
            }
        }
        finally
        {
            _isManaging = false;
            StateHasChanged();
        }
    }

    private async Task DeleteGraphAsync(GraphDto g)
    {
        var confirmed = await Js.InvokeAsync<bool>("confirm",
            $"删除图 '{g.Name}' 将同时删除其所有节点和边，此操作不可撤销。\n\n确定删除？");
        if (!confirmed) return;

        _isManaging = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.DeleteGraphAsync(g.Guid);
            if (ok)
            {
                _graphs.RemoveAll(x => x.Guid == g.Guid);
                if (_selectedGraphGuid == g.Guid.ToString())
                {
                    _selectedGraphGuid = "";
                    _nodes.Clear();
                    _edges.Clear();
                    _topologyNodes.Clear();
                    _topologyEdges.Clear();
                    _nodePositions.Clear();
                }

                await LoadGraphsAsync();
            }
            else
            {
                _errorMessage = "删除失败。";
            }
        }
        finally
        {
            _isManaging = false;
            StateHasChanged();
        }
    }

    private void StartRename(GraphDto g)
    {
        _renamingGraphGuid = g.Guid;
        _renamingGraphName = g.Name;
        StateHasChanged();
    }

    private void CancelRename()
    {
        _renamingGraphGuid = null;
        _renamingGraphName = "";
        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════
    // 节点/边行内编辑
    // ══════════════════════════════════════════════════════

    private void StartEditNode(NodeDto node)
    {
        _editingNodeGuid = node.Guid;
        _editingNodeName = node.Name;
        StateHasChanged();
    }

    private void CancelEditNode()
    {
        _editingNodeGuid = null;
        _editingNodeName = "";
        StateHasChanged();
    }

    private void StartEditEdge(EdgeDto edge)
    {
        _editingEdgeGuid = edge.Guid;
        _editingEdgeName = edge.Name;
        StateHasChanged();
    }

    private void CancelEditEdge()
    {
        _editingEdgeGuid = null;
        _editingEdgeName = "";
        StateHasChanged();
    }

    private async Task HandleEditKeyDown(KeyboardEventArgs e, Func<Task> onSave)
    {
        if (e.Key == "Enter") await onSave();
        else if (e.Key == "Escape")
        {
            CancelEditNode();
            CancelEditEdge();
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

    public void Dispose() { }

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