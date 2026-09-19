从你列出的错误日志来看，主要有三个问题：

1. **Razor 嵌套双引号**：`@onclick="() => SwitchTab("nodes")"` 里内层 `"nodes"` 提前终止了 `@onclick` 属性值，导致 Razor 解析崩溃，进而报出“意外的标记”、“SwitchTab 具有 1 个形参但使用 0 个实参调用”等一连串错误。
2. **向量搜索 `Search` 方法不存在**：`LiteGraphSdk.Vector.Search(...)` 在这个 SDK 版本中不是这个签名。暂时移除向量搜索 Tab，确保能编译。
3. **字段命名规范**：IDE 建议私有字段用 `_camelCase` 前缀。这是警告不阻止编译，但既然要重写，顺便改掉。

下面是修复后的完整代码。

---

### 📄 `Components/Pages/McpGraphExplorer.razor`

```razor
@page "/graph-explorer"
@using System.Linq
@using System.Text.Json
@using System.Text.Encodings.Web
@using LiteGraph.Sdk
@inject LiteGraphSdk LiteGraph
@inject ILogger<McpGraphExplorer> Logger
@rendermode InteractiveServer

<PageTitle>图数据查询</PageTitle>

<div class="explorer-container">
    <div class="explorer-header">
        <h1>🔍 MCP 图数据 Explorer</h1>
        <p class="page-subtitle">节点 / 边 CRUD + 分页 —— 基于 LiteGraph SDK</p>
    </div>

    <!-- 1. 图选择工具栏 -->
    <div class="toolbar">
        <select @bind="_selectedGraphGuid" @bind:after="OnGraphChangedAsync" class="graph-selector">
            <option value="">-- 选择图 --</option>
            @foreach (var g in _graphs)
            {
                <option value="@g.GUID">@g.Name</option>
            }
        </select>
        <button class="btn-refresh" @onclick="LoadGraphsAsync" disabled="@_isLoading">🔄 刷新图列表</button>
    </div>

    @if (!string.IsNullOrEmpty(_selectedGraphGuid))
    {
        <!-- 2. Tab 切换（注意：使用单引号包裹 @onclick 值，避免嵌套双引号） -->
        <div class="tab-bar">
            <button class="tab @(_activeTab == "nodes" ? "active" : "")"
                    @onclick='() => SwitchTab("nodes")'>📦 节点 (@_nodeTotal)</button>
            <button class="tab @(_activeTab == "edges" ? "active" : "")"
                    @onclick='() => SwitchTab("edges")'>🔗 边 (@_edgeTotal)</button>
        </div>

        <!-- 3. 节点面板 -->
        @if (_activeTab == "nodes")
        {
            <div class="panel">
                <div class="crud-bar">
                    <input type="text" @bind="_newNodeName" placeholder="新节点名称" class="input-sm" />
                    <button class="btn-primary" @onclick="CreateNodeAsync" disabled="@_isBusy">+ 新建节点</button>
                    <button class="btn-danger"
                            @onclick="DeleteSelectedNodesAsync"
                            disabled="@(_selectedNodeGuids.Count == 0 || _isBusy)">
                        🗑️ 删除选中 (@_selectedNodeGuids.Count)
                    </button>
                </div>

                <div class="list-container">
                    @foreach (var node in _nodes)
                    {
                        <div class="list-item @(_selectedNodeGuids.Contains(node.GUID) ? "selected" : "")">
                            <input type="checkbox"
                                   checked="@_selectedNodeGuids.Contains(node.GUID)"
                                   @onchange="(e) => ToggleNodeSelection(node.GUID, (bool)e.Value!)" />
                            <div class="item-info">
                                <strong>@node.Name</strong>
                                <span class="guid">@node.GUID.ToString("N")[..8]</span>
                            </div>
                            <div class="item-actions">
                                <button class="btn-icon" @onclick="() => ShowNodeJson(node)">👁️</button>
                                <button class="btn-icon danger"
                                        @onclick="() => DeleteNodeAsync(node.GUID)"
                                        disabled="@_isBusy">🗑️</button>
                            </div>
                        </div>
                    }
                </div>

                <div class="pagination">
                    <button class="btn-page"
                            @onclick="PrevNodePageAsync"
                            disabled="@(!_nodeHasPrev || _isBusy)">⬅️ 上一页</button>
                    <span class="page-info">已加载 @_nodes.Count / 共 @_nodeTotal 条</span>
                    <button class="btn-page"
                            @onclick="NextNodePageAsync"
                            disabled="@(!_nodeHasNext || _isBusy)">下一页 ➡️</button>
                </div>
            </div>
        }

        <!-- 4. 边面板 -->
        @if (_activeTab == "edges")
        {
            <div class="panel">
                <div class="crud-bar">
                    <select @bind="_newEdgeFrom" class="input-sm">
                        <option value="">起点</option>
                        @foreach (var n in _nodes)
                        {
                            <option value="@n.GUID">@n.Name</option>
                        }
                    </select>
                    <select @bind="_newEdgeTo" class="input-sm">
                        <option value="">终点</option>
                        @foreach (var n in _nodes)
                        {
                            <option value="@n.GUID">@n.Name</option>
                        }
                    </select>
                    <input type="text" @bind="_newEdgeName" placeholder="边名称 (如 PARENT_OF)" class="input-sm" />
                    <button class="btn-primary" @onclick="CreateEdgeAsync" disabled="@_isBusy">+ 新建边</button>
                </div>

                <div class="list-container">
                    @foreach (var edge in _edges)
                    {
                        <div class="list-item">
                            <div class="item-info">
                                <strong>@edge.Name</strong>
                                <span class="guid">@edge.From.ToString("N")[..8] → @edge.To.ToString("N")[..8]</span>
                            </div>
                            <div class="item-actions">
                                <button class="btn-icon" @onclick="() => ShowEdgeJson(edge)">👁️</button>
                                <button class="btn-icon danger"
                                        @onclick="() => DeleteEdgeAsync(edge.GUID)"
                                        disabled="@_isBusy">🗑️</button>
                            </div>
                        </div>
                    }
                </div>

                <div class="pagination">
                    <button class="btn-page"
                            @onclick="PrevEdgePageAsync"
                            disabled="@(!_edgeHasPrev || _isBusy)">⬅️ 上一页</button>
                    <span class="page-info">已加载 @_edges.Count / 共 @_edgeTotal 条</span>
                    <button class="btn-page"
                            @onclick="NextEdgePageAsync"
                            disabled="@(!_edgeHasNext || _isBusy)">下一页 ➡️</button>
                </div>
            </div>
        }
    }

    <!-- JSON 详情弹窗 -->
    @if (_detailJson != null)
    {
        <div class="modal-overlay" @onclick="CloseDetailModal">
            <div class="modal-content" @onclick:stopPropagation="true">
                <div class="modal-header">
                    <strong>详情</strong>
                    <button class="btn-close" @onclick="CloseDetailModal">✕</button>
                </div>
                <pre>@_detailJson</pre>
            </div>
        </div>
    }

    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="error-bar">@_errorMessage</div>
    }
</div>
```

---

### 📄 `Components/Pages/McpGraphExplorer.razor.cs`

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using LiteGraph.Sdk;
using Microsoft.AspNetCore.Components;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class McpGraphExplorer : ComponentBase, IDisposable
{
    // ─── 状态字段（按 IDE 规范使用 _camelCase 命名）───
    private List<Graph> _graphs = new();
    private List<Node> _nodes = new();
    private List<Edge> _edges = new();

    private string _selectedGraphGuid = "";
    private string _activeTab = "nodes";
    private bool _isLoading;
    private bool _isBusy;
    private string? _errorMessage;
    private string? _detailJson;

    // 分页状态
    private Guid? _nodeContinuationToken;
    private long _nodeTotal;
    private bool _nodeHasPrev;
    private bool _nodeHasNext;

    private Guid? _edgeContinuationToken;
    private long _edgeTotal;
    private bool _edgeHasPrev;
    private bool _edgeHasNext;

    private const int PageSize = 20;

    // CRUD 输入
    private string _newNodeName = "";
    private readonly HashSet<Guid> _selectedNodeGuids = new();
    private string _newEdgeFrom = "";
    private string _newEdgeTo = "";
    private string _newEdgeName = "";

    private static readonly Guid DefaultTenant = Guid.Empty;

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

        await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
    }

    // ─── 节点查询（带分页）────────────────────────────
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

    // ─── 边查询（带分页）────────────────────────────
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

    // ─── CRUD：节点 ──────────────────────────────────
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

    // ─── CRUD：边 ────────────────────────────────────
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

    // ─── UI 辅助 ─────────────────────────────────────
    private void SwitchTab(string tab)
    {
        _activeTab = tab;
        StateHasChanged();
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

    /// <summary>
    /// 简易确认对话框。
    /// 当前版本直接返回 true，后续可替换为 IJSRuntime 调浏览器 confirm()。
    /// </summary>
    private Task<bool> ConfirmAsync(string message)
    {
        // 使用 _ = message 抑制“形参从未使用”警告，
        // 待接入 JS interop 后即可使用。
        _ = message;
        return Task.FromResult(true);
    }

    public void Dispose() { }
}
```

---

### 📄 `Components/Pages/McpGraphExplorer.razor.css`

```css
.explorer-container {
    padding: 24px;
    max-width: 1200px;
    margin: 0 auto;
}

.explorer-header h1 {
    font-size: 1.6rem;
    margin: 0 0 4px;
}

.page-subtitle {
    font-size: 0.85rem;
    color: #64748b;
    margin: 0 0 20px;
}

/* 工具栏 */
.toolbar {
    display: flex;
    gap: 12px;
    margin-bottom: 16px;
}

.graph-selector {
    padding: 8px 12px;
    border: 1px solid #cbd5e1;
    border-radius: 6px;
    min-width: 240px;
}

.btn-refresh {
    padding: 8px 16px;
    background: #4a90e2;
    color: #fff;
    border: none;
    border-radius: 6px;
    cursor: pointer;
}

/* Tab */
.tab-bar {
    display: flex;
    gap: 4px;
    margin-bottom: 16px;
    border-bottom: 2px solid #e2e8f0;
}

.tab {
    padding: 10px 20px;
    border: none;
    background: none;
    cursor: pointer;
    font-size: 0.9rem;
    color: #64748b;
    border-bottom: 2px solid transparent;
    margin-bottom: -2px;
}

.tab.active {
    color: #4a90e2;
    border-bottom-color: #4a90e2;
    font-weight: 600;
}

/* 面板 */
.panel {
    background: #fff;
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    overflow: hidden;
}

.crud-bar {
    display: flex;
    gap: 8px;
    padding: 12px 16px;
    background: #f8fafc;
    border-bottom: 1px solid #e2e8f0;
    flex-wrap: wrap;
    align-items: center;
}

.input-sm {
    padding: 6px 10px;
    border: 1px solid #cbd5e1;
    border-radius: 4px;
    font-size: 0.85rem;
}

.btn-primary {
    padding: 6px 14px;
    background: #4a90e2;
    color: #fff;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.85rem;
}

.btn-danger {
    padding: 6px 14px;
    background: #ef4444;
    color: #fff;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.85rem;
}

.btn-primary:disabled,
.btn-danger:disabled {
    background: #94a3b8;
    cursor: not-allowed;
}

/* 列表 */
.list-container {
    max-height: 420px;
    overflow-y: auto;
}

.list-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 16px;
    border-bottom: 1px solid #f1f5f9;
}

.list-item:hover {
    background: #f8fafc;
}

.list-item.selected {
    background: #eff6ff;
}

.item-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 2px;
}

.item-info strong {
    font-size: 0.9rem;
}

.guid {
    font-size: 0.75rem;
    color: #94a3b8;
    font-family: monospace;
}

.item-actions {
    display: flex;
    gap: 4px;
}

.btn-icon {
    background: none;
    border: none;
    cursor: pointer;
    font-size: 1rem;
    padding: 4px;
}

.btn-icon.danger:hover {
    background: #fef2f2;
    border-radius: 4px;
}

/* 分页 */
.pagination {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 16px;
    padding: 12px;
    background: #f8fafc;
    border-top: 1px solid #e2e8f0;
}

.btn-page {
    padding: 6px 14px;
    background: #fff;
    border: 1px solid #cbd5e1;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.85rem;
}

.btn-page:disabled {
    color: #cbd5e1;
    cursor: not-allowed;
}

.page-info {
    font-size: 0.85rem;
    color: #64748b;
}

/* 弹窗 */
.modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.4);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal-content {
    background: #fff;
    border-radius: 8px;
    max-width: 640px;
    width: 90%;
    max-height: 80vh;
    overflow: hidden;
    display: flex;
    flex-direction: column;
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 16px;
    border-bottom: 1px solid #e2e8f0;
}

.btn-close {
    background: none;
    border: none;
    font-size: 1.2rem;
    cursor: pointer;
}

.modal-content pre {
    padding: 16px;
    margin: 0;
    overflow: auto;
    font-size: 0.8rem;
    background: #f8fafc;
}

/* 错误 */
.error-bar {
    margin-top: 16px;
    padding: 12px 16px;
    background: #fef2f2;
    border-left: 4px solid #ef4444;
    border-radius: 4px;
    color: #991b1b;
    font-size: 0.85rem;
}
```

---

### 🔑 关键修复点

| 原问题 | 修复 |
|---|---|
| **`SwitchTab` 具有 1 个形参但使用 0 个实参调用** | 用**单引号**包裹 `@onclick` 的值：`@onclick='() => SwitchTab("nodes")'`，避免内层 `"nodes"` 提前闭合属性值 |
| **意外的标记 / 标记开始未关闭** | 同上，Razor 编译恢复正常 |
| **无法解析符号 `Search`** | 暂时移除向量搜索 Tab，仅保留节点和边的 CRUD |
| **`new()` 没有目标类型** | 所有 `new()` 都改为 `new List<T>()` 或 `new()` 在明确类型上下文中 |
| **`形参 'continuationToken' 具有相同的默认值`** | 拆分为两个重载：无参版本调用有参版本，有参版本不设默认值 |
| **`形参 'message' 从未使用`** | 用 `_ = message;` 显式抑制警告 |
| **私有字段命名规范警告** | 全部字段重命名为 `_camelCase`，Razor 中的绑定同步更新 |

---

### 🚀 验证步骤

1. 保存三个文件，运行 `dotnet build`。
2. 若编译通过，重启 AppHost，打开 `/graph-explorer`：
    - 从下拉框选择 `TestGraph` → 应看到 3 个节点
    - 切换到"🔗 边"Tab → 应看到 `PARENT_OF` 边
    - 尝试"新建节点"、单条/批量"删除"、"查看 JSON 详情"
    - 分页按钮应有反应

### 📌 关于向量搜索的后续

`LiteGraph.Vector.Search` 在你的 SDK 版本中不存在。等你确认了正确的方法名（在 VS 中对 `LiteGraph.Vector` 按 F12，看它有哪些方法），或者把 SDK 对象浏览器截图发我，我再帮你把向量搜索 Tab 加回来。它现在只是个未解决的 SDK 签名问题，不影响节点和边的所有功能。