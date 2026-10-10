
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky;

/// <summary>
/// 基于 MudBlazor MudTreeView 的字符串树组件（UI 对齐 TreeSky）：
///   - MudTreeView 泛型参数直接使用 string（TreeItemData.Value = 节点 Id），
///     同 MudBlazor 官方 ServerData 示例；
///   - 节点业务元数据由 <see cref="_nodes"/>（Id → StringNodeDto）承载，
///     StringNodeDto 仅存在于服务适配层，不进入 UI 模板；
///   - ServerData 懒加载，@bind-Items / @bind-Expanded 双向绑定。
/// </summary>
public partial class StringTreeSky : ComponentBase
{
    [Parameter] public EventCallback<string> OnNodeSelected { get; set; }
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
    [Inject] private EavApiClient EavApi { get; set; } = default!;
    [Inject] private NodeSchemaCache SchemaCache { get; set; } = default!;

    // 优先级链：ExplicitEntityType > TreeKey > 默认 StringTreeNode
    private string EntityType =>
        !string.IsNullOrWhiteSpace(ExplicitEntityType) ? ExplicitEntityType
        : !string.IsNullOrWhiteSpace(TreeKey) ? $"{StringTreeEntityTypes.Node}:{TreeKey}"
        : StringTreeEntityTypes.Node;

    // ========== 树状态：Value 为节点 Id（string），元数据放 _nodes ==========
    private List<TreeItemData<string>> _items = new();

    /// <summary>已加载节点的业务元数据（Id → DTO），仅服务适配层/操作逻辑使用。</summary>
    private readonly Dictionary<string, StringNodeDto> _nodes = new();

    /// <summary>节点摘要缓存（Id → 摘要文本）。</summary>
    private readonly Dictionary<string, string?> _summaries = new();

    private bool _loading;

    private string? _lastRootNodeId;

    // ============ 过滤态 ============
    private bool _isFiltered;
    private HashSet<string>? _visibleIds;        // 过滤后应显示的节点
    private HashSet<string>? _matchedIds;        // 直接命中的节点（高亮用）
    private string _highlightText = string.Empty; // 高亮文本（首个字符串条件值）

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
            _visibleIds = null;
            _matchedIds = null;
            _isFiltered = false;
            _highlightText = string.Empty;

            await ReloadAsync();
        }
    }

    // ========== 渲染辅助 ==========

    private string? GetSummary(string? nodeId)
        => nodeId is not null && _summaries.TryGetValue(nodeId, out var summary) ? summary : null;

    private bool IsMatch(string? nodeId)
        => nodeId is not null && _isFiltered && _matchedIds?.Contains(nodeId) == true;

    /// <summary>MudHighlighter 高亮文本：仅过滤态生效。</summary>
    private string HighlightedText => _isFiltered ? _highlightText : string.Empty;

    /// <summary>节点点击 → 选中回调（对应 TreeSky.ClickItemText 的选中部分），回传节点 Id。</summary>
    private async Task SelectNodeAsync(string? nodeId)
    {
        if (nodeId is null) return;
        if (OnNodeSelected.HasDelegate) await OnNodeSelected.InvokeAsync(nodeId);
    }

    // ========== 树遍历/查找（显式栈 DFS） ==========

    internal static IEnumerable<TreeItemData<string>> WalkItems(
        IEnumerable<TreeItemData<string>> items)
    {
        var stack = new Stack<TreeItemData<string>>(items.Reverse());
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children.OfType<TreeItemData<string>>())
                stack.Push(child);
        }
    }

    private TreeItemData<string>? FindItem(string? id)
    {
        if (string.IsNullOrEmpty(id) || _items.Count == 0) return null;
        return WalkItems(_items).FirstOrDefault(i => i.Value == id);
    }

    private StringNodeDto RequireNode(string id)
        => _nodes.TryGetValue(id, out var node)
            ? node
            : throw new InvalidOperationException($"节点元数据不存在：{id}");
}
