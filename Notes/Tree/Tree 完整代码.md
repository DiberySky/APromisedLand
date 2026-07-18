以下是完整的最终代码，涵盖**后端服务（EF Core + PostgreSQL）**、**前端页面（MudBlazor）**以及**辅助工具**。所有文件均已适配您的最新需求（泛型、`IReadOnlyList`、递归CTE优化）。

---

## 文件结构概览

```
APromisedLand.Shared.DiberyTree/
├── Interfaces/
│   └── ITreeNode.cs
├── Models/
│   ├── TreeNodeDto.cs
│   ├── CategoryTree.cs
│   └── TreeQueryParams.cs
├── DiberyTreeHelper.cs

APromisedLand.Api.Projects.DiberyTree/
├── Services/
│   ├── ITreeService.cs          (接口)
│   └── CategoryTreeService.cs   (EF + PostgreSQL实现)

APromisedLand.Api.Data/
└── AppDbContext.cs

APromisedLand.Razor.DiberyTree/
└── TreePage.razor
```

---

## 1. `ITreeNode` 接口

```csharp
// APromisedLand.Shared.DiberyTree.Interfaces/ITreeNode.cs
namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeNode
{
    string Id { get; }
    string? ParentId { get; }
    string Text();  // 或 Name 属性，根据实现而定
}
```

---

## 2. `TreeNodeDto<T>`（泛型树节点传输对象）

```csharp
// APromisedLand.Shared.DiberyTree.Models/TreeNodeDto.cs
namespace APromisedLand.Shared.DiberyTree.Models;

public class TreeNodeDto<T>
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public T? Value { get; set; }
    public string? Text { get; set; }
    public string? Icon { get; set; }
    public bool HasChildren { get; set; }
    public bool Expanded { get; set; }
    public bool Selected { get; set; }
    public List<TreeNodeDto<T>>? Children { get; set; }
    public Dictionary<string, object>? ExtraData { get; set; }
}
```

---

## 3. `TreeQueryParams`（查询参数）

```csharp
// APromisedLand.Shared.DiberyTree.Models/TreeQueryParams.cs
namespace APromisedLand.Shared.DiberyTree.Models;

public class TreeQueryParams
{
    public string? ParentId { get; set; }      // null = 根节点，空字符串=忽略
    public string? SearchTerm { get; set; }
    public bool OnlyWithChildren { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

---

## 4. `CategoryTree`（EF 实体）

```csharp
// APromisedLand.Shared.DiberyTree.Models/CategoryTree.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

[Table("CategoryTrees")]
public class CategoryTree : ITreeNode
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(300)]
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(36)]
    public string? ParentId { get; set; }

    [JsonIgnore]
    public bool HasChildren { get; set; }

    // 导航属性（可选）
    [ForeignKey(nameof(ParentId))]
    [JsonIgnore]
    public virtual CategoryTree? Parent { get; set; }

    [JsonIgnore]
    public virtual ICollection<CategoryTree>? Children { get; set; }

    public string Text() => Name;
}
```

---

## 5. `DiberyTreeHelper`（扩展方法）

```csharp
// APromisedLand.Shared.DiberyTree/DiberyTreeHelper.cs
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Shared.DiberyTree;

public static class DiberyTreeHelper
{
    public static TreeNodeDto<T> ToNodeDto<T>(
        this T nodeValue,
        string? icon = null,
        bool hasChildren = false) where T : class, ITreeNode
    {
        return new TreeNodeDto<T>
        {
            Id = nodeValue.Id,
            Text = nodeValue.Text(),
            ParentId = nodeValue.ParentId,
            Value = nodeValue,
            Icon = icon,
            HasChildren = hasChildren
        };
    }
}
```

---

## 6. `ITreeService<T>` 接口（已调整为 `IReadOnlyList`）

```csharp
// APromisedLand.Api.Projects.DiberyTree.Services/ITreeService.cs
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeService<T>
{
    Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default);
    Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);
    Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default);
}
```

---

## 7. `AppDbContext`（EF Core 上下文）

```csharp
// APromisedLand.Api.Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CategoryTree> CategoryTrees { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoryTree>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentId);
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict); // 防止意外级联删除
        });

        base.OnModelCreating(modelBuilder);
    }
}
```

---

## 8. `CategoryTreeService`（使用 PostgreSQL 递归 CTE 优化）

```csharp
// APromisedLand.Api.Projects.DiberyTree.Services/CategoryTreeService.cs
using Microsoft.EntityFrameworkCore;
using Npgsql;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Api.Data;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

public class CategoryTreeService : ITreeService<CategoryTree>
{
    private readonly AppDbContext _dbContext;

    public CategoryTreeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var roots = await _dbContext.CategoryTrees
            .Where(c => c.ParentId == null)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);
        return roots;
    }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        var children = await _dbContext.CategoryTrees
            .Where(c => c.ParentId == parentId)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);
        return children;
    }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CategoryTrees.AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.ParentId))
            query = query.Where(c => c.ParentId == queryParams.ParentId);
        else if (queryParams.ParentId == null)
            query = query.Where(c => c.ParentId == null);

        if (!string.IsNullOrEmpty(queryParams.SearchTerm))
            query = query.Where(c => c.Name.Contains(queryParams.SearchTerm));

        if (queryParams.OnlyWithChildren)
            query = query.Where(c => c.HasChildren);

        var result = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);
        return result;
    }

    public async Task<TreeNodeDto<CategoryTree>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default)
    {
        // 使用 PostgreSQL CTE 一次性获取完整树
        var sql = @"
            WITH RECURSIVE tree_nodes AS (
                SELECT id, name, ""ParentId"", ""HasChildren""
                FROM ""CategoryTrees""
                WHERE id = COALESCE(@rootId, (SELECT id FROM ""CategoryTrees"" WHERE ""ParentId"" IS NULL LIMIT 1))
                UNION ALL
                SELECT c.id, c.name, c.""ParentId"", c.""HasChildren""
                FROM ""CategoryTrees"" c
                INNER JOIN tree_nodes tn ON c.""ParentId"" = tn.id
            )
            SELECT id, name, ""ParentId"", ""HasChildren"" FROM tree_nodes;";

        var param = new NpgsqlParameter("@rootId", rootId ?? (object)DBNull.Value);
        var nodes = await _dbContext.Database
            .SqlQueryRaw<CategoryTree>(sql, param)
            .ToListAsync(cancellationToken);

        if (nodes.Count == 0)
            return null;

        // 构建树（内存组装）
        var dtoDict = nodes.ToDictionary(n => n.Id, n => n.ToNodeDto());
        TreeNodeDto<CategoryTree>? rootDto = null;
        foreach (var node in nodes)
        {
            var dto = dtoDict[node.Id];
            if (string.IsNullOrEmpty(node.ParentId))
            {
                rootDto = dto;
            }
            else if (dtoDict.TryGetValue(node.ParentId, out var parentDto))
            {
                parentDto.Children ??= new List<TreeNodeDto<CategoryTree>>();
                parentDto.Children.Add(dto);
                parentDto.HasChildren = true;
            }
        }
        return rootDto;
    }

    public async Task<TreeNodeDto<CategoryTree>> CreateNodeAsync(TreeNodeDto<CategoryTree> nodeDto, CancellationToken cancellationToken = default)
    {
        var entity = new CategoryTree
        {
            Id = nodeDto.Id ?? Guid.NewGuid().ToString(),
            Name = nodeDto.Text ?? string.Empty,
            ParentId = nodeDto.ParentId,
            HasChildren = false
        };

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await _dbContext.CategoryTrees.FindAsync(new object[] { entity.ParentId }, cancellationToken);
            if (parent != null && !parent.HasChildren)
            {
                parent.HasChildren = true;
                _dbContext.Update(parent);
            }
        }

        await _dbContext.CategoryTrees.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
    }

    public async Task<TreeNodeDto<CategoryTree>> UpdateNodeAsync(TreeNodeDto<CategoryTree> nodeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CategoryTrees.FindAsync(new object[] { nodeDto.Id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        entity.Name = nodeDto.Text ?? entity.Name;
        // 不在此修改 ParentId，请使用 MoveNodeAsync
        _dbContext.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // 使用 CTE 递归删除整个子树
        var sql = @"
            WITH RECURSIVE to_delete AS (
                SELECT id FROM ""CategoryTrees"" WHERE id = @nodeId
                UNION ALL
                SELECT c.id FROM ""CategoryTrees"" c
                INNER JOIN to_delete td ON c.""ParentId"" = td.id
            )
            DELETE FROM ""CategoryTrees"" WHERE id IN (SELECT id FROM to_delete);";

        var param = new NpgsqlParameter("@nodeId", nodeId);
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(sql, param, cancellationToken);
        if (affected == 0)
            return false;

        // 更新父节点的 HasChildren（如果存在）
        var parentId = await _dbContext.CategoryTrees
            .Where(c => c.Id == nodeId)
            .Select(c => c.ParentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrEmpty(parentId))
        {
            var hasChildren = await _dbContext.CategoryTrees
                .AnyAsync(c => c.ParentId == parentId, cancellationToken);
            if (!hasChildren)
            {
                var parent = await _dbContext.CategoryTrees.FindAsync(new object[] { parentId }, cancellationToken);
                if (parent != null)
                {
                    parent.HasChildren = false;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
        return true;
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default)
    {
        var node = await _dbContext.CategoryTrees.FindAsync(new object[] { nodeId }, cancellationToken);
        if (node == null)
            return false;

        // 循环检测
        if (await IsDescendantAsync(nodeId, newParentId, cancellationToken))
            return false;

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        _dbContext.Update(node);

        // 更新旧父节点 HasChildren
        if (!string.IsNullOrEmpty(oldParentId))
        {
            var oldParentChildrenCount = await _dbContext.CategoryTrees
                .Where(c => c.ParentId == oldParentId)
                .CountAsync(cancellationToken);
            if (oldParentChildrenCount == 0)
            {
                var oldParent = await _dbContext.CategoryTrees.FindAsync(new object[] { oldParentId }, cancellationToken);
                if (oldParent != null)
                {
                    oldParent.HasChildren = false;
                    _dbContext.Update(oldParent);
                }
            }
        }

        // 更新新父节点 HasChildren
        if (!string.IsNullOrEmpty(newParentId))
        {
            var newParent = await _dbContext.CategoryTrees.FindAsync(new object[] { newParentId }, cancellationToken);
            if (newParent != null)
            {
                newParent.HasChildren = true;
                _dbContext.Update(newParent);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> IsDescendantAsync(string ancestorId, string? nodeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(nodeId) || ancestorId == nodeId)
            return false;

        var sql = @"
            WITH RECURSIVE descendants AS (
                SELECT id FROM ""CategoryTrees"" WHERE id = @ancestorId
                UNION ALL
                SELECT c.id FROM ""CategoryTrees"" c
                INNER JOIN descendants d ON c.""ParentId"" = d.id
            )
            SELECT EXISTS (SELECT 1 FROM descendants WHERE id = @nodeId);";

        var param1 = new NpgsqlParameter("@ancestorId", ancestorId);
        var param2 = new NpgsqlParameter("@nodeId", nodeId);
        var exists = await _dbContext.Database
            .SqlQueryRaw<bool>(sql, param1, param2)
            .FirstOrDefaultAsync(cancellationToken);
        return exists;
    }
}
```

---

## 9. 依赖注入注册（在 `Program.cs` 或 `Startup.cs`）

```csharp
// 配置 PostgreSQL 连接
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 注册树服务（使用 EF 实现）
builder.Services.AddScoped<ITreeService<CategoryTree>, CategoryTreeService>();
```

---

## 10. `TreePage.razor`（前端页面，MudBlazor 7+）

```razor
@page "/tree"
@using APromisedLand.Shared.DiberyTree.Models
@using MudBlazor
@inject ITreeService<CategoryTree> TreeService
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4" Class="mb-4">🌳 树管理</MudText>

<MudStack Row="true" Spacing="2" Class="mb-4">
    <MudButton Variant="Variant.Filled" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add" OnClick="@AddRootNode">
        添加根节点
    </MudButton>
    <MudButton Variant="Variant.Filled" Color="Color.Secondary" StartIcon="@Icons.Material.Filled.Refresh" OnClick="@LoadRootNodes">
        刷新
    </MudButton>
</MudStack>

<MudTreeView Items="@_rootItems"
             TValue="TreeNodeDto<CategoryTree>"
             Hover="true"
             Dense="true"
             CanSelect="true"
             @bind-SelectedValue="_selectedNode"
             ItemTemplate="@ItemTemplate">
</MudTreeView>

@code {
    private IReadOnlyCollection<ITreeItemData<TreeNodeDto<CategoryTree>>> _rootItems =
        Array.Empty<ITreeItemData<TreeNodeDto<CategoryTree>>>();
    private TreeNodeDto<CategoryTree>? _selectedNode;

    protected override async Task OnInitializedAsync()
    {
        await LoadRootNodes();
    }

    private async Task LoadRootNodes()
    {
        var roots = await TreeService.GetRootNodesAsync();
        _rootItems = roots.Select(root => BuildTreeItem(root)).ToList();
        StateHasChanged();
    }

    private ITreeItemData<TreeNodeDto<CategoryTree>> BuildTreeItem(TreeNodeDto<CategoryTree> node)
    {
        return new TreeItemData<TreeNodeDto<CategoryTree>>
        {
            Value = node,
            GetChildren = async () =>
            {
                var children = await TreeService.GetChildrenAsync(node.Id);
                return children.Select(child => BuildTreeItem(child)).ToList();
            }
        };
    }

    private async Task AddRootNode()
    {
        var dialog = await DialogService.ShowAsync<AddNodeDialog>("添加根节点",
            new DialogParameters { ["Title"] = "新建根节点" });
        var result = await dialog.Result;
        if (!result.Cancelled && result.Data is string text && !string.IsNullOrWhiteSpace(text))
        {
            var newNode = new TreeNodeDto<CategoryTree>
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                ParentId = null,
                HasChildren = false,
                Icon = "folder"
            };
            await TreeService.CreateNodeAsync(newNode);
            await LoadRootNodes();
            Snackbar.Add($"根节点“{text}”已添加", Severity.Success);
        }
    }

    private async Task AddChildNode(TreeNodeDto<CategoryTree> parent)
    {
        var dialog = await DialogService.ShowAsync<AddNodeDialog>("添加子节点",
            new DialogParameters { ["Title"] = $"添加子节点到“{parent.Text}”" });
        var result = await dialog.Result;
        if (!result.Cancelled && result.Data is string text && !string.IsNullOrWhiteSpace(text))
        {
            var newNode = new TreeNodeDto<CategoryTree>
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                ParentId = parent.Id,
                HasChildren = false,
                Icon = "insert_drive_file"
            };
            await TreeService.CreateNodeAsync(newNode);
            await LoadRootNodes();
            Snackbar.Add($"子节点“{text}”已添加", Severity.Success);
        }
    }

    private async Task DeleteNode(TreeNodeDto<CategoryTree> node)
    {
        bool? confirm = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定要删除节点“{node.Text}”及其所有子节点吗？",
            "删除",
            "取消");
        if (confirm == true)
        {
            await TreeService.DeleteNodeAsync(node.Id);
            await LoadRootNodes();
            Snackbar.Add($"节点“{node.Text}”已删除", Severity.Success);
        }
    }

    private RenderFragment<ITreeItemData<TreeNodeDto<CategoryTree>>> ItemTemplate => (item) => @<div style="display: flex; align-items: center; width: 100%; gap: 8px;">
        <span style="flex: 1;">
            @if (!string.IsNullOrEmpty(item.Value.Icon))
            {
                <MudIcon Icon="@item.Value.Icon" Size="Size.Small" Class="mr-2" />
            }
            @item.Value.Text
        </span>
        <div style="display: flex; gap: 4px;">
            <MudIconButton Icon="@Icons.Material.Filled.Add" Size="Size.Small" Color="Color.Success" OnClick="@(() => AddChildNode(item.Value))" />
            <MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" Color="Color.Error" OnClick="@(() => DeleteNode(item.Value))" />
        </div>
    </div>;
}
```

---

## 11. `AddNodeDialog.razor`（对话框组件）

```razor
@using MudBlazor
@inject MudBlazor.IDialogService DialogService

<MudDialog>
    <TitleContent>@Title</TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="Text" Label="节点名称" For="() => Text" Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="Submit">确定</MudButton>
        <MudButton Variant="Variant.Outlined" Color="Color.Secondary" OnClick="Cancel">取消</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] MudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "添加节点";
    [Parameter] public string Text { get; set; } = string.Empty;

    private void Submit()
    {
        MudDialog.Close(Text);
    }

    private void Cancel() => MudDialog.Cancel();
}
```

---

## 12. 数据库连接字符串（`appsettings.json`）

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=TreeDb;Username=postgres;Password=yourpassword"
  }
}
```

---

## 13. 初始种子数据（可选）

在 `AppDbContext` 的 `OnModelCreating` 或通过迁移添加种子数据：

```csharp
modelBuilder.Entity<CategoryTree>().HasData(
    new CategoryTree { Id = "root1", Name = "根节点1", ParentId = null, HasChildren = false },
    new CategoryTree { Id = "child1", Name = "子节点1-1", ParentId = "root1", HasChildren = false }
);
```

---

## ✅ 总结

- **后端**：使用 PostgreSQL 递归 CTE，所有树操作高效（尤其是删除、移动、完整加载）。
- **前端**：MudBlazor 7+ 的 `TreeView` 懒加载，界面友好。
- **数据层**：EF Core 6/7/8 均可，使用 Npgsql 提供程序。
- **扩展性**：泛型设计支持任何实现 `ITreeNode` 的实体。

所有代码均已完整提供，可直接复制使用。如有任何问题，欢迎继续交流！