using System.Text.Encodings.Web;
using System.Text.Json;
using LiteGraph.Sdk;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class McpGraphExplorer : ComponentBase, IDisposable
{
    // ─── 注入 ─────────────────────────────────────────
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;

    // ─── 状态字段 ─────────────────────────────────────
    private List<Graph> _graphs = new();
    private List<Node> _nodes = new();
    private List<Edge> _edges = new();

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

    /// <summary>LiteGraph SDK 限制：MaxResults 最大 1000。</summary>
    private const int MaxEnumerationResults = 1000;

    // CRUD 输入
    private string _newNodeName = "";
    private readonly HashSet<Guid> _selectedNodeGuids = new();
    private string _newEdgeFrom = "";
    private string _newEdgeTo = "";
    private string _newEdgeName = "";

    // 向量搜索
    private string _vectorQueryText = "";
    private List<VectorSearchDisplayResult> _vectorResults = new();
    private string _vectorProgress = "";

    // ★ P2: 拓扑图状态
    private List<Node> _topologyNodes = new();
    private List<Edge> _topologyEdges = new();
    private Node? _selectedTopologyNode;
    private readonly Dictionary<Guid, (double X, double Y)> _nodePositions = new();

    // 常量
    private static readonly Guid DefaultTenant = Guid.Empty;

    private const string OllamaEmbeddingUrl = "http://localhost:11618/api/embeddings";
    private const string EmbeddingModel = "bge-large";

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
            var result = await LiteGraph.Graph.ReadMany(DefaultTenant);
            _graphs = result.Objects ?? new List<Graph>();
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
        _vectorResults.Clear();
        _vectorProgress = "";

        // ★ P2：清空拓扑状态
        _topologyNodes.Clear();
        _topologyEdges.Clear();
        _nodePositions.Clear();
        _selectedTopologyNode = null;

        await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
    }

    // ─── 节点查询 ─────────────────────────────────────
    private async Task LoadNodesAsync()
    {
        await LoadNodesAsync(null);
    }

    private async Task LoadNodesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        var graphGuid = Guid.Parse(_selectedGraphGuid);
        _isBusy = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var query = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = PageSize,
                ContinuationToken = continuationToken
            };

            var result = await LiteGraph.Node.Enumerate(query);
            _nodes = result.Objects ?? new List<Node>();
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
    private async Task LoadEdgesAsync()
    {
        await LoadEdgesAsync(null);
    }

    private async Task LoadEdgesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        var graphGuid = Guid.Parse(_selectedGraphGuid);
        _isBusy = true;
        StateHasChanged();

        try
        {
            var query = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = PageSize,
                ContinuationToken = continuationToken
            };

            var result = await LiteGraph.Edge.Enumerate(query);
            _edges = result.Objects ?? new List<Edge>();
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
            var node = new Node
            {
                TenantGUID = DefaultTenant,
                GraphGUID = Guid.Parse(_selectedGraphGuid),
                Name = _newNodeName.Trim()
            };
            await LiteGraph.Node.Create(node);
            _newNodeName = "";
            await LoadNodesAsync();
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
            await LiteGraph.Node.DeleteByGuid(
                DefaultTenant,
                Guid.Parse(_selectedGraphGuid),
                nodeGuid);
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
            foreach (var guid in _selectedNodeGuids)
            {
                await LiteGraph.Node.DeleteByGuid(
                    DefaultTenant,
                    Guid.Parse(_selectedGraphGuid),
                    guid);
            }
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
            var edge = new Edge
            {
                TenantGUID = DefaultTenant,
                GraphGUID = Guid.Parse(_selectedGraphGuid),
                From = Guid.Parse(_newEdgeFrom),
                To = Guid.Parse(_newEdgeTo),
                Name = string.IsNullOrWhiteSpace(_newEdgeName) ? "RELATED_TO" : _newEdgeName.Trim()
            };
            await LiteGraph.Edge.Create(edge);
            _newEdgeFrom = _newEdgeTo = _newEdgeName = "";
            await LoadEdgesAsync();
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
            await LiteGraph.Edge.DeleteByGuid(
                DefaultTenant,
                Guid.Parse(_selectedGraphGuid),
                edgeGuid);
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

    // ─── 向量搜索 ─────────────────────────────────────

    /// <summary>
    /// 调用 Ollama 生成 embedding。
    /// </summary>
    private async Task<List<float>?> GetEmbeddingAsync(string text)
    {
        try
        {
            var http = HttpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync(
                OllamaEmbeddingUrl,
                new { model = EmbeddingModel, prompt = text });

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _errorMessage = $"Ollama 返回 {response.StatusCode}: {body}";
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("embedding", out var arr))
            {
                _errorMessage = "Ollama 响应中没有 embedding 字段";
                return null;
            }

            return arr.EnumerateArray().Select(x => x.GetSingle()).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "调用 Ollama embedding 失败");
            _errorMessage = $"生成 embedding 失败: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// 余弦相似度计算。
    /// </summary>
    private static float CosineSimilarity(List<float> a, List<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0f;

        float dot = 0f, na = 0f, nb = 0f;
        for (int i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na == 0f || nb == 0f) return 0f;
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb));
    }

    /// <summary>
    /// 语义搜索：文本 → embedding → 手动遍历所有向量计算相似度。
    /// </summary>
    private async Task SearchVectorAsync()
    {
        if (string.IsNullOrWhiteSpace(_vectorQueryText) ||
            string.IsNullOrEmpty(_selectedGraphGuid))
            return;

        _isBusy = true;
        _errorMessage = null;
        _vectorResults.Clear();
        _vectorProgress = "生成 embedding...";
        StateHasChanged();

        try
        {
            var queryEmbedding = await GetEmbeddingAsync(_vectorQueryText);
            if (queryEmbedding == null || queryEmbedding.Count == 0) return;

            Logger.LogInformation("查询 embedding 维度：{Dim}", queryEmbedding.Count);
            _vectorProgress = "加载所有向量...";
            StateHasChanged();

            var graphGuid = Guid.Parse(_selectedGraphGuid);

            var vectorQuery = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = MaxEnumerationResults
            };

            var vectorResult = await LiteGraph.Vector.Enumerate(vectorQuery);
            var allVectors = vectorResult.Objects ?? new List<VectorMetadata>();

            Logger.LogInformation("加载到 {Count} 个向量", allVectors.Count);
            _vectorProgress = $"计算相似度（{allVectors.Count} 个向量）...";
            StateHasChanged();

            var results = new List<VectorSearchDisplayResult>();

            foreach (var v in allVectors)
            {
                if (v.Vectors == null || v.Vectors.Count == 0) continue;
                if (v.NodeGUID == null) continue;

                var score = CosineSimilarity(queryEmbedding, v.Vectors);

                var nodeName = _nodes.FirstOrDefault(n => n.GUID == v.NodeGUID.Value)?.Name
                               ?? $"[节点 {v.NodeGUID.Value.ToString("N")[..8]}]";

                results.Add(new VectorSearchDisplayResult
                {
                    NodeName = nodeName,
                    Score = score
                });
            }

            _vectorResults = results
                .OrderByDescending(r => r.Score)
                .Take(20)
                .ToList();

            _vectorProgress = $"返回 {_vectorResults.Count} 条结果";
            Logger.LogInformation("向量搜索完成，返回 {Count} 条结果", _vectorResults.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "向量搜索失败");
            _errorMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 为当前图所有节点生成并保存向量。
    /// </summary>
    private async Task GenerateVectorsForAllNodesAsync()
    {
        if (string.IsNullOrEmpty(_selectedGraphGuid)) return;

        if (!await ConfirmAsync("将为当前图的所有节点生成向量（可能需要几分钟），继续？"))
            return;

        _isBusy = true;
        _errorMessage = null;
        _vectorProgress = "";
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(_selectedGraphGuid);

            var query = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                MaxResults = MaxEnumerationResults
            };
            var result = await LiteGraph.Node.Enumerate(query);
            var allNodes = result.Objects ?? new List<Node>();

            int success = 0, fail = 0;
            int total = allNodes.Count;
            int i = 0;

            foreach (var node in allNodes)
            {
                i++;
                _vectorProgress = $"({i}/{total}) {node.Name}";
                StateHasChanged();

                if (string.IsNullOrWhiteSpace(node.Name))
                {
                    fail++;
                    continue;
                }

                var embedding = await GetEmbeddingAsync(node.Name);
                if (embedding == null)
                {
                    fail++;
                    continue;
                }

                try
                {
                    var metadata = new VectorMetadata
                    {
                        TenantGUID = DefaultTenant,
                        GraphGUID = graphGuid,
                        NodeGUID = node.GUID,
                        Model = EmbeddingModel,
                        Dimensionality = embedding.Count,
                        Vectors = embedding
                    };
                    await LiteGraph.Vector.Create(metadata);
                    success++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "为节点 {Name} 创建向量失败", node.Name);
                    fail++;
                }
            }

            _vectorProgress = $"完成：成功 {success} / 失败 {fail}";
            Logger.LogInformation("向量生成完成: 成功 {S} / 失败 {F}", success, fail);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "批量生成向量失败");
            _errorMessage = $"生成向量失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    // ─── 拓扑图 ──────────────────────────────────────

    /// <summary>
    /// 加载当前图的所有节点和边，用于拓扑渲染。
    /// </summary>
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

            var nodeQuery = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = MaxEnumerationResults
            };
            var nodeResult = await LiteGraph.Node.Enumerate(nodeQuery);
            _topologyNodes = nodeResult.Objects ?? new List<Node>();

            var edgeQuery = new EnumerationRequest
            {
                TenantGUID = DefaultTenant,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = MaxEnumerationResults
            };
            var edgeResult = await LiteGraph.Edge.Enumerate(edgeQuery);
            _topologyEdges = edgeResult.Objects ?? new List<Edge>();

            ComputeCircularLayout();

            Logger.LogInformation(
                "拓扑图加载：{N} 个节点 / {E} 条边",
                _topologyNodes.Count, _topologyEdges.Count);
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

    /// <summary>
    /// 圆形布局：所有节点均匀分布在一个圆周上。
    /// </summary>
    private void ComputeCircularLayout()
    {
        _nodePositions.Clear();
        int n = _topologyNodes.Count;
        if (n == 0) return;

        const double cx = 400, cy = 300;
        double radius = Math.Min(220, Math.Max(100, n * 8));

        for (int i = 0; i < n; i++)
        {
            // 从正上方开始顺时针分布
            double angle = 2 * Math.PI * i / n - Math.PI / 2;
            double x = cx + radius * Math.Cos(angle);
            double y = cy + radius * Math.Sin(angle);
            _nodePositions[_topologyNodes[i].GUID] = (x, y);
        }
    }

    /// <summary>
    /// 查询节点坐标；若节点不在图中返回 null。
    /// </summary>
    private (double X, double Y)? GetPosition(Guid nodeGuid)
    {
        return _nodePositions.TryGetValue(nodeGuid, out var p) ? p : null;
    }

    /// <summary>
    /// 点击节点：切换高亮状态。
    /// </summary>
    private void SelectTopologyNode(Node node)
    {
        _selectedTopologyNode = _selectedTopologyNode?.GUID == node.GUID ? null : node;
        StateHasChanged();
    }

    // ─── UI 辅助 ─────────────────────────────────────
    private async Task SwitchTabAsync(string tab)
    {
        _activeTab = tab;

        // 切到拓扑图且尚未加载时，自动加载
        if (tab == "topology" &&
            _topologyNodes.Count == 0 &&
            !string.IsNullOrEmpty(_selectedGraphGuid))
        {
            await LoadTopologyAsync();
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

    private void ShowNodeJson(Node node) =>
        _detailJson = JsonSerializer.Serialize(node, PrettyJson);

    private void ShowEdgeJson(Edge edge) =>
        _detailJson = JsonSerializer.Serialize(edge, PrettyJson);

    private void CloseDetailModal()
    {
        _detailJson = null;
        StateHasChanged();
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        try
        {
            return await Js.InvokeAsync<bool>("confirm", message);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "JS confirm 调用失败，默认允许操作");
            return true;
        }
    }

    public void Dispose() { }

    // ─── 向量搜索显示 DTO ────────────────────────────
    private sealed class VectorSearchDisplayResult
    {
        public string NodeName { get; set; } = "";
        public float Score { get; set; }
    }
}