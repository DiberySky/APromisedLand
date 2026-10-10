
using MudBlazor;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky;

public partial class StringTreeSky
{
    // ========== 数据加载（对齐 TreeSky.razor.Loading 的职责拆分） ==========

    /// <summary>重建根列表并按默认层级预展开。</summary>
    private async Task ReloadAsync()
    {
        _loading = true;
        try
        {
            _nodes.Clear();

            if (_isFiltered)
            {
                // 过滤态：按缓存的可见集合重建过滤视图
                await RebuildFilteredViewAsync();
                return;
            }

            var roots = await FetchRootsAsync();
            RegisterNodes(roots);
            _items = roots.Select(n => ToItem(n, expanded: false)).ToList();

            await ExpandDefaultAsync();
            await LoadSummariesForLoadedAsync();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>根据 RootNodeId 决定根列表：null = 所有根；非 null = 仅该节点（空间子树）。</summary>
    private async Task<List<StringNodeDto>> FetchRootsAsync()
    {
        if (string.IsNullOrEmpty(RootNodeId))
            return await Client.GetRootNodesAsync();

        var root = await Client.GetNodeAsync(RootNodeId);
        return root is null ? new List<StringNodeDto>() : new List<StringNodeDto> { root };
    }

    private void RegisterNodes(IEnumerable<StringNodeDto> nodes)
    {
        foreach (var node in nodes)
            _nodes[node.Id] = node;
    }

    /// <summary>DTO → MudBlazor TreeItemData（Value = 节点 Id；Text/Icon/Expandable/Expanded）。</summary>
    private static TreeItemData<string> ToItem(StringNodeDto node, bool expanded) => new()
    {
        Value = node.Id,
        Text = node.Name,
        Icon = Icons.Material.Outlined.Label,
        Expandable = node.HasChildren,
        Expanded = expanded,
    };

    // ========== 懒加载（MudTreeView.ServerData，官方签名：parent 为 null 时返回根） ==========

    private async Task<IReadOnlyCollection<TreeItemData<string>>> LoadServerData(string? parent)
    {
        // 过滤态不响应懒加载（过滤视图已全量构建）
        if (_isFiltered) return Array.Empty<TreeItemData<string>>();

        var children = string.IsNullOrEmpty(parent)
            ? await FetchRootsAsync()
            : await Client.GetChildrenAsync(parent);

        RegisterNodes(children);
        await LoadSummariesAsync(children.Select(n => n.Id));
        return children.Select(c => ToItem(c, expanded: false)).ToList();
    }

    /// <summary>拉取子级并直接写入 item.Children（预展开路径专用）。</summary>
    private async Task LoadChildrenIntoAsync(TreeItemData<string> item)
    {
        var children = await Client.GetChildrenAsync(item.Value!);
        RegisterNodes(children);
        await LoadSummariesAsync(children.Select(n => n.Id));
        item.Children = children.Select(c => ToItem(c, expanded: false)).ToList();
    }

    // ========== 预展开（默认层级） ==========

    /// <summary>自根向下展开 DefaultExpandLevel 层。</summary>
    private async Task ExpandDefaultAsync()
    {
        if (Options.DefaultExpandLevel <= 0 || _items.Count == 0) return;

        foreach (var root in _items.ToList())
            await ExpandDefaultCoreAsync(root, level: 0);
    }

    private async Task ExpandDefaultCoreAsync(TreeItemData<string> item, int level)
    {
        if (string.IsNullOrEmpty(item.Value)) return;
        if (!item.Expandable) return;
        if (level >= Options.DefaultExpandLevel) return;

        if (item.Children is not { Count: > 0 })
            await LoadChildrenIntoAsync(item);

        item.Expanded = true;

        if (item.Children is not { Count: > 0 }) return;
        foreach (var child in item.Children.OfType<TreeItemData<string>>().ToList())
            await ExpandDefaultCoreAsync(child, level + 1);
    }

    // ========== 摘要 ==========

    /// <summary>批量拉取节点摘要并写入组件级缓存。</summary>
    private async Task LoadSummariesAsync(IEnumerable<string> nodeIds)
    {
        if (!SummaryService.IsEnabled) return;

        var ids = nodeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return;

        var map = await SummaryService.GetSummariesAsync(EntityType, ids);
        foreach (var (id, summary) in map)
            _summaries[id] = summary;

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>为当前已加载的全部节点拉取摘要（对应旧版 LoadSummariesForVisibleAsync）。</summary>
    private async Task LoadSummariesForLoadedAsync()
    {
        if (!SummaryService.IsEnabled || _items.Count == 0) return;

        var ids = WalkItems(_items).Where(i => i.Value is not null).Select(i => i.Value!);
        await LoadSummariesAsync(ids);
    }

    // ========== 过滤 ==========

    /// <summary>
    /// 应用过滤条件：
    ///   1) 走 EAV 查询拿匹配的 EntityId（= NodeId）
    ///   2) 并发查每个匹配节点的祖先链
    ///   3) 合并得 _visibleIds，重建过滤视图
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
            // 高亮文本：取首个非空字符串条件值
            _highlightText = filters
                .Select(f => f.Value as string)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

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
                _nodes.Clear();
                _items = new List<TreeItemData<string>>();
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

            _isFiltered = true;
            _visibleIds = visible;
            _matchedIds = matched;

            // 4) 重建过滤视图
            await RebuildFilteredViewAsync();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// 过滤态视图：一次性加载可见节点（匹配 + 祖先链）并构造层级，全部展开；
    /// Expandable 统一为 false（对齐旧版“过滤态无折叠按钮、全部展开”语义）。
    /// </summary>
    private async Task RebuildFilteredViewAsync()
    {
        _nodes.Clear();

        if (_visibleIds is not { Count: > 0 })
        {
            _items = new List<TreeItemData<string>>();
            return;
        }

        // 并发拉取所有可见节点的 DTO
        var nodeTasks = _visibleIds.Select(id => Client.GetNodeAsync(id));
        var nodes = await Task.WhenAll(nodeTasks);

        var byId = new Dictionary<string, StringNodeDto>();
        foreach (var node in nodes)
            if (node is not null) byId[node.Id] = node;
        RegisterNodes(byId.Values);

        var inserted = new HashSet<string>();

        TreeItemData<string>? InsertSubtree(string nodeId)
        {
            if (!byId.TryGetValue(nodeId, out var node) || !inserted.Add(nodeId))
                return null;

            var childItems = byId.Values
                .Where(c => c.ParentId == nodeId && _visibleIds!.Contains(c.Id))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .Select(c => InsertSubtree(c.Id))
                .Where(i => i is not null)
                .Cast<TreeItemData<string>>()
                .ToList();

            return new TreeItemData<string>
            {
                Value = node.Id,
                Text = node.Name,
                Icon = Icons.Material.Outlined.Label,
                Expanded = true,     // 过滤态全部展开
                Expandable = false,  // 过滤态无折叠语义
                Children = childItems,
            };
        }

        var rootIds = byId.Values
            .Where(n => n.ParentId is null)
            .OrderBy(n => n.SortOrder)
            .ThenBy(n => n.Id)
            .Select(n => n.Id)
            .ToList();

        var roots = new List<TreeItemData<string>>();
        foreach (var rootId in rootIds)
        {
            var item = InsertSubtree(rootId);
            if (item is not null) roots.Add(item);
        }

        _items = roots;
        await LoadSummariesAsync(byId.Keys);
    }

    /// <summary>清除过滤，恢复懒加载树。</summary>
    private async Task ClearFilterAsync()
    {
        _isFiltered = false;
        _visibleIds = null;
        _matchedIds = null;
        _highlightText = string.Empty;

        await ReloadAsync();
    }
}
