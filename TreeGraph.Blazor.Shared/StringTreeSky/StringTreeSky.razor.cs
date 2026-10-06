using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TreeGraph.StringTree.Contracts;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;

namespace TreeGraph.Blazor.Shared.StringTreeSky;

public partial class StringTreeSky : ComponentBase
{
    [Parameter] public EventCallback<StringNodeDto> OnNodeSelected { get; set; }
    [Parameter] public EventCallback OnTreeChanged { get; set; }

    private sealed class Row
    {
        public StringNodeDto Node { get; init; } = default!;
        public int Level { get; init; }
        public bool Expanded { get; set; }
        public bool Loaded { get; set; }
    }

    private readonly List<Row> _all = new();
    private List<Row> _visible = new();
    private readonly HashSet<int> _expandedIds = new();
    private bool _loading;

    private int? _editingId;
    private string _editingName = string.Empty;

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        _loading = true;
        _all.Clear();

        var roots = await Client.GetRootNodesAsync();
        foreach (var root in roots)
        {
            _all.Add(new Row
            {
                Node = root,
                Level = 0,
                Expanded = _expandedIds.Contains(root.Id)
            });
        }

        // 展开默认层级
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
        foreach (var child in children)
        {
            _all.Insert(insertAt++, new Row
            {
                Node = child,
                Level = parent.Level + 1,
                Expanded = _expandedIds.Contains(child.Id)
            });
        }
        parent.Loaded = true;
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
        _visible = _all
            .Where(r => IsVisible(r))
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Node.SortOrder)
            .ThenBy(r => r.Node.Id)
            .ToList();
    }

    private bool IsVisible(Row row)
    {
        var current = row.Node;
        while (current.ParentId.HasValue)
        {
            if (!_expandedIds.Contains(current.ParentId.Value)) return false;
            var parentRow = _all.FirstOrDefault(r => r.Node.Id == current.ParentId.Value);
            if (parentRow == null) return false;
            current = parentRow.Node;
        }
        return true;
    }

    private void RemoveDescendants(int parentId)
    {
        var stack = new Stack<int>();
        stack.Push(parentId);
        var toRemove = new List<int>();
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

    private async Task CreateRootAsync()
    {
        var dto = new StringNodeDto { Name = "新建节点", ParentId = null };
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
        await NotifyChangedAsync();
    }

    private async Task NotifyChangedAsync()
    {
        if (OnTreeChanged.HasDelegate) await OnTreeChanged.InvokeAsync();
    }
}
