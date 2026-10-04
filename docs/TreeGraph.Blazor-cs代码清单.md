# TreeGraph.Blazor C# 代码清单

- 生成时间：2026-10-04 21:37:23
- 文件总数：13
- 排除：bin/、obj/、csproj、README.md
- 项目状态：含 TreeSkyDemo 与 StringTreeDemo 演示页；Services/DemoTree 为 StringTree 内存示例（Store/DataSource/Handler）；DI：AddTreeSky + AddStringTreeSky

## 文件 1/13 TreeGraph.Blazor/Program.cs

```csharp
using Microsoft.Extensions.Http.Resilience;
using MudBlazor.Services;
using Polly;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Services;
using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.Blazor.Shared.Trees.Extensions;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ★ TreeSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
builder.Services.AddTreeSky();

// ★ TreeSky 演示：StringTreeNode（string 名称节点），数据存于 TreeGraph.Api 的
//   string_tree_nodes 表（Postgres）。读写都经 DiberyTreeApiClient<StringTreeNode>
//   → /StringTreeNode/* 端点（TreeControllerBase<StringTreeNode> + EfTreeService）。
builder.Services.AddScoped<ITreeClientService<StringTreeNode>, StringTreeClientService>();
builder.Services.AddHttpClient("TreeSky", client =>
    {
        // Aspire 服务发现：与 EavApiClient 同一后端
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    // 写操作（POST/PUT/DELETE/move）不幂等，禁止自动重试
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
    });

// ★ StringTreeSky（T=string 树）：Noop Handler 用 TryAdd 注册，
//   必须在宿主自定义 Handler 之前，下面的 AddScoped 才能覆盖默认值。
builder.Services.AddStringTreeSky();

// ★ StringTreeSky 内存演示：单例 store 与接口实现共享同一份内存数据
//   （与上方 API 版 StringTreeClientService 并存，服务不同接口，互不影响）。
builder.Services.AddSingleton<InMemoryStringTreeStore>();
builder.Services.AddScoped<IStringTreeDataSource, InMemoryStringTreeDataSource>();
builder.Services.AddScoped<IStringTreeActionHandler, InMemoryStringTreeActionHandler>();

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi
// 弹性策略显式配置：
//   - 关闭自动重试：元数据 PUT/POST 不幂等，自动重试会引发数据损坏
//     （例如 recalculate-factor 被重放 → 值被平方调整）
//   - 放宽超时：单次重算可能耗时几秒
//   - 保留熔断器默认参数（保护后端）
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(options =>
    {
        // ★ 禁止重试的正确写法：
        //   MaxRetryAttempts 校验约束为 1–int.MaxValue（不接受 0），
        //   因此置 1 通过校验，再用 ShouldHandle 恒 false 让重试永不触发。
        //   管理台 PUT/POST 不幂等（如 recalculate-factor 重放会导致数据损坏）。
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);

        // ★ 熔断器采样窗口必须 ≥ 2 × AttemptTimeout（30s → 至少 60s）
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);

        // 超时设置
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

// ★ 供 WebApplicationFactory<Program> 引用（启动级 smoke 测试需要）
public partial class Program { }
```

## 文件 2/13 TreeGraph.Blazor/Components/FieldRenderers/NumericValueDto.cs

```csharp
namespace TreeGraph.Blazor.Components.FieldRenderers;

/// <summary>
/// 带单位的数值（DynamicForm 内部表单值）。
/// 对应后端 JSON 形状 { value, unitId }，提交时由 DynamicForm 序列化。
/// </summary>
public class NumericValueDto
{
    public decimal? Value { get; set; }
    public Guid? UnitId { get; set; }
}
```

## 文件 3/13 TreeGraph.Blazor/Services/DemoTree/InMemoryStringTreeActionHandler.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

public class InMemoryStringTreeActionHandler(InMemoryStringTreeStore store)
    : IStringTreeActionHandler
{
    public Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default)
        => Task.FromResult(store.Create(parentId, newChild));

    public Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default)
        => Task.FromResult(store.Update(node) ? node : null);

    public Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default)
        => Task.FromResult(store.Delete(id));

    public Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default)
        => Task.FromResult(store.Move(id, newParentId));

    public Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default)
        => Task.FromResult(store.Sort(parentId, orderedChildIds));
}
```

## 文件 4/13 TreeGraph.Blazor/Services/DemoTree/InMemoryStringTreeDataSource.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

public class InMemoryStringTreeDataSource(InMemoryStringTreeStore store)
    : IStringTreeDataSource
{
    public Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(CancellationToken ct = default)
        => Task.FromResult(store.GetRoots());

    public Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
        string parentId, CancellationToken ct = default)
        => Task.FromResult(store.GetChildren(parentId));

    public Task<StringNodeMeta?> GetByIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(store.GetById(id));

    public Task<List<string>?> GetAncestorPathAsync(
        string id, CancellationToken ct = default)
        => Task.FromResult(store.GetAncestorPath(id));
}
```

## 文件 5/13 TreeGraph.Blazor/Services/DemoTree/InMemoryStringTreeStore.cs

```csharp
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 内存树共享存储：DataSource 与 ActionHandler 共用同一份数据。
/// 演示用途，非线程安全（UI 单线程访问）。
/// </summary>
public class InMemoryStringTreeStore
{
    private readonly Dictionary<string, StringNodeMeta> _nodes = new();
    private readonly object _lock = new();

    public InMemoryStringTreeStore()
    {
        Seed();
    }

    // ============================================================
    // 种子数据（3 层 8 节点）
    // ============================================================

    private void Seed()
    {
        AddRoot("电子产品", Icons.Material.Filled.Folder, order: 10, hasChildren: true);
        AddRoot("服装", Icons.Material.Filled.Checkroom, order: 20, hasChildren: true);

        var electronics = FindByName("电子产品")!;
        AddChild(electronics.Id, "手机", Icons.Material.Filled.Smartphone, order: 10, canHaveChildren: true);
        AddChild(electronics.Id, "电脑", Icons.Material.Filled.Laptop, order: 20, canHaveChildren: true);

        var phones = FindByName("手机")!;
        AddChild(phones.Id, "安卓手机", Icons.Material.Filled.PhoneAndroid, order: 10, canHaveChildren: false);
        AddChild(phones.Id, "iPhone", Icons.Material.Filled.PhoneIphone, order: 20, canHaveChildren: false);

        var computers = FindByName("电脑")!;
        AddChild(computers.Id, "笔记本", Icons.Material.Filled.LaptopMac, order: 10, canHaveChildren: false);

        var clothing = FindByName("服装")!;
        AddChild(clothing.Id, "男装", Icons.Material.Filled.Man, order: 10, canHaveChildren: false);
    }

    // ============================================================
    // 查询
    // ============================================================

    public IReadOnlyList<StringNodeMeta> GetRoots() => Snapshot()
        .Where(n => n.ParentId is null)
        .OrderBy(n => n.SortOrder).ThenBy(n => n.Text)
        .ToList();

    public IReadOnlyList<StringNodeMeta> GetChildren(string parentId) => Snapshot()
        .Where(n => n.ParentId == parentId)
        .OrderBy(n => n.SortOrder).ThenBy(n => n.Text)
        .ToList();

    public StringNodeMeta? GetById(string id)
        => _nodes.GetValueOrDefault(id);

    public List<string>? GetAncestorPath(string id)
    {
        var path = new List<string>();
        var cursor = _nodes.GetValueOrDefault(id);
        while (cursor is not null)
        {
            path.Insert(0, cursor.Id);
            cursor = cursor.ParentId is null
                ? null
                : _nodes.GetValueOrDefault(cursor.ParentId);
        }
        return path.Count > 0 ? path : null;
    }

    // ============================================================
    // 写入
    // ============================================================

    public StringNodeMeta? Create(string parentId, StringNodeMeta template)
    {
        var parent = _nodes.GetValueOrDefault(parentId);
        if (parent is null || !parent.CanHaveChildren) return null;

        var created = template.Clone();
        created.Id = Guid.NewGuid().ToString("D");
        created.ParentId = parentId;
        created.SortOrder = NextSortOrder(parentId);
        _nodes[created.Id] = created;

        parent.HasChildren = true;
        return created;
    }

    public bool Update(StringNodeMeta node)
    {
        var existing = _nodes.GetValueOrDefault(node.Id);
        if (existing is null) return false;

        existing.Text = node.Text;
        existing.Description = node.Description;
        existing.Subtitle = node.Subtitle;
        existing.Icon = node.Icon;
        existing.CanHaveChildren = node.CanHaveChildren;
        existing.ExtraData = node.ExtraData;
        return true;
    }

    public bool Delete(string id)
    {
        var node = _nodes.GetValueOrDefault(id);
        if (node is null) return false;

        // 级联删后代
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var child in _nodes.Values.Where(n => n.ParentId == cur).ToList())
                queue.Enqueue(child.Id);
            _nodes.Remove(cur);
        }

        // 更新父的 HasChildren
        if (node.ParentId is { } pid && _nodes.GetValueOrDefault(pid) is { } parent)
            parent.HasChildren = _nodes.Values.Any(n => n.ParentId == pid);

        return true;
    }

    public bool Move(string id, string? newParentId)
    {
        var node = _nodes.GetValueOrDefault(id);
        if (node is null) return false;

        // 防环：newParent 不能是自己或后代
        var cursor = newParentId;
        while (cursor is not null)
        {
            if (cursor == id) return false;
            cursor = _nodes.GetValueOrDefault(cursor)?.ParentId;
        }

        if (newParentId is not null)
        {
            var np = _nodes.GetValueOrDefault(newParentId);
            if (np is null || !np.CanHaveChildren) return false;
        }

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        node.SortOrder = NextSortOrder(newParentId);

        // 更新新旧父的 HasChildren
        if (oldParentId is { } op && _nodes.GetValueOrDefault(op) is { } opNode)
            opNode.HasChildren = _nodes.Values.Any(n => n.ParentId == op);
        if (newParentId is { } np2 && _nodes.GetValueOrDefault(np2) is { } npNode)
            npNode.HasChildren = true;

        return true;
    }

    public bool Sort(string parentId, IReadOnlyList<string> orderedIds)
    {
        // 按列表位置重编号（★ 不按 SortOrder 排序，避免撤销拖拽结果）
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var node = _nodes.GetValueOrDefault(orderedIds[i]);
            if (node is null || node.ParentId != parentId) continue;
            node.SortOrder = (i + 1) * 10;
        }
        return true;
    }

    // ============================================================
    // 辅助
    // ============================================================

    private List<StringNodeMeta> Snapshot()
    {
        lock (_lock) return _nodes.Values.ToList();
    }

    private StringNodeMeta? FindByName(string text)
        => _nodes.Values.FirstOrDefault(n => n.Text == text);

    private int NextSortOrder(string? parentId)
    {
        var siblings = _nodes.Values.Where(n => n.ParentId == parentId).ToList();
        return siblings.Count == 0 ? 10 : siblings.Max(n => n.SortOrder) + 10;
    }

    // 种子构造辅助
    private void AddRoot(string text, string? icon, int order, bool hasChildren)
    {
        var id = Guid.NewGuid().ToString("D");
        _nodes[id] = new StringNodeMeta
        {
            Id = id,
            ParentId = null,
            Text = text,
            Icon = icon,
            HasChildren = hasChildren,
            CanHaveChildren = true,
            SortOrder = order,
        };
    }

    private void AddChild(string parentId, string text, string? icon,
        int order, bool canHaveChildren)
    {
        var id = Guid.NewGuid().ToString("D");
        _nodes[id] = new StringNodeMeta
        {
            Id = id,
            ParentId = parentId,
            Text = text,
            Icon = icon,
            HasChildren = false,
            CanHaveChildren = canHaveChildren,
            SortOrder = order,
        };

        // ★ 与 Create() 语义对齐：子节点存在 ⇒ 父节点 HasChildren=true，
        //   否则父节点 Expandable=false，UI 上永远没有展开箭头
        var parent = _nodes.GetValueOrDefault(parentId);
        if (parent is not null) parent.HasChildren = true;
    }
}
```

## 文件 6/13 TreeGraph.Blazor/Services/DemoTree/StringTreeClientService.cs

```csharp
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 字符串节点的 <see cref="ITreeClientService{TTree}"/> 实现：
/// 通过 <see cref="DiberyTreeApiClient{T}"/> 访问 TreeGraph.Api 的 StringTreeNodeController。
/// </summary>
public class StringTreeClientService(DiberyTreeApiClient<StringTreeNode> api) : ITreeClientService<StringTreeNode>
{
    public string Title { get; set; } = "字符串节点演示树";

    /// <summary>关闭"新页面打开"：演示中点击目录不触发 forceLoad 整页跳转。</summary>
    public bool NewPageShow { get; set; }

    /// <summary>移动/选择对话框中只能选中叶子（与源计量单位树语义一致）。</summary>
    public bool SelectLeaf { get; set; } = true;

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default)
        => await api.GetRootNodesAsync(rootId, ct);

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadChildrenAsync(
        StringTreeNode? parent = null, CancellationToken ct = default)
        => parent == null
            ? await api.GetRootNodesAsync(null, ct)
            : await api.GetChildrenAsync(parent.Id, ct);

    public async Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default)
        => [.. await api.GetAncestorPathAsync(nodeId, ct)];
}
```

## 文件 7/13 TreeGraph.Blazor/Services/EavApiClient.cs

```csharp
using System.Text.Json;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// TreeGraphEavApi 客户端。
///
/// ★ 所有实体 ID / 元数据 ID 均为 GUID 字符串（36 字符）。
/// </summary>
public class EavApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<EavApiClient> _logger;

    public EavApiClient(HttpClient http, ILogger<EavApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ============================================================
    // EavController
    // ============================================================

    public async Task<IReadOnlyList<AttributeSchemaDto>?> GetSchemaAsync(
        string entityType, CancellationToken ct = default)
    {
        var resp = await GetAsync<SchemaResponse>($"api/eav/{entityType}/schema", ct);
        return resp?.Attributes;
    }

    public async Task<DynamicEntityDto?> GetEntityAsync(
        string entityType, string entityId,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/eav/{entityType}/entities/{entityId}";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<DynamicEntityDto>(url, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        SaveEntityAsync(
        string entityType, string entityId,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/eav/{entityType}/entities/{entityId}";
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } expected)
            {
                req.Headers.Add("X-Expected-Updated-At",
                    expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PUT {Url} → 409 冲突：{Body}", url, text);

                DateTimeOffset? currentUpdatedAt = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(text, JsonOptions);
                    currentUpdatedAt = conflict?.CurrentUpdatedAt;
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "解析 409 响应体失败");
                }

                return (false,
                    "并发冲突：实体已被其他用户修改，请刷新后重试",
                    currentUpdatedAt);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}", url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT entities 失败");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchEntityAsync(
        string entityType, string entityId,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/eav/{entityType}/entities/{entityId}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } expected)
            {
                req.Headers.Add("X-Expected-Updated-At",
                    expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PATCH {Url} → 409：{Body}", url, text);

                DateTimeOffset? currentUpdatedAt = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(text, JsonOptions);
                    currentUpdatedAt = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false,
                    "并发冲突：实体已被其他用户修改，请刷新后重试",
                    currentUpdatedAt);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PATCH {Url} → {Status}: {Error}", url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH entities 失败");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteEntityAsync(
        string entityType, string entityId, CancellationToken ct = default)
        => await DeleteWithErrorAsync(
            $"api/eav/{entityType}/entities/{entityId}", ct);

    public async Task<BatchDeleteResultDto?> BatchDeleteEntitiesAsync(
        string entityType,
        IReadOnlyList<string> entityIds,
        CancellationToken ct = default)
    {
        var body = new BatchDeleteRequest { EntityIds = entityIds.ToList() };
        return await PostAsync<BatchDeleteResultDto>(
            $"api/eav/{entityType}/entities/batch-delete", body, ct);
    }

    public async Task<PagedResult<DynamicEntityDto>?> QueryAsync(
        string entityType, EavQueryRequest request, CancellationToken ct = default)
        => await PostAsync<PagedResult<DynamicEntityDto>>(
            $"api/eav/{entityType}/entities/query", request, ct);

    public async Task<IReadOnlyList<string>?> QueryByTableAsync(
        string entityType, QueryByTableRequest request, CancellationToken ct = default)
    {
        var resp = await PostAsync<QueryByTableResponse>(
            $"api/eav/{entityType}/entities/query-by-table", request, ct);
        return resp?.EntityIds;
    }

    public async Task<IReadOnlyList<EntityHistoryDto>?> GetHistoryAsync(
        string entityType, string entityId,
        DateTimeOffset? from = null, CancellationToken ct = default)
    {
        var url = $"api/eav/{entityType}/entities/{entityId}/history";
        if (from.HasValue)
            url += $"?from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        return await GetAsync<IReadOnlyList<EntityHistoryDto>>(url, ct);
    }

    // ============================================================
    // CustomTableDataController
    // ============================================================

    public async Task<CustomTableValue?> LoadCustomTableAsync(
        string entityType, string entityId, string tableName, CancellationToken ct = default)
        => await GetAsync<CustomTableValue>(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", ct);

    public async Task<(bool Ok, string? Error)> ReplaceCustomTableAsync(
        string entityType, string entityId, string tableName,
        CustomTableValue value, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", value, ct);

    public async Task<(bool Ok, string? Error)> UpsertCustomTableRowAsync(
        string entityType, string entityId, string tableName,
        CustomTableRowValue rowValue, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows", rowValue, ct);

    public async Task<(bool Ok, string? Error)> DeleteCustomTableRowAsync(
        string entityType, string entityId, string tableName, string rowId,
        CancellationToken ct = default)
        => await DeleteWithErrorAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows/{rowId}", ct);

    // ============================================================
    // EavMetadataController - 创建
    // ============================================================

    public Task<string?> CreateAttributeAsync(
        CreateAttributeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/attributes", request, "attributeId", ct);

    public Task<string?> CreateCompositeTypeAsync(
        CreateCompositeTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/composite-types", request, "compositeTypeId", ct);

    public Task<string?> AddCompositeFieldAsync(
        string compositeTypeId, CreateCompositeFieldRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields",
            request, "fieldId", ct);

    public Task<string?> CreateCustomTableAsync(
        CreateCustomTableRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/custom-tables", request, "tableDefinitionId", ct);

    public Task<string?> AddCustomTableColumnAsync(
        string tableDefinitionId, CreateTableColumnRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/custom-tables/{tableDefinitionId}/columns",
            request, "columnId", ct);

    public Task<(bool Ok, string? Error)> DeleteAttributeAsync(
        string attributeId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/attributes/{attributeId}", ct);

    // ============================================================
    // 组合类型
    // ============================================================

    public async Task<IReadOnlyList<CompositeTypeDetailDto>?> ListCompositeTypesAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/composite-types";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<CompositeTypeDetailDto>>(url, ct);
    }

    public Task<CompositeTypeDetailDto?> GetCompositeTypeAsync(
        string id, CancellationToken ct = default)
        => GetAsync<CompositeTypeDetailDto>($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeTypeAsync(
        string id, UpdateCompositeTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/composite-types/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeTypeAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeFieldAsync(
        string compositeTypeId, string fieldId,
        UpdateCompositeFieldRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeFieldAsync(
        string compositeTypeId, string fieldId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}", ct);

    // ============================================================
    // 自定义表
    // ============================================================

    public async Task<IReadOnlyList<CustomTableDetailDto>?> ListCustomTablesAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/custom-tables";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<CustomTableDetailDto>>(url, ct);
    }

    public Task<CustomTableDetailDto?> GetCustomTableAsync(
        string id, CancellationToken ct = default)
        => GetAsync<CustomTableDetailDto>($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCustomTableAsync(
        string id, UpdateCustomTableRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/custom-tables/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCustomTableAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateTableColumnAsync(
        string tableId, string columnId,
        UpdateTableColumnRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteTableColumnAsync(
        string tableId, string columnId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}", ct);

    // ============================================================
    // 属性查询/更新
    // ============================================================

    public async Task<IReadOnlyList<AttributeDetailDto>?> ListAttributesAsync(
        string? entityType = null, bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/attributes";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<AttributeDetailDto>>(url, ct);
    }

    public Task<AttributeDetailDto?> GetAttributeAsync(
        string id, CancellationToken ct = default)
        => GetAsync<AttributeDetailDto>($"api/eav/metadata/attributes/{id}", ct);

    public async Task<IReadOnlyList<EntityTypeSummaryDto>?> ListEntityTypesAsync(
        CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EntityTypeSummaryDto>>(
            "api/eav/entity-types", ct);

    /// <summary>
    /// 一次拉取所有实体类型详情（含属性计数）。
    /// 替代"ListEntityTypesAsync + N 次 GetEntityTypeAsync"的 N+1 模式。
    /// </summary>
    public async Task<IReadOnlyList<EntityTypeDetailDto>?> ListEntityTypeDetailsAsync(
        bool includeDeleted = false, CancellationToken ct = default)
    {
        var url = "api/eav/entity-types/details";
        if (includeDeleted) url += "?includeDeleted=true";
        return await GetAsync<IReadOnlyList<EntityTypeDetailDto>>(url, ct);
    }

    // ============================================================
    // 实体类型 CRUD
    // ============================================================

    public Task<string?> CreateEntityTypeAsync(
        CreateEntityTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/entity-types", request, "entityTypeId", ct);

    public Task<EntityTypeDetailDto?> GetEntityTypeAsync(
        string id, CancellationToken ct = default)
        => GetAsync<EntityTypeDetailDto>($"api/eav/entity-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateEntityTypeAsync(
        string id, UpdateEntityTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/entity-types/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteEntityTypeAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/entity-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UndeleteEntityTypeAsync(
        string id, CancellationToken ct = default)
        => PostNoBodyAsync($"api/eav/entity-types/{id}/undelete", ct);

    public async Task<PagedResult<DynamicEntityDto>?> ListEntitiesAsync(
        string entityType, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var request = new EavQueryRequest
        {
            EntityType = entityType,
            Filters = new List<AttributeFilter>(),
            Page = page,
            PageSize = pageSize
        };
        return await PostAsync<PagedResult<DynamicEntityDto>>(
            $"api/eav/{entityType}/entities/query", request, ct);
    }

    public Task<(bool Ok, string? Error)> UpdateAttributeAsync(
        string id, UpdateAttributeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/attributes/{id}", request, ct);

    // ============================================================
    // 选项集
    // ============================================================

    public Task<string?> CreateOptionSetAsync(
        CreateOptionSetRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/option-sets", request, "optionSetId", ct);

    public async Task<IReadOnlyList<OptionSetSummaryDto>?> ListOptionSetsAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/option-sets";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<OptionSetSummaryDto>>(url, ct);
    }

    public Task<(bool Ok, string? Error)> DeleteOptionSetAsync(
        string optionSetId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/option-sets/{optionSetId}", ct);

    public Task<OptionSetDetailDto?> GetOptionSetAsync(
        string optionSetId,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var url = $"api/eav/metadata/option-sets/{optionSetId}";
        if (includeDeleted) url += "?includeDeleted=true";
        return GetAsync<OptionSetDetailDto>(url, ct);
    }

    public Task<(bool Ok, string? Error)> UpdateOptionSetAsync(
        string optionSetId, UpdateOptionSetRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/option-sets/{optionSetId}", request, ct);

    public Task<IReadOnlyList<OptionSetReferenceDto>?> GetOptionSetReferencesAsync(
        string optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionSetReferenceDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/references", ct);

    // ============================================================
    // 选项项
    // ============================================================

    public Task<IReadOnlyList<OptionItemDetailDto>?> ListOptionItemsAsync(
        string optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionItemDetailDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/items", ct);

    public Task<string?> AddOptionItemAsync(
        string optionSetId, CreateOptionItemRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items",
            request, "optionItemId", ct);

    public Task<(bool Ok, string? Error)> UpdateOptionItemAsync(
        string optionSetId, string itemId, UpdateOptionItemRequest request,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteOptionItemAsync(
        string optionSetId, string itemId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}", ct);

    public Task<(bool Ok, string? Error)> ReorderOptionItemsAsync(
        string optionSetId, List<ReorderOptionItem> orders,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/reorder",
            orders, ct);

    // ============================================================
    // undelete 端点
    // ============================================================

    public Task<(bool Ok, string? Error)> UndeleteAttributeAsync(
        string attributeId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/attributes/{attributeId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCompositeTypeAsync(
        string compositeTypeId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCompositeFieldAsync(
        string compositeTypeId, string fieldId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCustomTableAsync(
        string tableId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/custom-tables/{tableId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteTableColumnAsync(
        string tableId, string columnId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteOptionSetAsync(
        string optionSetId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteOptionItemAsync(
        string optionSetId, string itemId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}/undelete", ct);

    // ============================================================
    // 单位
    // ============================================================

    public async Task<IReadOnlyList<UnitDto>?> ListUnitsAsync(
        string? category = null, CancellationToken ct = default)
    {
        var url = "api/units";
        if (!string.IsNullOrEmpty(category))
            url += $"?category={Uri.EscapeDataString(category)}";
        return await GetAsync<IReadOnlyList<UnitDto>>(url, ct);
    }

    public Task<IReadOnlyList<UnitCategoryDto>?> GetUnitCategoriesAsync(
        CancellationToken ct = default)
        => GetAsync<IReadOnlyList<UnitCategoryDto>>("api/units/categories", ct);

    public async Task<Guid?> CreateUnitAsync(
        CreateUnitRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/units", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("POST api/units → {Status}", resp.StatusCode);
                return null;
            }
            var doc = await resp.Content.ReadFromJsonAsync<IdResponse<Guid>>(JsonOptions, ct);
            return doc?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST api/units 失败");
            return null;
        }
    }

    public async Task<(bool Ok, string? Error)> MigrateUnitCategoryAsync(
        Guid unitId, MigrateUnitCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"api/units/{unitId}/migrate-category", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorAsync(resp, ct);
                _logger.LogWarning("POST migrate-category → {Status}: {Error}",
                    resp.StatusCode, msg);
                return (false, msg);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST migrate-category 失败");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, RecalculateUnitFactorResult? Result, string? Error)>
        RecalculateUnitFactorAsync(
        Guid unitId, RecalculateUnitFactorRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"api/units/{unitId}/recalculate-factor", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorAsync(resp, ct);
                _logger.LogWarning("POST recalculate-factor → {Status}: {Error}",
                    resp.StatusCode, msg);
                return (false, null, msg);
            }

            var result = await resp.Content
                .ReadFromJsonAsync<RecalculateUnitFactorResult>(JsonOptions, ct);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST recalculate-factor 失败");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateUnitAsync(
        Guid unitId, UpdateUnitRequest request, CancellationToken ct = default)
        => await PutAsync($"api/units/{unitId}", request, ct);

    public async Task<(bool Ok, string? Error)> DeleteUnitAsync(
        Guid unitId, CancellationToken ct = default)
        => await DeleteWithErrorAsync($"api/units/{unitId}", ct);

    // ============================================================
    // 通用助手
    // ============================================================

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync(relativeUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<T?> PostAsync<T>(
        string relativeUrl, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("POST {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<(bool Ok, string? Error)> PutAsync(
        string relativeUrl, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string? Error)> DeleteWithErrorAsync(
        string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.DeleteAsync(relativeUrl, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("DELETE {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string? Error)> PostNoBodyAsync(
        string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsync(relativeUrl, content: null, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("POST {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private static async Task<string?> ExtractErrorAsync(
        HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content
                .ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct);
            if (body?.Errors is { Count: > 0 })
                return string.Join("; ",
                    body.Errors.Select(e => $"{e.Field}: {e.Message}"));
            return body?.Error ?? resp.ReasonPhrase;
        }
        catch
        {
            return resp.ReasonPhrase;
        }
    }

    /// <summary>
    /// ★ 按属性名取 ID。ID 值从 JSON 数字改为字符串。
    /// </summary>
    private async Task<string?> PostForIdAsync(
        string relativeUrl, object body, string idPropertyName,
        CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("POST {Url} → {Status}: {Body}",
                    relativeUrl, resp.StatusCode, text);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty(idPropertyName, out var p)
                && p.ValueKind == JsonValueKind.String)
                return p.GetString();

            _logger.LogWarning(
                "POST {Url} → 响应缺少或类型不符 {IdProperty}: {Body}",
                relativeUrl, idPropertyName, doc.RootElement.GetRawText());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return null;
        }
    }

    // ============================================================
    // ★ iNode 声明层
    // ============================================================

    public async Task<IReadOnlyList<InodeTypeCardDto>?> ListInodeTypeCardsAsync(
        string inodeId, CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<InodeTypeCardDto>>(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types", ct);

    public Task<(bool Ok, string? Error)> AttachInodeTypeAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    public Task<(bool Ok, string? Error)> DetachInodeTypeAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    // ============================================================
    // ★ iNode 实体 CRUD
    // ============================================================

    public async Task<IReadOnlyList<DynamicEntityDto>?> ListInodeEntitiesAsync(
        string inodeId, bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<IReadOnlyList<DynamicEntityDto>>(url, ct);
    }

    public async Task<DynamicEntityDto?> GetInodeEntityAsync(
        string inodeId, string entityType,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<DynamicEntityDto>(url, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        SaveInodeEntityAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        return await PutWithConflictAsync(url, values, expectedUpdatedAt, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchInodeEntityAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        return await PatchWithConflictAsync(url, values, expectedUpdatedAt, ct);
    }

    public Task<(bool Ok, string? Error)> DeleteInodeEntityAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    public async Task<IReadOnlyList<EntityHistoryDto>?> GetInodeEntityHistoryAsync(
        string inodeId, string entityType,
        DateTimeOffset? from = null, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}/history";
        if (from.HasValue)
            url += $"?from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        return await GetAsync<IReadOnlyList<EntityHistoryDto>>(url, ct);
    }

    // ============================================================
    // ★ 跨 iNode 查询
    // ============================================================

    public async Task<PagedResult<InodeEntityDto>?> QueryInodesAsync(
        InodeQueryRequest request, CancellationToken ct = default)
        => await PostAsync<PagedResult<InodeEntityDto>>(
            "api/inode/query", request, ct);

    // ============================================================
    // ★ iNode 全景 JSON
    // ============================================================

    /// <summary>
    /// 拉取该 iNode 下所有实体的全景 JSON（字符串形式）。
    ///
    /// 用途：外部应用 / 缓存 / 转发第三方。
    ///
    /// 参数：
    ///   displayName  - true 时属性 key 用中文 DisplayName
    ///   includeNull  - true 时保留值为 null 的属性键
    ///   units        - "default" | "base" | "original"
    /// </summary>
    public async Task<string?> GetInodeAllAsJsonAsync(
        string inodeId,
        bool displayName = false,
        bool includeNull = false,
        string? units = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/json";

        var query = new List<string>();
        if (displayName) query.Add("displayName=true");
        if (includeNull) query.Add("includeNull=true");
        if (!string.IsNullOrWhiteSpace(units))
            query.Add($"units={Uri.EscapeDataString(units)}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}",
                    url, resp.StatusCode);
                return null;
            }

            // ★ 直接返回字符串，不反序列化
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", url);
            return null;
        }
    }

    // ============================================================
    // ★ iNode 原始 JSON 预览
    // ============================================================

    /// <summary>
    /// 拉取 iNode 的原始 JSON（未解析）。
    ///
    /// 参数：
    ///   includeNull      - true 时未写入的属性也以 null 出现
    ///   displayName      - true 时属性 key 用中文 DisplayName
    ///   originalUnits    - true 时数量值按原始输入单位还原（?units=original）
    ///
    /// 返回原始 JSON 字符串；失败返回 null。
    /// </summary>
    public async Task<string?> GetInodeJsonAsync(
        string inodeId,
        bool includeNull = false,
        bool displayName = false,
        bool originalUnits = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (includeNull) query.Add("includeNull=true");
        if (displayName) query.Add("displayName=true");
        if (originalUnits) query.Add("units=original");

        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/json";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}",
                    url, resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", url);
            return null;
        }
    }

    // ============================================================
    // ★ 内部辅助：带乐观锁的 PUT / PATCH
    // ============================================================

    private async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PutWithConflictAsync(
        string url, Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } exp)
                req.Headers.Add("X-Expected-Updated-At",
                    exp.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PUT {Url} → 409：{Body}", url, text);

                DateTimeOffset? current = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(
                        text, JsonOptions);
                    current = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false, "并发冲突：实体已被其他用户修改，请刷新后重试", current);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}",
                url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", url);
            return (false, ex.Message, null);
        }
    }

    private async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchWithConflictAsync(
        string url, Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } exp)
                req.Headers.Add("X-Expected-Updated-At",
                    exp.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PATCH {Url} → 409：{Body}", url, text);

                DateTimeOffset? current = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(
                        text, JsonOptions);
                    current = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false, "并发冲突：实体已被其他用户修改，请刷新后重试", current);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH {Url} 失败", url);
            return (false, ex.Message, null);
        }
    }

    // ============================================================
    // 私有 record
    // ============================================================

    private sealed record SchemaResponse(
        string EntityType, IReadOnlyList<AttributeSchemaDto> Attributes);

    private sealed record QueryByTableResponse(List<string> EntityIds);

    private sealed record IdResponse<T>(T Id);

    private sealed record ErrorResponse(
        string? Error, List<ValidationErrorDto>? Errors);

    private sealed record ValidationErrorDto(string Field, string Message);

    private sealed record ConflictResponse(
        string? Error,
        DateTimeOffset? CurrentUpdatedAt,
        DateTimeOffset? ExpectedUpdatedAt);
}
```

## 文件 8/13 TreeGraph.Blazor/Services/EavFieldValidator.cs

```csharp
using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

public interface IEavFieldValidator
{
    /// <summary>校验属性值，返回结构化错误（Path 相对该属性）。</summary>
    IReadOnlyList<FieldValidationError> Validate(AttributeSchemaDto attr, object? value);

    /// <summary>仅校验 JSON 文本格式，用于 json / file / 自定义表行输入框。</summary>
    string? ValidateJsonText(string? text);
}

public class EavFieldValidator : IEavFieldValidator
{
    // ============================================================
    // 入口
    // ============================================================

    public IReadOnlyList<FieldValidationError> Validate(
        AttributeSchemaDto attr, object? value)
    {
        var errors = new List<FieldValidationError>();
        ValidateAttribute(attr, value, "", errors);
        return errors;
    }

    public string? ValidateJsonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return null;
        }
        catch (JsonException ex)
        {
            return $"JSON 格式错误：{ex.Message}";
        }
    }

    // ============================================================
    // 属性级
    // ============================================================

    private void ValidateAttribute(
        AttributeSchemaDto attr, object? value, string path,
        List<FieldValidationError> errors)
    {
        // 必填（单选有默认值时放行）
        if (attr.IsRequired && IsEmpty(value))
        {
            if (!HasDefaultOption(attr.DataType, attr.OptionSet?.Items))
            {
                errors.Add(new FieldValidationError(path, "必填字段"));
                return;
            }
        }

        if (value is null || IsEmpty(value)) return;

        switch (attr.DataType)
        {
            case "string":
                ValidateStringRules(
                    value.ToString() ?? "", attr.ValidationRule,
                    attr.AllowedValues, path, errors);
                break;

            case "int":
            case "decimal":
                ValidateNumericAttribute(value, attr, path, errors);
                break;

            case "date":
                if (value is string ds && DateOnly.TryParse(ds, out var d))
                    ValidateDateRules(d, attr.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "日期格式错误（应为 yyyy-MM-dd）"));
                break;

            case "time":
                if (value is string ts && TimeOnly.TryParse(ts, out var t))
                    ValidateTimeRules(t, attr.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "时间格式错误（应为 HH:mm:ss）"));
                break;

            case "single_choice":
                if (attr.OptionSet is null)
                {
                    errors.Add(new FieldValidationError(path, "属性未绑定选项集"));
                }
                else
                {
                    var v = value.ToString() ?? "";
                    if (attr.OptionSet.Items.All(i => i.Value != v))
                    {
                        var valid = string.Join("、",
                            attr.OptionSet.Items.Select(i => i.Value));
                        errors.Add(new FieldValidationError(
                            path, $"值 '{v}' 不在选项集中（有效值：{valid}）"));
                    }
                }
                break;

            case "composite":
                if (attr.CompositeType is not null)
                    ValidateComposite(attr.CompositeType, value, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "组合类型未定义"));
                break;

            case "json":
            case "file":
                // 由控件层用 ValidateJsonText 保证文本合法性；这里只做非空检查
                // （已在上方必填检查处理）。若 value 是原始字符串，可进一步解析。
                if (value is string rawJson && !string.IsNullOrWhiteSpace(rawJson))
                {
                    var jsonErr = ValidateJsonText(rawJson);
                    if (jsonErr is not null)
                        errors.Add(new FieldValidationError(path, jsonErr));
                }
                break;

            // table 类型不走 Validate（由 CustomTableEditor 处理）
        }
    }

    /// <summary>
    /// 顶层数值属性校验。
    ///
    /// ★ int 无单位：拒绝小数（写入 ValueInt 会截断）
    /// ★ int 有单位：允许小数（归一化到基准单位可能产生小数，写 ValueDecimal）
    /// ★ 未绑定单位时不允许指定 UnitId；绑定单位时 UnitId 必须在可用范围内
    /// </summary>
    private void ValidateNumericAttribute(
        object value, AttributeSchemaDto attr, string path,
        List<FieldValidationError> errors)
    {
        // 单位归属
        if (value is NumericInput ni)
        {
            if (attr.Unit is null && ni.UnitId is not null)
            {
                errors.Add(new FieldValidationError(
                    path, "该属性未绑定基准单位，不允许指定单位"));
                return;
            }

            if (attr.Unit is not null && ni.UnitId is { } uid
                && attr.AvailableUnits?.All(u => u.Id != uid) == true)
            {
                errors.Add(new FieldValidationError(
                    path, "指定的单位不属于该属性的可用单位"));
                return;
            }
        }

        var d = ToDecimal(value);
        if (d is null)
        {
            errors.Add(new FieldValidationError(path, "数值格式错误"));
            return;
        }

        // ★ int 无单位时才拒绝小数
        if (attr.DataType == "int"
            && attr.Unit is null
            && d.Value != Math.Truncate(d.Value))
        {
            errors.Add(new FieldValidationError(path, "int 类型（无单位）不接受小数"));
            return;
        }

        ValidateNumericRules(d.Value, attr.ValidationRule, path, errors);
    }

    // ============================================================
    // 组合类型递归（含数组）
    // ============================================================

    private void ValidateComposite(
        CompositeTypeSchemaDto type, object? value, string basePath,
        List<FieldValidationError> errors)
    {
        // ★ 修复 P0-1（组合结构非法时静默通过 → 现在显式报错）
        if (value is not IReadOnlyDictionary<string, object?> dict)
        {
            errors.Add(new FieldValidationError(
                basePath, "组合值期望字典结构"));
            return;
        }

        foreach (var field in type.Fields)
        {
            dict.TryGetValue(field.FieldName, out var fieldValue);
            var fieldPath = JoinPath(basePath, field.FieldName);

            // 数组字段
            if (field.IsArray)
            {
                ValidateArrayField(field, fieldValue, fieldPath, errors);
                continue;
            }

            // 必填：single_choice 有默认值时放行（★ 修复 P0-4）
            if (field.IsRequired && IsEmpty(fieldValue))
            {
                if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                    errors.Add(new FieldValidationError(fieldPath, "必填字段"));
                continue;
            }
            if (fieldValue is null || IsEmpty(fieldValue)) continue;

            // 嵌套组合
            if (field.DataType == "composite" && field.NestedType is not null)
            {
                ValidateComposite(field.NestedType, fieldValue, fieldPath, errors);
                continue;
            }

            // 叶子字段
            ValidateLeafField(field, fieldValue, fieldPath, errors);
        }
    }

    private void ValidateArrayField(
        CompositeFieldSchemaDto field, object? value, string path,
        List<FieldValidationError> errors)
    {
        if (field.IsRequired && IsEmpty(value))
        {
            if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                errors.Add(new FieldValidationError(path, "必填字段"));
            return;
        }
        if (value is null) return;

        // 期望 IEnumerable（排除 string）
        if (value is not IEnumerable items || value is string)
        {
            errors.Add(new FieldValidationError(path, "期望数组"));
            return;
        }

        int i = 0;
        foreach (var item in items)
        {
            var itemPath = $"{path}[{i}]";

            if (field.IsRequired && IsEmpty(item))
            {
                if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                    errors.Add(new FieldValidationError(itemPath, "必填字段"));
            }
            else if (!IsEmpty(item))
            {
                if (field.DataType == "composite" && field.NestedType is not null)
                    ValidateComposite(field.NestedType, item, itemPath, errors);
                else
                    ValidateLeafField(field, item, itemPath, errors);
            }
            i++;
        }
    }

    /// <summary>
    /// 组合内叶子字段校验：value 保证非空。
    ///
    /// ★ int 无单位：拒绝小数（写入 ValueInt 会截断）
    /// ★ int 有单位：允许小数（归一化到基准单位可能产生小数，写 ValueDecimal）
    /// ★ 未绑定单位时不允许指定 UnitId；绑定单位时 UnitId 必须在可用范围内
    /// </summary>
    private void ValidateLeafField(
        CompositeFieldSchemaDto field, object value, string path,
        List<FieldValidationError> errors)
    {
        switch (field.DataType)
        {
            case "string":
                ValidateStringRules(
                    value.ToString() ?? "", field.ValidationRule,
                    field.AllowedValues, path, errors);
                break;

            case "int":
            case "decimal":
            {
                // 组合内 decimal 带单位（NumericInput 负载）
                if (value is NumericInput ni)
                {
                    if (field.Unit is null && ni.UnitId is not null)
                    {
                        errors.Add(new FieldValidationError(
                            path, "该字段未绑定基准单位，不允许指定单位"));
                        break;
                    }

                    if (field.Unit is not null && ni.UnitId is { } uid
                        && field.AvailableUnits?.All(u => u.Id != uid) == true)
                    {
                        errors.Add(new FieldValidationError(
                            path, "指定的单位不属于该字段的可用单位"));
                        break;
                    }

                    // ★ int 无单位时才拒绝小数
                    if (field.DataType == "int"
                        && field.Unit is null
                        && ni.Value != Math.Truncate(ni.Value))
                    {
                        errors.Add(new FieldValidationError(
                            path, "int 类型（无单位）不接受小数"));
                        break;
                    }

                    // 范围校验：前端不做单位归一化，按原始输入值近似
                    // （严格的归一化范围校验由后端负责）
                    if (field.ValidationRule is not null)
                        ValidateNumericRules(ni.Value, field.ValidationRule, path, errors);
                    break;
                }

                // 裸数值路径
                var num = ToDecimal(value);
                if (num is null)
                {
                    errors.Add(new FieldValidationError(path, "数值格式错误"));
                    break;
                }

                // ★ int 无单位时才拒绝小数
                if (field.DataType == "int"
                    && field.Unit is null
                    && num.Value != Math.Truncate(num.Value))
                {
                    errors.Add(new FieldValidationError(
                        path, "int 类型（无单位）不接受小数"));
                    break;
                }

                ValidateNumericRules(num.Value, field.ValidationRule, path, errors);
                break;
            }

            case "date":
                if (value is string ds && DateOnly.TryParse(ds, out var d))
                    ValidateDateRules(d, field.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "日期格式错误（应为 yyyy-MM-dd）"));
                break;

            case "time":
                if (value is string ts && TimeOnly.TryParse(ts, out var t))
                    ValidateTimeRules(t, field.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "时间格式错误（应为 HH:mm:ss）"));
                break;

            case "single_choice":
                // 优先用选项集校验，无则回退到 AllowedValues
                if (field.OptionSet is not null)
                {
                    var v = value.ToString() ?? "";
                    if (field.OptionSet.Items.All(i => i.Value != v))
                    {
                        var valid = string.Join("、",
                            field.OptionSet.Items.Select(i => i.Value));
                        errors.Add(new FieldValidationError(
                            path, $"值 '{v}' 不在选项集中（有效值：{valid}）"));
                    }
                }
                else if (field.AllowedValues is JsonElement av
                         && av.ValueKind == JsonValueKind.Array)
                {
                    var allowed = av.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!)
                        .ToList();

                    if (allowed.Count > 0)
                    {
                        var v = value.ToString() ?? "";
                        if (!allowed.Contains(v))
                            errors.Add(new FieldValidationError(
                                path, $"值必须是以下之一：{string.Join("、", allowed)}"));
                    }
                }
                break;

            case "json":
            case "file":
                if (value is string rawJson && !string.IsNullOrWhiteSpace(rawJson))
                {
                    var jsonErr = ValidateJsonText(rawJson);
                    if (jsonErr is not null)
                        errors.Add(new FieldValidationError(path, jsonErr));
                }
                break;
        }
    }

    // ============================================================
    // 规则方法
    // ============================================================

    private static void ValidateStringRules(
        string s, JsonElement? validationRule, JsonElement? allowedValues,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateString(s, validationRule, allowedValues))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateNumericRules(
        decimal value, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateNumeric(value, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateDateRules(
        DateOnly d, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateDate(d, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateTimeRules(
        TimeOnly t, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateTime(t, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    // ============================================================
    // 工具
    // ============================================================

    private static string JoinPath(string basePath, string name)
        => string.IsNullOrEmpty(basePath) ? name : $"{basePath}.{name}";

    private static bool IsEmpty(object? v) => v switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        // 数组（List<object?> / object?[]）无元素视为空
        ICollection c when c.Count == 0 => true,
        _ => false
    };

    /// <summary>
    /// ★ 修复 P0-4 的辅助：判断 single_choice 是否含默认选项（有默认值时必填放行）。
    /// 与后端 EavValidationService.ValidateSingleChoice 语义一致。
    /// </summary>
    private static bool HasDefaultOption(
        string dataType, IReadOnlyList<OptionItemSchemaDto>? items)
        => dataType == "single_choice"
        && items is { Count: > 0 }
        && items.Any(i => i.IsDefault);

    private static decimal? ToDecimal(object? v) => v switch
    {
        decimal d => d,
        long l => l,
        int i => i,
        double db => (decimal)db,
        NumericInput ni => ni.Value,
        string s when decimal.TryParse(s, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };
}
```

## 文件 9/13 TreeGraph.Blazor/Services/EntityTypeDisplayService.cs

```csharp
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// 实体类型"显示名"解析器。
///
/// 缓存 EntityType → DisplayName 映射。
/// UI 中只显示 DisplayName（中文），EntityType（英文标识）仅用于 URL / API。
///
/// Scoped 生命周期：每个 Blazor Circuit 一份缓存。
/// </summary>
public class EntityTypeDisplayService
{
    private readonly EavApiClient _api;
    private Dictionary<string, EntityTypeSummaryDto>? _map;
    private SemaphoreSlim? _loadLock;
    private bool _loaded;

    public EntityTypeDisplayService(EavApiClient api)
    {
        _api = api;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        _loadLock ??= new SemaphoreSlim(1, 1);
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return;

            var list = await _api.ListEntityTypesAsync()
                       ?? new List<EntityTypeSummaryDto>();
            _map = list
                .GroupBy(x => x.EntityType)
                .ToDictionary(g => g.Key, g => g.First());
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Invalidate()
    {
        _loaded = false;
        _map = null;
    }

    /// <summary>
    /// 获取显示名。
    /// DisplayName 为空时回退到 EntityType。
    /// </summary>
    public string GetDisplayName(string? entityType)
    {
        if (string.IsNullOrEmpty(entityType)) return entityType ?? "";

        if (_map is not null
            && _map.TryGetValue(entityType, out var dto)
            && !string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return dto.DisplayName;
        }

        return entityType;
    }

    /// <summary>从 SummaryDto 直接取显示名（不需要查表）。</summary>
    public static string GetDisplayName(EntityTypeSummaryDto dto)
        => string.IsNullOrWhiteSpace(dto.DisplayName)
            ? dto.EntityType
            : dto.DisplayName;
}
```

## 文件 10/13 TreeGraph.Blazor/Services/FieldValidationError.cs

```csharp
namespace TreeGraph.Blazor.Services;

/// <summary>
/// 结构化字段校验错误。
///
/// Path 语义：相对某个 AttributeSchemaDto 的路径。
///   - ""                 → 属性本身
///   - "brand"            → composite 内的 brand 子字段
///   - "brand.name"       → brand 的 name 子字段
///   - "tags[0]"          → 数组 tags 的第 0 个元素
///   - "tags[0].sub"      → 数组 tags 的第 0 个元素的 sub 子字段
///
/// 父级组件按前缀筛选后，剥离前缀传给子组件；子组件只看它负责的部分。
/// </summary>
public sealed record FieldValidationError(string Path, string Message);
```

## 文件 11/13 TreeGraph.Blazor/Services/FieldValidationRules.cs

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// 字段校验规则引擎（纯静态，无状态）。
///
/// 被 EavFieldValidator 与 CustomTableEditor 共用，
/// 保证顶层属性 / 组合字段 / 自定义表列的规则语义一致。
/// </summary>
public static class FieldValidationRules
{
    /// <summary>数值 min/max。rule 为空或非对象 → 返回空列表。</summary>
    public static List<string> ValidateNumeric(decimal value, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("min", out var min) && TryGetDecimal(min, out var minVal)
            && value < minVal)
            errors.Add($"不能小于 {minVal}");

        if (r.TryGetProperty("max", out var max) && TryGetDecimal(max, out var maxVal)
            && value > maxVal)
            errors.Add($"不能大于 {maxVal}");

        return errors;
    }

    /// <summary>字符串：AllowedValues + minLength / maxLength / regex。</summary>
    public static List<string> ValidateString(
        string s, JsonElement? rule, JsonElement? allowedValues)
    {
        var errors = new List<string>();

        // AllowedValues 优先
        if (allowedValues is JsonElement av && av.ValueKind == JsonValueKind.Array)
        {
            var allowed = av.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToList();

            if (allowed.Count > 0 && !allowed.Contains(s))
                errors.Add($"值必须是以下之一：{string.Join("、", allowed)}");
        }

        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minLength", out var minL) && minL.TryGetInt32(out var minLen)
            && s.Length < minLen)
            errors.Add($"长度不能少于 {minLen} 个字符");

        if (r.TryGetProperty("maxLength", out var maxL) && maxL.TryGetInt32(out var maxLen)
            && s.Length > maxLen)
            errors.Add($"长度不能超过 {maxLen} 个字符");

        if (r.TryGetProperty("regex", out var rx) && rx.ValueKind == JsonValueKind.String)
        {
            var pattern = rx.GetString();
            if (!string.IsNullOrEmpty(pattern))
            {
                try
                {
                    if (!Regex.IsMatch(s, pattern))
                        errors.Add(r.TryGetProperty("message", out var m)
                            && m.ValueKind == JsonValueKind.String
                            ? m.GetString()!
                            : "格式不正确");
                }
                catch (ArgumentException) { /* 非法正则忽略 */ }
            }
        }

        return errors;
    }

    /// <summary>日期 minDate / maxDate。</summary>
    public static List<string> ValidateDate(DateOnly d, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minDate", out var minD)
            && minD.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(minD.GetString(), out var minDate) && d < minDate)
            errors.Add($"日期不能早于 {minDate:yyyy-MM-dd}");

        if (r.TryGetProperty("maxDate", out var maxD)
            && maxD.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(maxD.GetString(), out var maxDate) && d > maxDate)
            errors.Add($"日期不能晚于 {maxDate:yyyy-MM-dd}");

        return errors;
    }

    /// <summary>时间 minTime / maxTime。</summary>
    public static List<string> ValidateTime(TimeOnly t, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minTime", out var minT)
            && minT.ValueKind == JsonValueKind.String
            && TimeOnly.TryParse(minT.GetString(), out var minTime) && t < minTime)
            errors.Add($"时间不能早于 {minTime:HH:mm:ss}");

        if (r.TryGetProperty("maxTime", out var maxT)
            && maxT.ValueKind == JsonValueKind.String
            && TimeOnly.TryParse(maxT.GetString(), out var maxTime) && t > maxTime)
            errors.Add($"时间不能晚于 {maxTime:HH:mm:ss}");

        return errors;
    }

    public static bool TryGetDecimal(JsonElement elem, out decimal value)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out value))
            return true;
        if (elem.ValueKind == JsonValueKind.String)
            return decimal.TryParse(elem.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        value = 0;
        return false;
    }
}
```

## 文件 12/13 TreeGraph.Blazor/Services/FilterOperatorCatalog.cs

```csharp
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// 动态查询运算符的元数据。
///
/// 职责：
///   1) 按属性类型列出可用运算符
///   2) 提供运算符的显示名
///   3) 标记运算符是否需要第二个值（between）或多个值（in/nin）
///
/// 与后端 EavQueryService 的运算符支持严格对应，见：
///   TreeGraph.Api/Services/EavQueryService.cs
/// </summary>
public static class FilterOperatorCatalog
{
    public sealed record OperatorInfo(
        string Code,
        string DisplayName,
        bool NeedsValue2 = false,
        bool IsMultiValue = false);

    private static readonly OperatorInfo[] _numeric = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("gt", "大于"),
        new OperatorInfo("gte", "大于等于"),
        new OperatorInfo("lt", "小于"),
        new OperatorInfo("lte", "小于等于"),
        new OperatorInfo("between", "区间", NeedsValue2: true),
        new OperatorInfo("in", "包含于", IsMultiValue: true)
    };

    private static readonly OperatorInfo[] _string = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("like", "包含"),
        new OperatorInfo("startswith", "开头是"),
        new OperatorInfo("endswith", "结尾是"),
        new OperatorInfo("in", "包含于", IsMultiValue: true)
    };

    private static readonly OperatorInfo[] _bool = new[]
    {
        new OperatorInfo("eq", "等于")
    };

    private static readonly OperatorInfo[] _datetime = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("gt", "晚于"),
        new OperatorInfo("gte", "不早于"),
        new OperatorInfo("lt", "早于"),
        new OperatorInfo("lte", "不晚于"),
        new OperatorInfo("between", "区间", NeedsValue2: true)
    };

    private static readonly OperatorInfo[] _date = _datetime;
    private static readonly OperatorInfo[] _time = _datetime;

    private static readonly OperatorInfo[] _singleChoice = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("in", "包含于", IsMultiValue: true),
        new OperatorInfo("nin", "不包含于", IsMultiValue: true)
    };

    /// <summary>属性的基准类型（忽略 unit 后缀）。</summary>
    public static string BaseKind(string dataType) => dataType switch
    {
        "int" or "decimal" => "numeric",
        "string" => "string",
        "bool" => "bool",
        "datetime" => "datetime",
        "date" => "date",
        "time" => "time",
        "single_choice" => "single_choice",
        _ => "unsupported"
    };

    /// <summary>按属性返回可用运算符列表。</summary>
    public static IReadOnlyList<OperatorInfo> For(AttributeSchemaDto attr) => attr.DataType switch
    {
        "int" or "decimal" => _numeric,
        "string" => _string,
        "bool" => _bool,
        "datetime" => _datetime,
        "date" => _date,
        "time" => _time,
        "single_choice" => _singleChoice,
        _ => Array.Empty<OperatorInfo>()
    };

    /// <summary>该属性是否支持动态查询。</summary>
    public static bool IsSupported(AttributeSchemaDto attr) =>
        attr.DataType is "int" or "decimal" or "string" or "bool"
            or "datetime" or "date" or "time" or "single_choice";

    /// <summary>根据 Code 取运算符元数据。</summary>
    public static OperatorInfo? Get(AttributeSchemaDto attr, string code)
        => For(attr).FirstOrDefault(o => o.Code == code);
}
```

## 文件 13/13 TreeGraph.Blazor/Services/NumericInput.cs

```csharp
namespace TreeGraph.Blazor.Services;

public class NumericInput
{
    public decimal Value { get; set; }
    public Guid? UnitId { get; set; }

    public object ToSubmitValue()
        => UnitId is null ? Value : new { value = Value, unitId = UnitId.Value };
}
```

