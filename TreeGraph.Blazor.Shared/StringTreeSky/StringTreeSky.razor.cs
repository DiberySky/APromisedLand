
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Shared.Eav.Dtos;
using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky;

public partial class StringTreeSky : ComponentBase
{
    [Parameter] public EventCallback<StringNodeDto> OnNodeSelected { get; set; }
    [Parameter] public EventCallback OnTreeChanged { get; set; }
    [Parameter] public string? TreeKey { get; set; }

    /// <summary>
    /// 显式指定 EAV EntityType。优先级最高。
    /// 空间场景传 space.EntityType；非空间场景可省略。
    /// </summary>
    [Parameter] public string? ExplicitEntityType { get; set; }

    /// <summary>
    /// 根节点 Id。null = 显示所有根（兼容旧行为）；
    /// 非 null = 只渲染该节点下的子树（用于“进入某空间”）。
    /// </summary>
    [Parameter] public string? RootNodeId { get; set; }

    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NodePropertySummaryService SummaryService { get; set; } = default!;
    [Inject] private EavApiClient EavApi { get; set; } = default!;          // ★ 新增
    [Inject] private NodeSchemaCache SchemaCache { get; set; } = default!;  // ★ 新增

    // 优先级链：ExplicitEntityType > TreeKey > 默认 StringTreeNode
    private string EntityType =>
        !string.IsNullOrWhiteSpace(ExplicitEntityType) ? ExplicitEntityType
        : !string.IsNullOrWhiteSpace(TreeKey) ? $"{StringTreeEntityTypes.Node}:{TreeKey}"
        : StringTreeEntityTypes.Node;

    private sealed class Row
    {
        public StringNodeDto Node { get; init; } = default!;
        public int Level { get; init; }
        public bool Expanded { get; set; }
        public bool Loaded { get; set; }
        public string? Summary { get; set; }
    }

    private readonly List<Row> _all = new();
    private List<Row> _visible = new();
    private readonly HashSet<string> _expandedIds = new();
    private bool _loading;

    private string? _editingId;
    private string _editingName = string.Empty;

    private string? _lastRootNodeId;

    // ============ 新增：过滤态 ============
    private bool _isFiltered;
    private HashSet<string>? _visibleIds;        // 过滤后应显示的节点
    private HashSet<string>? _matchedIds;        // 直接命中的节点（高亮用）

    // Schema 缓存（供过滤面板使用）
    private IReadOnlyList<AttributeSchemaDto> _schema = Array.Empty<AttributeSchemaDto>();

    protected override async Task OnInitializedAsync()
    {
        // 首次加载已按 RootNodeId 取根，预置标记避免 OnParametersSetAsync 首帧重复 Reload。
        _lastRootNodeId = RootNodeId;

        if (Options.AllowFilter)
        {
            try { _schema = await SchemaCache.GetAsync(EntityType); }
            catch { _schema = Array.Empty<AttributeSchemaDto>(); }
        }
        await ReloadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // 检测 RootNodeId 变化（切换空间时自动重载）
        if (_lastRootNodeId != RootNodeId)
        {
            _lastRootNodeId = RootNodeId;
            _expandedIds.Clear();
            _visibleIds = null;
            _matchedIds = null;
            _isFiltered = false;

            await ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        _all.Clear();

        // ★ 根据 RootNodeId 决定根列表：null = 所有根；非 null = 仅该节点（空间子树）
        List<StringNodeDto> roots;
        if (string.IsNullOrEmpty(RootNodeId))
        {
            roots = await Client.GetRootNodesAsync();
        }
        else
        {
            var root = await Client.GetNodeAsync(RootNodeId);
            roots = root is null
                ? new List<StringNodeDto>()
                : new List<StringNodeDto> { root };
        }

        foreach (var root in roots)
        {
            _all.Add(new Row
            {
                Node = root,
                Level = 0,
                Expanded = _expandedIds.Contains(root.Id)
            });
        }

        if (_expandedIds.Count == 0 && Options.DefaultExpandLevel > 0)
        {
            await ExpandDefaultAsync();
        }
        else
        {
            foreach (var id in _expandedIds.ToList())
            {
                var row = _all.FirstOrDefault(r => r.Node.Id == id);
                if (row != null) await EnsureChildrenAsync(row);
            }
        }

        _loading = false;
        RebuildVisible();
        await LoadSummariesForVisibleAsync();
    }

    private async Task ExpandDefaultAsync()
    {
        var level = 0;
        var current = _all.Where(r => r.Level == level).ToList();
        while (level < Options.DefaultExpandLevel && current.Count > 0)
        {
            foreach (var row in current)
            {
                await EnsureChildrenAsync(row);
                row.Expanded = true;
                _expandedIds.Add(row.Node.Id);
            }
            level++;
            current = _all.Where(r => r.Level == level).ToList();
        }
    }

    private async Task EnsureChildrenAsync(Row parent)
    {
        if (parent.Loaded) return;
        var children = await Client.GetChildrenAsync(parent.Node.Id);

        RemoveDescendants(parent.Node.Id);

        var insertAt = _all.IndexOf(parent) + 1;
        var newRows = new List<Row>(children.Count);
        foreach (var child in children)
        {
            var row = new Row
            {
                Node = child,
                Level = parent.Level + 1,
                Expanded = _expandedIds.Contains(child.Id)
            };
            _all.Insert(insertAt++, row);
            newRows.Add(row);
        }
        parent.Loaded = true;

        if (SummaryService.IsEnabled && newRows.Count > 0)
        {
            var map = await SummaryService.GetSummariesAsync(
                EntityType, newRows.Select(r => r.Node.Id));
            foreach (var row in newRows)
                if (map.TryGetValue(row.Node.Id, out var s))
                    row.Summary = s;

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadSummariesForVisibleAsync()
    {
        if (!SummaryService.IsEnabled) return;
        var visibleRows = _visible.ToList();
        if (visibleRows.Count == 0) return;

        var map = await SummaryService.GetSummariesAsync(
            EntityType, visibleRows.Select(r => r.Node.Id));

        foreach (var row in visibleRows)
            if (map.TryGetValue(row.Node.Id, out var s))
                row.Summary = s;

        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleAsync(Row row)
    {
        if (row.Expanded)
        {
            row.Expanded = false;
            _expandedIds.Remove(row.Node.Id);
        }
        else
        {
            await EnsureChildrenAsync(row);
            row.Expanded = true;
            _expandedIds.Add(row.Node.Id);
        }
        RebuildVisible();
        await Task.CompletedTask;
    }

    private void RebuildVisible()
    {
        IEnumerable<Row> query = _all;

        // ★ 过滤态：只显示 _visibleIds 内的节点
        if (_isFiltered && _visibleIds is not null)
            query = query.Where(r => _visibleIds.Contains(r.Node.Id));

        _visible = query
            .Where(r => _isFiltered || IsVisible(r))   // 过滤态跳过展开判断
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Node.SortOrder)
            .ThenBy(r => r.Node.Id)
            .ToList();
    }

    private bool IsVisible(Row row)
    {
        var current = row.Node;
        while (current.ParentId is not null)
        {
            if (!_expandedIds.Contains(current.ParentId)) return false;
            var parentRow = _all.FirstOrDefault(r => r.Node.Id == current.ParentId);
            if (parentRow == null) return false;
            current = parentRow.Node;
        }
        return true;
    }

    private void RemoveDescendants(string parentId)
    {
        var stack = new Stack<string>();
        stack.Push(parentId);
        var toRemove = new List<string>();
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            foreach (var child in _all.Where(r => r.Node.ParentId == id).ToList())
            {
                toRemove.Add(child.Node.Id);
                stack.Push(child.Node.Id);
            }
        }
        _all.RemoveAll(r => toRemove.Contains(r.Node.Id));
    }

    // ============ 过滤逻辑 ============

    /// <summary>
    /// 应用过滤条件：
    ///   1) 走 EAV 查询拿匹配的 EntityId（= NodeId）
    ///   2) 并发查每个匹配节点的祖先链
    ///   3) 合并得 _visibleIds，重新构建树
    /// </summary>
    private async Task ApplyFilterAsync(List<AttributeFilter> filters)
    {
        if (filters.Count == 0)
        {
            await ClearFilterAsync();
            return;
        }

        _loading = true;
        try
        {
            // 1) EAV 查询
            var request = new EavQueryRequest
            {
                EntityType = EntityType,
                Filters = filters,
                Page = 1,
                PageSize = Options.FilterPageSize
            };

            var page = await EavApi.QueryAsync(EntityType, request);
            if (page is null || page.Items.Count == 0)
            {
                _isFiltered = true;
                _visibleIds = new HashSet<string>();
                _matchedIds = new HashSet<string>();
                _all.Clear();
                _visible = new List<Row>();
                return;
            }

            // 2) 提取匹配的节点 Id
            var matched = page.Items
                .Select(e => e.EntityId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            // 3) 并发查祖先链
            var visible = new HashSet<string>(matched);
            var pathTasks = matched.Select(id => Client.GetAncestorPathAsync(id));
            var paths = await Task.WhenAll(pathTasks);
            foreach (var path in paths)
                foreach (var node in path)
                    if (!string.IsNullOrEmpty(node.Id))
                        visible.Add(node.Id);

            // 4) 拉取这些节点的完整数据（用 GetNodeAsync 并发）
            await LoadFilteredNodesAsync(visible, matched);

            _isFiltered = true;
            _visibleIds = visible;
            _matchedIds = matched;
        }
        finally
        {
            _loading = false;
        }

        RebuildVisible();
        await LoadSummariesForVisibleAsync();
    }

    /// <summary>
    /// 过滤态下一次性加载指定节点的数据，并构造层级关系。
    /// </summary>
    private async Task LoadFilteredNodesAsync(
        HashSet<string> visibleIds, HashSet<string> matchedIds)
    {
        _all.Clear();

        // 并发拉取所有可见节点的 DTO
        var nodeTasks = visibleIds.Select(id => Client.GetNodeAsync(id));
        var nodes = await Task.WhenAll(nodeTasks);

        var byId = nodes
            .Where(n => n is not null)
            .ToDictionary(n => n!.Id, n => n!);

        // 从根开始递归插入（保证 _all 顺序对应层级顺序）
        var inserted = new HashSet<string>();

        void InsertSubtree(string nodeId, int level)
        {
            if (!byId.TryGetValue(nodeId, out var node)) return;
            if (inserted.Contains(nodeId)) return;
            inserted.Add(nodeId);

            _all.Add(new Row
            {
                Node = node,
                Level = level,
                Expanded = true,     // 过滤态全部展开
                Loaded = true
            });

            var childIds = visibleIds
                .Where(cid =>
                    byId.TryGetValue(cid, out var child) &&
                    child.ParentId == nodeId)
                .ToList();

            foreach (var childId in childIds)
                InsertSubtree(childId, level + 1);
        }

        var rootIds = visibleIds
            .Where(id => byId.TryGetValue(id, out var n) && n.ParentId is null)
            .ToList();

        foreach (var rootId in rootIds)
            InsertSubtree(rootId, 0);
    }

    /// <summary>清除过滤，恢复懒加载树。</summary>
    private async Task ClearFilterAsync()
    {
        _isFiltered = false;
        _visibleIds = null;
        _matchedIds = null;
        _expandedIds.Clear();

        await ReloadAsync();
    }

    // ============ 现有业务方法（略作调整） ============

    private async Task CreateRootAsync()
    {
        // 有空间时，新节点挂在当前空间下
        var dto = new StringNodeDto
        {
            Name = "新建节点",
            ParentId = string.IsNullOrEmpty(RootNodeId) ? null : RootNodeId
        };
        await Client.CreateNodeAsync(dto);
        await ReloadAsync();
        await NotifyChangedAsync();
    }

    private async Task AddChildAsync(Row parent)
    {
        var dto = new StringNodeDto { Name = "新建子节点", ParentId = parent.Node.Id };
        await Client.CreateNodeAsync(dto);

        if (!parent.Node.HasChildren) parent.Node.HasChildren = true;

        if (parent.Loaded)
        {
            RemoveDescendants(parent.Node.Id);
            parent.Loaded = false;
        }
        await EnsureChildrenAsync(parent);
        parent.Expanded = true;
        _expandedIds.Add(parent.Node.Id);

        RebuildVisible();
        await NotifyChangedAsync();
    }

    private async Task OpenPropertiesAsync(StringNodeDto node)
    {
        var parameters = new DialogParameters
        {
            { nameof(StringNodePropertiesDialog.NodeId), node.Id },
            { nameof(StringNodePropertiesDialog.NodeName), node.Name },
            { nameof(StringNodePropertiesDialog.EntityType), EntityType }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<StringNodePropertiesDialog>(
            "节点属性", parameters, options);

        var result = await dialog.Result;

        if (result is { Canceled: false } && SummaryService.IsEnabled)
        {
            SummaryService.Invalidate(EntityType, node.Id);
            var summary = await SummaryService.GetSummaryAsync(EntityType, node.Id);
            var row = _all.FirstOrDefault(r => r.Node.Id == node.Id);
            if (row is not null)
            {
                row.Summary = summary;
                await InvokeAsync(StateHasChanged);
            }

            // 过滤态下保存后可能需要重新计算（例如过滤条件正是被修改的属性）
            if (_isFiltered)
                await InvokeAsync(StateHasChanged);
        }
    }

    private void StartEdit(StringNodeDto node)
    {
        _editingId = node.Id;
        _editingName = node.Name;
    }

    private void CancelEdit()
    {
        _editingId = null;
        _editingName = string.Empty;
    }

    private async Task OnEditKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            var row = _all.FirstOrDefault(r => r.Node.Id == _editingId);
            if (row != null) await CommitEditAsync(row.Node);
        }
        else if (e.Key == "Escape")
        {
            CancelEdit();
        }
    }

    private async Task CommitEditAsync(StringNodeDto node)
    {
        if (string.IsNullOrWhiteSpace(_editingName)) return;
        node.Name = _editingName.Trim();
        await Client.UpdateNodeAsync(node);
        _editingId = null;
        _editingName = string.Empty;
        await NotifyChangedAsync();
    }

    private async Task DeleteAsync(Row row)
    {
        await Client.DeleteNodeAsync(row.Node.Id);

        _expandedIds.Remove(row.Node.Id);
        RemoveDescendants(row.Node.Id);
        _all.RemoveAll(r => r.Node.Id == row.Node.Id);
        RebuildVisible();

        SummaryService.Invalidate(EntityType, row.Node.Id);

        await NotifyChangedAsync();
    }

    private async Task NotifyChangedAsync()
    {
        if (OnTreeChanged.HasDelegate) await OnTreeChanged.InvokeAsync();
    }
}
