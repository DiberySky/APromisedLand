# TreeGraph.Api.Tests C# 代码清单

- 生成时间：2026-10-04 21:37:24
- 文件总数：21
- 排除：bin/、obj/、csproj、README.md
- 项目状态：API 测试 100 项

## 文件 1/21 TreeGraph.Api.Tests/Fixtures/EavApiFactory.cs

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 启动一个真实 PostgreSQL 容器 + 完整 WebApplication。
/// 全测试集合共享此 fixture（见 IntegrationCollection）。
/// </summary>
public class EavApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("TreeGraphDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:TreeGraphDb", _postgres.GetConnectionString());
        builder.UseEnvironment("Testing");
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<EavApiFactory> { }
```

## 文件 2/21 TreeGraph.Api.Tests/Fixtures/InMemoryCaches.cs

```csharp
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.Tests.Fixtures;

public sealed class InMemoryCompositeTypeCache : ICompositeTypeCache
{
    private readonly Dictionary<string, CompositeTypeDefinition> _types = new();

    public void Add(CompositeTypeDefinition type) => _types[type.CompositeTypeId] = type;
    public void Clear() => _types.Clear();

    public CompositeTypeDefinition GetType(string id)
        => _types.TryGetValue(id, out var t)
            ? t
            : throw new KeyNotFoundException($"CompositeType {id} 不存在");

    public void Invalidate(string id) => _types.Remove(id);
}

public sealed class InMemoryUnitCache : IUnitCache
{
    private readonly List<Unit> _units = new();

    public void Add(Unit u) => _units.Add(u);
    public void Clear() => _units.Clear();

    public Unit Get(Guid id)
        => _units.FirstOrDefault(u => u.Id == id)
           ?? throw new InvalidOperationException($"Unit {id} 不存在");

    public IReadOnlyList<Unit> GetAll() => _units;
    public IReadOnlyList<Unit> GetByCategory(string category)
        => _units.Where(u => u.Category == category).ToList();
    public Unit? GetBaseUnit(string category)
        => _units.FirstOrDefault(u => u.Category == category && u.IsBaseUnit);
    public void Invalidate() { }
}

public sealed class InMemoryOptionSetCache : IOptionSetCache
{
    private readonly Dictionary<string, OptionSet> _sets = new();

    public void Add(OptionSet s) => _sets[s.OptionSetId] = s;
    public void Clear() => _sets.Clear();

    public OptionSet GetSet(string id)
        => _sets.TryGetValue(id, out var s)
            ? s
            : throw new KeyNotFoundException($"OptionSet {id} 不存在");

    public List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false)
    {
        var query = _sets.Values.AsEnumerable();
        if (!includeDeleted)
            query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType || s.EntityType == "Shared");
        return query.ToList();
    }

    public void Invalidate(string id) => _sets.Remove(id);
}
```

## 文件 3/21 TreeGraph.Api.Tests/Fixtures/InodeTestData.cs

```csharp
using System.Net;
using System.Net.Http.Json;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// iNode 测试用的 schema 初始化辅助。
///
/// 每个 entityType 只初始化一次（用 static 字典 + 锁保护）。
/// 注意两步：先建实体类型（/api/eav/entity-types），
/// 再建属性（/api/eav/metadata/attributes），否则 CreateAttribute 400。
/// </summary>
public static class InodeTestData
{
    private static readonly Dictionary<string, Task> _initByType = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>确保 entityType 已注册（实体类型 + 3 个属性）。</summary>
    public static async Task EnsureTypeAsync(HttpClient client, string entityType)
    {
        Task task;
        await _lock.WaitAsync();
        try
        {
            if (_initByType.TryGetValue(entityType, out var existing))
            {
                task = existing;
            }
            else
            {
                task = CreateTypeAsync(client, entityType);
                _initByType[entityType] = task;
            }
        }
        finally
        {
            _lock.Release();
        }

        // 在锁外 await，避免串行阻塞
        await task;
    }

    private static async Task CreateTypeAsync(HttpClient client, string entityType)
    {
        // ---- 先创建实体类型（否则 CreateAttribute 400）----
        var typeBody = new { entityType, displayName = entityType };
        var typeResp = await client.PostAsJsonAsync(
            "/api/eav/entity-types", typeBody);

        // 409 = 已存在，视为成功
        if (!typeResp.IsSuccessStatusCode
            && typeResp.StatusCode != HttpStatusCode.Conflict)
        {
            var text = await typeResp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Create entity type '{entityType}' failed " +
                $"{typeResp.StatusCode}: {text}");
        }

        // ---- 3 个属性：string / int / decimal ----
        await CreateAttrAsync(client, entityType, "name", "string",
            isRequired: false, isSearchable: true, isSortable: false, displayOrder: 1);
        await CreateAttrAsync(client, entityType, "amount", "int",
            isRequired: false, isSearchable: true, isSortable: true, displayOrder: 2);
        await CreateAttrAsync(client, entityType, "price", "decimal",
            isRequired: false, isSearchable: true, isSortable: true, displayOrder: 3);
    }

    private static async Task CreateAttrAsync(
        HttpClient client, string entityType, string attrName, string dataType,
        bool isRequired, bool isSearchable, bool isSortable, int displayOrder)
    {
        var body = new
        {
            entityType,
            attributeName = attrName,
            displayName = attrName,
            dataType,
            isRequired,
            isSearchable,
            isSortable,
            displayOrder
        };

        var resp = await client.PostAsJsonAsync(
            "/api/eav/metadata/attributes", body);

        // 409 = 已存在（其他测试已建过），视为成功
        if (resp.StatusCode == HttpStatusCode.Conflict) return;
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Create attribute '{entityType}.{attrName}' failed " +
                $"{resp.StatusCode}: {text}");
        }
    }
}
```

## 文件 4/21 TreeGraph.Api.Tests/Fixtures/IntegrationTestBase.cs

```csharp
using System.Net.Http.Json;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

[Collection("Integration")]
public abstract class IntegrationTestBase
{
    protected readonly EavApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(EavApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// 把测试用整数 n 转换为一个合法 GUID 字符串。
    /// 格式：00000000-0000-0000-0000-{n:D12}
    /// 便于在测试日志 / DB 中按数字识别。
    /// </summary>
    protected static string GuidFromInt(long n)
        => $"00000000-0000-0000-0000-{n:D12}";

    /// <summary>
    /// PUT 实体（全量替换语义）。返回 entityId（GUID 字符串）。
    /// </summary>
    protected async Task<string> PutEntityAsync(
        string entityType, string entityId, object payload)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{entityType}/entities/{entityId}", payload);
        resp.EnsureSuccessStatusCode();
        return entityId;
    }
}
```

## 文件 5/21 TreeGraph.Api.Tests/Fixtures/TestData.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 一次性测试数据初始化的静态工具。
/// 用 Lazy&lt;Task&gt; 保证多个测试类并发调用时只执行一次。
/// </summary>
public static class TestData
{
    public const string EntityType = "TestProduct";

    private static readonly Lazy<Task> _init = new(() => EnsureSchemaCoreAsync());
    private static HttpClient? _client;

    /// <summary>注入 HttpClient（由测试基类第一次调用时传入）。</summary>
    public static Task EnsureSchemaAsync(HttpClient client)
    {
        _client = client;
        return _init.Value;
    }

    private static async Task EnsureSchemaCoreAsync()
    {
        if (_client is null)
            throw new InvalidOperationException("请先传入 HttpClient");

        // ---- 先创建实体类型（否则 CreateAttribute 400）----
        await EnsureEntityTypeAsync(EntityType, "测试商品");

        // ---- 属性：int / decimal / string / bool ----
        await CreateAttributeAsync("amount", "amount", "int",
            isRequired: true, isSearchable: true, isSortable: true);
        await CreateAttributeAsync("price", "price", "decimal",
            isRequired: false, isSearchable: true, isSortable: true);
        await CreateAttributeAsync("label", "label", "string",
            isRequired: false, isSearchable: true, isSortable: false);
        await CreateAttributeAsync("flag", "flag", "bool",
            isRequired: false, isSearchable: true, isSortable: false);
    }

    /// <summary>确保实体类型存在（幂等：已存在时跳过）。</summary>
    public static async Task EnsureEntityTypeAsync(string entityType, string displayName)
    {
        var body = new { entityType, displayName };
        var resp = await _client!.PostAsJsonAsync("/api/eav/entity-types", body);

        // 409 Conflict = 已存在，视为成功
        if (resp.StatusCode == HttpStatusCode.Conflict) return;
        resp.EnsureSuccessStatusCode();
    }

    private static async Task CreateAttributeAsync(
        string name, string displayName, string dataType,
        bool isRequired, bool isSearchable, bool isSortable)
    {
        var body = new
        {
            entityType = EntityType,
            attributeName = name,
            displayName,
            dataType,
            isRequired,
            isSearchable,
            isSortable,
            displayOrder = 1
        };

        var resp = await _client!.PostAsJsonAsync("/api/eav/metadata/attributes", body);

        // 已存在（409）视为成功
        if (resp.StatusCode == HttpStatusCode.Conflict) return;

        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"CreateAttribute '{name}' failed {resp.StatusCode}: {text}");
        }
    }
}
```

## 文件 6/21 TreeGraph.Api.Tests/Tests/BatchDeleteTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class BatchDeleteTests : IntegrationTestBase
{
    public BatchDeleteTests(EavApiFactory factory) : base(factory) { }

    [Fact]
    public async Task BatchDelete_SplitsDeletedAndNotFound()
    {
        await TestData.EnsureSchemaAsync(Client);

        // 创建 3 个
        var existing = new[] { GuidFromInt(94001), GuidFromInt(94002), GuidFromInt(94003) };
        foreach (var id in existing)
        {
            await PutEntityAsync(TestData.EntityType, id,
                new Dictionary<string, object?> { ["amount"] = 1L });
        }

        // 请求里混合：2 个存在 + 1 个不存在 + 1 个重复
        var req = new BatchDeleteRequest
        {
            EntityIds = new List<string> { GuidFromInt(94001), GuidFromInt(94002), GuidFromInt(94001), GuidFromInt(94999) }
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/batch-delete", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<BatchDeleteResultDto>();
        Assert.NotNull(result);
        Assert.Equal(new[] { GuidFromInt(94001), GuidFromInt(94002) }, result!.Deleted.OrderBy(x => x));
        Assert.Contains(GuidFromInt(94999), result.NotFound);
        Assert.DoesNotContain(GuidFromInt(94003), result.Deleted);   // 94003 未在请求中
    }

    [Fact]
    public async Task BatchDelete_EmptyList_ReturnsEmpty()
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/batch-delete",
            new BatchDeleteRequest { EntityIds = new List<string>() });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<BatchDeleteResultDto>();
        Assert.NotNull(result);
        Assert.Empty(result!.Deleted);
        Assert.Empty(result.NotFound);
    }
}
```

## 文件 7/21 TreeGraph.Api.Tests/Tests/CompositeValueServiceTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// CompositeValueService 的序列化/反序列化单测。
/// 不触 DB：用内存 stub 提供 type / unit / option set。
/// </summary>
public class CompositeValueServiceTests
{
    private readonly InMemoryCompositeTypeCache _compositeCache = new();
    private readonly InMemoryUnitCache _unitCache = new();
    private readonly InMemoryOptionSetCache _optionSetCache = new();
    private readonly UnitConverter _converter;
    private readonly CompositeValueService _service;

    // 测试类型 ID（GUID 字符串）
    private const string SpecsTypeId = "00000000-0000-0000-0000-000000000100";
    private const string BrandTypeId = "00000000-0000-0000-0000-000000000101";
    private const string NoUnitTypeId = "00000000-0000-0000-0000-000000000200";

    private Guid _kg;
    private Guid _g;
    private Guid _cm;

    public CompositeValueServiceTests()
    {
        _converter = new UnitConverter(_unitCache);

        _kg = Guid.NewGuid();
        _g = Guid.NewGuid();
        _cm = Guid.NewGuid();
        _unitCache.Add(new Unit
        {
            Id = _kg, Category = "weight", Name = "千克", Symbol = "kg",
            ToBaseFactor = 1m, IsBaseUnit = true
        });
        _unitCache.Add(new Unit
        {
            Id = _g, Category = "weight", Name = "克", Symbol = "g",
            ToBaseFactor = 0.001m, IsBaseUnit = false
        });
        _unitCache.Add(new Unit
        {
            Id = _cm, Category = "length", Name = "厘米", Symbol = "cm",
            ToBaseFactor = 0.01m, IsBaseUnit = false
        });

        // Brand 组合
        var brandType = new CompositeTypeDefinition
        {
            CompositeTypeId = BrandTypeId,
            TypeName = "Brand",
            DisplayName = "品牌",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = "00000000-0000-0000-0001-000000000011",
                        FieldName = "name", DataType = EavDataTypes.String, DisplayOrder = 1 },
                new() { FieldId = "00000000-0000-0000-0001-000000000012",
                        FieldName = "origin", DataType = EavDataTypes.String, DisplayOrder = 2 }
            }
        };
        _compositeCache.Add(brandType);

        // Specs 组合
        var specsType = new CompositeTypeDefinition
        {
            CompositeTypeId = SpecsTypeId,
            TypeName = "Specs",
            DisplayName = "规格",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = "00000000-0000-0000-0002-000000000001",
                        FieldName = "color", DataType = EavDataTypes.String, DisplayOrder = 1 },
                new() { FieldId = "00000000-0000-0000-0002-000000000002",
                        FieldName = "weight", DataType = EavDataTypes.Decimal,
                        UnitId = _kg, DisplayOrder = 2 },
                new() { FieldId = "00000000-0000-0000-0002-000000000003",
                        FieldName = "brand", DataType = EavDataTypes.Composite,
                        RefCompositeTypeId = BrandTypeId, DisplayOrder = 3 }
            }
        };
        _compositeCache.Add(specsType);

        // NoUnit 组合
        var noUnitType = new CompositeTypeDefinition
        {
            CompositeTypeId = NoUnitTypeId,
            TypeName = "NoUnit",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = "00000000-0000-0000-0003-000000000020",
                        FieldName = "ratio", DataType = EavDataTypes.Decimal }
            }
        };
        _compositeCache.Add(noUnitType);

        var validator = new EavValidationService(
            _compositeCache, _unitCache, _converter, _optionSetCache);
        _service = new CompositeValueService(
            _compositeCache, validator, _unitCache, _converter);
    }

    // ============================================================
    // 辅助
    // ============================================================

    private static DynamicCompositeValue MakeValue(
        string typeName, params (string Name, object? Value)[] fields)
    {
        var v = new DynamicCompositeValue(typeName);
        foreach (var (n, val) in fields) v[n] = val;
        return v;
    }

    private static JsonElement Root(JsonDocument doc) => doc.RootElement;

    // ============================================================
    // Serialize — 基础类型
    // ============================================================

    [Fact]
    public void Serialize_StringField_OutputsRawString()
    {
        var v = MakeValue("Specs", ("color", "red"));
        var doc = _service.Serialize(v, SpecsTypeId);

        Assert.Equal("red", Root(doc).GetProperty("color").GetString());
    }

    // ============================================================
    // Serialize — 无单位 decimal → 裸数值
    // ============================================================

    [Fact]
    public void Serialize_DecimalWithoutUnit_OutputsBareNumber()
    {
        var v = MakeValue("NoUnit", ("ratio", 1.5m));
        var doc = _service.Serialize(v, NoUnitTypeId);

        Assert.Equal(JsonValueKind.Number, Root(doc).GetProperty("ratio").ValueKind);
        Assert.Equal(1.5m, Root(doc).GetProperty("ratio").GetDecimal());
    }

    // ============================================================
    // Serialize — 带单位 decimal
    // ============================================================

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputIsBaseUnit_OutputsBareNumber()
    {
        var v = MakeValue("Specs", ("weight", 5m));
        var doc = _service.Serialize(v, SpecsTypeId);

        Assert.Equal(JsonValueKind.Number, Root(doc).GetProperty("weight").ValueKind);
        Assert.Equal(5m, Root(doc).GetProperty("weight").GetDecimal());
    }

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputHasUnit_OutputsObjectWithNormalizedValue()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(1000m, _g)));
        var doc = _service.Serialize(v, SpecsTypeId);

        var weight = Root(doc).GetProperty("weight");
        Assert.Equal(JsonValueKind.Object, weight.ValueKind);
        Assert.Equal(1m, weight.GetProperty("value").GetDecimal());
        Assert.Equal(_g, weight.GetProperty("unitId").GetGuid());
    }

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputIsExplicitNumericValueWithBaseUnit_OutputsObject()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(3m, _kg)));
        var doc = _service.Serialize(v, SpecsTypeId);

        var weight = Root(doc).GetProperty("weight");
        Assert.Equal(JsonValueKind.Object, weight.ValueKind);
        Assert.Equal(3m, weight.GetProperty("value").GetDecimal());
        Assert.Equal(_kg, weight.GetProperty("unitId").GetGuid());
    }

    // ============================================================
    // Serialize — 嵌套组合
    // ============================================================

    [Fact]
    public void Serialize_NestedComposite_RecursesIntoNestedJson()
    {
        var brand = MakeValue("Brand", ("name", "Acme"), ("origin", "US"));
        var v = MakeValue("Specs", ("brand", brand));
        var doc = _service.Serialize(v, SpecsTypeId);

        var brandElem = Root(doc).GetProperty("brand");
        Assert.Equal(JsonValueKind.Object, brandElem.ValueKind);
        Assert.Equal("Acme", brandElem.GetProperty("name").GetString());
        Assert.Equal("US", brandElem.GetProperty("origin").GetString());
    }

    // ============================================================
    // Deserialize
    // ============================================================

    [Fact]
    public void Deserialize_WithUnitObject_DefaultReturnsBaseUnitValue()
    {
        var json = $$"""
        {
            "weight": { "value": 1, "unitId": "{{_g}}" }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: false);

        var weight = result["weight"];
        Assert.IsType<NumericValue>(weight);
        var nv = (NumericValue)weight!;
        Assert.Equal(1m, nv.Value);
        Assert.Equal(_kg, nv.UnitId);
    }

    [Fact]
    public void Deserialize_WithUnitObject_OriginalUnits_RestoresInputUnit()
    {
        var json = $$"""
        {
            "weight": { "value": 1, "unitId": "{{_g}}" }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(1000m, nv.Value);
        Assert.Equal(_g, nv.UnitId);
    }

    [Fact]
    public void Deserialize_BareNumberWithUnitField_DefaultsToBaseUnit()
    {
        var json = """{"weight": 5}""";

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(5m, nv.Value);
        Assert.Equal(_kg, nv.UnitId);
    }

    [Fact]
    public void Deserialize_UnknownUnitId_FallsBackToBaseUnit()
    {
        var orphanUnit = Guid.NewGuid();
        var json = $$"""
        {
            "weight": { "value": 2, "unitId": "{{orphanUnit}}" }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(2m, nv.Value);
        Assert.Equal(_kg, nv.UnitId);
    }

    // ============================================================
    // 往返
    // ============================================================

    [Fact]
    public void RoundTrip_WithUnit_PreservesOriginalValue()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(1500m, _g)));

        var doc = _service.Serialize(v, SpecsTypeId);

        var stored = Root(doc).GetProperty("weight");
        Assert.Equal(1.5m, stored.GetProperty("value").GetDecimal());

        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);
        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(1500m, nv.Value);
        Assert.Equal(_g, nv.UnitId);
    }

    [Fact]
    public void RoundTrip_NestedComposite_PreservesValues()
    {
        var brand = MakeValue("Brand", ("name", "Acme"), ("origin", "US"));
        var v = MakeValue("Specs", ("color", "red"), ("brand", brand));

        var doc = _service.Serialize(v, SpecsTypeId);
        var result = _service.Deserialize(doc, SpecsTypeId);

        Assert.Equal("red", result["color"]);
        var brandOut = result["brand"] as DynamicCompositeValue;
        Assert.NotNull(brandOut);
        Assert.Equal("Acme", brandOut!["name"]);
        Assert.Equal("US", brandOut["origin"]);
    }

    // ============================================================
    // 缺失字段
    // ============================================================

    [Fact]
    public void Deserialize_MissingField_LeavesItUnset()
    {
        var json = """{"color": "blue"}""";
        using var doc = JsonDocument.Parse(json);

        var result = _service.Deserialize(doc, SpecsTypeId);

        Assert.Equal("blue", result["color"]);
        Assert.Null(result["weight"]);
        Assert.Null(result["brand"]);
    }

    [Fact]
    public void Serialize_NullField_OutputsNull()
    {
        var v = MakeValue("Specs", ("color", null));
        var doc = _service.Serialize(v, SpecsTypeId);

        Assert.Equal(JsonValueKind.Null, Root(doc).GetProperty("color").ValueKind);
    }
}
```

## 文件 8/21 TreeGraph.Api.Tests/Tests/CrossRowUniqueTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// CustomTableWriteService.UpsertRowAsync 跨行唯一性测试。
///
/// 唯一性语义：
///   - 同一父实体、同一属性、同一表下，IsUnique 列的值不可重复
///   - 更新自身（RowId 指向已存在的行）不与自己冲突
///   - 删除后释放该值
///   - 跨父实体互相隔离
///
/// 隔离：本类用独立 entityType "UniqueTest*"，避免与其它测试类干扰。
/// </summary>
public class CrossRowUniqueTests : IntegrationTestBase
{
    private const string EntityType = "UniqueTestProduct";

    public CrossRowUniqueTests(EavApiFactory factory) : base(factory) { }

    // ============================================================
    // 初始化：建表 + 列 + 属性
    // ============================================================

    private static bool _schemaReady;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);
    private static string _tableDefId = "";

    private async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await _schemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;

            // 0. 先创建实体类型（否则后续 CreateAttribute 400）
            await TestData.EnsureEntityTypeAsync(EntityType, "唯一性测试");

            // 1. 建表
            var tableResp = await Client.PostAsJsonAsync(
                "/api/eav/metadata/custom-tables",
                new
                {
                    entityType = EntityType,
                    tableName = "unique_certs",
                    displayName = "认证",
                    displayOrder = 1
                });
            tableResp.EnsureSuccessStatusCode();
            _tableDefId = (await tableResp.Content
                .ReadFromJsonAsync<IdResponse>())!.TableDefinitionId;

            // 2. 加列（IsUnique = true）
            var colResp = await Client.PostAsJsonAsync(
                $"/api/eav/metadata/custom-tables/{_tableDefId}/columns",
                new
                {
                    columnName = "cert_name",
                    displayName = "证书名",
                    dataType = "string",
                    isUnique = true,
                    isRequired = true,
                    displayOrder = 1
                });
            colResp.EnsureSuccessStatusCode();

            // 3. 建属性引用该表
            var attrResp = await Client.PostAsJsonAsync(
                "/api/eav/metadata/attributes",
                new
                {
                    entityType = EntityType,
                    attributeName = "unique_certs",
                    displayName = "认证",
                    dataType = "table",
                    refTableDefinitionId = _tableDefId,
                    displayOrder = 1
                });
            attrResp.EnsureSuccessStatusCode();
            // 顺带验证建属性响应字段名 attributeId 可解析
            var attrId = (await attrResp.Content
                .ReadFromJsonAsync<AttrIdResponse>())!.AttributeId;
            Assert.False(string.IsNullOrEmpty(attrId));

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private async Task<HttpResponseMessage> UpsertRowAsync(
        string entityId, string certName, string? rowId = null)
    {
        var body = new
        {
            rowId,
            rowOrder = 1,
            fields = new Dictionary<string, object?> { ["cert_name"] = certName }
        };
        return await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{entityId}/tables/unique_certs/rows",
            body);
    }

    // ============================================================
    // 首次插入 → OK
    // ============================================================

    [Fact]
    public async Task Upsert_FirstRow_Succeeds()
    {
        await EnsureSchemaAsync();

        var resp = await UpsertRowAsync(GuidFromInt(96001), "CERT-A");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 同父实体同值 → 400
    // ============================================================

    [Fact]
    public async Task Upsert_DuplicateInSameParent_Returns400()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96002);
        var first = await UpsertRowAsync(parent, "CERT-DUP");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await UpsertRowAsync(parent, "CERT-DUP");   // 新增第二行，同值
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("CERT-DUP", body);
    }

    // ============================================================
    // 更新自身 → OK（不与自己冲突）
    // ============================================================

    [Fact]
    public async Task Upsert_UpdateSelfToSameValue_Succeeds()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96003);
        await UpsertRowAsync(parent, "SELF-X");

        // 拿到 rowId
        var tableValue = await Client.GetFromJsonAsync<CustomTableValue>(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs");
        var rowId = tableValue!.Rows[0].RowId!;

        // 更新自身（同值）→ 应 OK
        var resp = await UpsertRowAsync(parent, "SELF-X", rowId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 跨父实体：隔离
    // ============================================================

    [Fact]
    public async Task Upsert_SameValueInDifferentParents_Isolated()
    {
        await EnsureSchemaAsync();

        var a = await UpsertRowAsync(GuidFromInt(96004), "SHARED-VAL");
        var b = await UpsertRowAsync(GuidFromInt(96005), "SHARED-VAL");   // 不同父实体

        Assert.Equal(HttpStatusCode.NoContent, a.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, b.StatusCode);
    }

    // ============================================================
    // 删除释放唯一值
    // ============================================================

    [Fact]
    public async Task Upsert_AfterDelete_ValueCanBeReused()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96006);
        await UpsertRowAsync(parent, "RELEASE-X");

        var tableValue = await Client.GetFromJsonAsync<CustomTableValue>(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs");
        var rowId = tableValue!.Rows[0].RowId!;

        // 删除该行
        var del = await Client.DeleteAsync(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs/rows/{rowId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 重新插入同值 → 应 OK
        var resp = await UpsertRowAsync(parent, "RELEASE-X");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 辅助 record
    // ============================================================

    private sealed record IdResponse(string TableDefinitionId);
    private sealed record AttrIdResponse(string AttributeId);
}
```

## 文件 9/21 TreeGraph.Api.Tests/Tests/InodeConstraintTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 核心约束（R2 / R3）测试。
///
/// R2：(inodeId, entityType) → 唯一 entityId
/// R3：entityId → 唯一 inodeId
///
/// 由于 entityId 由服务端生成、用户不感知，R3 通过"两 iNode 独立创建"来间接验证。
/// </summary>
public class InodeConstraintTests : IntegrationTestBase
{
    private const string EntityType = "InodeConstraintTest";

    public InodeConstraintTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // R2：同 iNode 同类型只能 1 个实体
    // ============================================================

    [Fact]
    public async Task SameInodeSameType_AlwaysReturnsSameEntityId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 5 次 PUT 同 (iNode, type)
        for (int i = 0; i < 5; i++)
        {
            await Client.PutAsJsonAsync(
                $"/api/inode/{inodeId}/entities/{EntityType}",
                new Dictionary<string, object?> { ["name"] = $"v{i}" });
        }

        // 只有 1 个实体
        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");
        Assert.NotNull(list);
        Assert.Single(list!);

        // 最后一次值生效
        Assert.Equal("v4", list![0].Properties["name"].GetString());
    }

    // ============================================================
    // R3：两个 iNode 同类型 → 2 个不同 entityId
    // ============================================================

    [Fact]
    public async Task DifferentInodes_ProduceDifferentEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        var inodeC = NewInodeId();

        foreach (var inodeId in new[] { inodeA, inodeB, inodeC })
        {
            await Client.PutAsJsonAsync(
                $"/api/inode/{inodeId}/entities/{EntityType}",
                new Dictionary<string, object?> { ["name"] = inodeId[..8] });
        }

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeA}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeB}/entities/{EntityType}");
        var dtoC = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeC}/entities/{EntityType}");

        var ids = new[] { dtoA!.EntityId, dtoB!.EntityId, dtoC!.EntityId };
        Assert.Equal(3, ids.Distinct().Count());
    }

    // ============================================================
    // 同 iNode 多类型 → 各自独立 entityId
    // ============================================================

    [Fact]
    public async Task SameInodeDifferentTypes_HaveIndependentEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}B",
            new Dictionary<string, object?> { ["name"] = "B" });

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}B");

        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);
        Assert.NotEqual(dtoA.EntityType, dtoB.EntityType);
    }

    // ============================================================
    // 声明层与实体层的独立幂等性
    // ============================================================

    [Fact]
    public async Task ExplicitAttach_ThenPut_ReusesDeclaration()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 显式声明
        await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        // 然后 PUT
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        // 声明列表里只有 1 条
        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.True(list![0].HasEntity);
    }

    // ============================================================
    // 唯一性跨 PUT / PATCH
    // ============================================================

    [Fact]
    public async Task Patch_CannotCreateDuplicate()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        // 对不存在的实体 PATCH（首次）→ 会自动创建
        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/inode/{inodeId}/entities/{EntityType}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 100L
            })
        };
        await Client.SendAsync(req);

        // 仍然只有 1 个实体
        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");
        Assert.NotNull(list);
        Assert.Single(list!);
    }
}
```

## 文件 10/21 TreeGraph.Api.Tests/Tests/InodeEavFacadeTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode Facade 与现有 EAV 管道互操作测试。
///
/// 核心验证："通过 iNode 写入的数据，能用旧 EAV API 读到，反之亦然"。
/// 这保证了方案 B 的"零破坏"承诺。
/// </summary>
public class InodeEavFacadeTests : IntegrationTestBase
{
    private const string EntityType = "InodeFacadeTest";

    public InodeEavFacadeTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // iNode 写 → 旧 EAV 读
    // ============================================================

    [Fact]
    public async Task WriteViaInode_ReadViaOldEavApi_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 通过 iNode 写入
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "via-inode",
                ["amount"] = 77L
            });

        // 从 iNode 拿到 entityId
        var viaInode = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.NotNull(viaInode);

        // 用旧 EAV API 读
        var viaEav = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{viaInode!.EntityId}");

        Assert.NotNull(viaEav);
        Assert.Equal(viaInode.EntityId, viaEav!.EntityId);
        Assert.Equal("via-inode", viaEav.Properties["name"].GetString());
        Assert.Equal(77L, viaEav.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 旧 EAV 写 → iNode 读
    // ============================================================

    [Fact]
    public async Task WriteViaOldEavApi_ReadViaInode_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 先通过 iNode 创建，拿到 entityId
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "seed" });

        var created = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.NotNull(created);
        var entityId = created!.EntityId;

        // 用旧 EAV API 改
        await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{entityId}",
            new Dictionary<string, object?>
            {
                ["name"] = "via-eav",
                ["amount"] = 88L
            });

        // 从 iNode 读
        var viaInode = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.NotNull(viaInode);
        Assert.Equal("via-eav", viaInode!.Properties["name"].GetString());
        Assert.Equal(88L, viaInode.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 审计历史
    // ============================================================

    [Fact]
    public async Task History_ViaInode_ReturnsLogs()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v2" });

        var history = await Client.GetFromJsonAsync<List<EntityHistoryDto>>(
            $"/api/inode/{inodeId}/entities/{EntityType}/history");

        Assert.NotNull(history);
        Assert.NotEmpty(history!);
        // 至少有一次 Insert 或 Update
        Assert.Contains(history, h =>
            h.ChangeType == "Insert" || h.ChangeType == "Update");
    }

    [Fact]
    public async Task History_NotCreated_ReturnsEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var history = await Client.GetFromJsonAsync<List<EntityHistoryDto>>(
            $"/api/inode/{inodeId}/entities/{EntityType}/history");

        Assert.NotNull(history);
        Assert.Empty(history!);
    }

    // ============================================================
    // 删除后旧 EAV 也不可见
    // ============================================================

    [Fact]
    public async Task Delete_ViaInode_OldEavApiSeesEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "to-delete" });

        var created = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        var entityId = created!.EntityId;

        // 通过 iNode 删除
        var del = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 旧 EAV API 契约：不存在的实体返回 200 + 空 Properties（无 404 分支）
        var viaEav = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{entityId}");
        Assert.NotNull(viaEav);
        Assert.False(viaEav!.Properties.ContainsKey("name"));
    }
}
```

## 文件 11/21 TreeGraph.Api.Tests/Tests/InodeEntityTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 归属层（inode_entity）集成测试。
/// 覆盖 GetOrCreate / Patch / Delete / 隔离。
/// </summary>
public class InodeEntityTests : IntegrationTestBase
{
    private const string EntityType = "InodeEntityTest";

    public InodeEntityTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 首次创建
    // ============================================================

    [Fact]
    public async Task Put_FirstTime_CreatesEntity()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "first",
                ["amount"] = 42L
            });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.NotNull(dto);
        Assert.False(string.IsNullOrEmpty(dto!.EntityId));
        Assert.Equal(EntityType, dto.EntityType);
        Assert.Equal("first", dto.Properties["name"].GetString());
        Assert.Equal(42L, dto.Properties["amount"].GetInt64());
    }

    [Fact]
    public async Task Put_SecondTime_SameEntityId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "first" });

        var dto1 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "second" });

        var dto2 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal(dto1!.EntityId, dto2!.EntityId);
        Assert.Equal("second", dto2.Properties["name"].GetString());
    }

    // ============================================================
    // 读取
    // ============================================================

    [Fact]
    public async Task Get_NotCreated_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.GetAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // PATCH
    // ============================================================

    [Fact]
    public async Task Patch_PartialUpdate_KeepsOthers()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "orig", ["amount"] = 1L });

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/inode/{inodeId}/entities/{EntityType}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 99L
            })
        };
        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal("orig", dto!.Properties["name"].GetString());
        Assert.Equal(99L, dto.Properties["amount"].GetInt64());
    }

    // ============================================================
    // DELETE
    // ============================================================

    [Fact]
    public async Task Delete_RemovesEntityAndMapping()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        var del = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 实体已不可读
        var get = await Client.GetAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_NotCreated_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Put_ThenDelete_CanRecreateWithNewId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        var dto1 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v2" });

        var dto2 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        // 新 GUID
        Assert.NotEqual(dto1!.EntityId, dto2!.EntityId);
        Assert.Equal("v2", dto2.Properties["name"].GetString());
    }

    // ============================================================
    // 列表
    // ============================================================

    [Fact]
    public async Task List_ReturnsAllCreatedEntities()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}B",
            new Dictionary<string, object?> { ["name"] = "B" });

        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task List_EmptyInode_ReturnsEmpty()
    {
        var inodeId = NewInodeId();

        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");

        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    // ============================================================
    // 隔离
    // ============================================================

    [Fact]
    public async Task TwoInodes_SameType_AreIsolated()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeA}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A-value" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeB}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "B-value" });

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeA}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeB}/entities/{EntityType}");

        // 两个不同 entityId
        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);

        // 数据隔离
        Assert.Equal("A-value", dtoA.Properties["name"].GetString());
        Assert.Equal("B-value", dtoB.Properties["name"].GetString());
    }

    /// <summary>
    /// 回归：声明但无属性的类型，实体可创建但无法写入。
    ///
    /// 修复前：GetOrCreateEntityIdAsync 误查 attribute_catalog，
    ///         对无属性类型 PUT 直接 400"实体类型不存在"。
    /// 修复后：实体创建成功，写入阶段因"未知属性"被 400 拒绝。
    /// </summary>
    [Fact]
    public async Task Put_OnTypeWithoutAttributes_FailsValidation()
    {
        // 建类型（不建属性）
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { displayName = $"无属性写入_{Guid.NewGuid():N}" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        Assert.NotNull(created);

        var inodeId = NewInodeId();

        // PUT 会走到 EavValidationException（未知属性）
        var resp = await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{created!.EntityType}",
            new Dictionary<string, object?> { ["whatever"] = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("未知属性", body);
    }

    private sealed record CreateTypeResponse(string EntityTypeId, string EntityType);
}
```

## 文件 12/21 TreeGraph.Api.Tests/Tests/InodeEntityTypeTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 声明层（inode_entitytype）集成测试。
/// </summary>
public class InodeEntityTypeTests : IntegrationTestBase
{
    private const string EntityType = "InodeEtTest";

    public InodeEntityTypeTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // Attach
    // ============================================================

    [Fact]
    public async Task Attach_ThenList_ContainsType()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var attach = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");

        Assert.NotNull(list);
        var card = Assert.Single(list!);
        Assert.Equal(EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.False(card.HasEntity);
    }

    [Fact]
    public async Task Attach_IsIdempotent()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var r1 = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);
        var r2 = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        Assert.Equal(HttpStatusCode.NoContent, r1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, r2.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.Single(list!);
    }

    [Fact]
    public async Task Attach_UnknownType_Returns400()
    {
        var inodeId = NewInodeId();
        var unknown = $"Nonexistent_{Guid.NewGuid():N}";

        var resp = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{unknown}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("不存在", body);
    }

    // ============================================================
    // Detach
    // ============================================================

    [Fact]
    public async Task Detach_WithoutEntity_RemovesDeclaration()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        var detach = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Detach_NotDeclared_Returns404()
    {
        var inodeId = NewInodeId();

        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Detach_WithEntity_Returns400()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 首次 PUT 会自动建立声明 + 实体
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        // Detach 应被拒绝
        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("实体", body);
    }

    // ============================================================
    // List 混合场景
    // ============================================================

    [Fact]
    public async Task List_MultipleDeclarations_ReturnsAll()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();
        await Client.PostAsync($"/api/inode/{inodeId}/types/{EntityType}", null);
        await Client.PostAsync($"/api/inode/{inodeId}/types/{EntityType}B", null);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, c => c.EntityType == EntityType);
        Assert.Contains(list, c => c.EntityType == EntityType + "B");
    }

    /// <summary>
    /// 回归：允许声明"尚未定义任何属性"的类型。
    ///
    /// 修复前：InodeEntityService.AttachAsync 误查 attribute_catalog，
    ///         导致无属性的类型被判定为不存在 → 400。
    /// </summary>
    [Fact]
    public async Task Attach_TypeWithoutAttributes_Succeeds()
    {
        // 直接建类型（不建属性）
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { displayName = $"无属性类型_{Guid.NewGuid():N}" });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        Assert.NotNull(created);

        var inodeId = NewInodeId();

        // 声明应该成功（修复前会 400）
        var resp = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{created!.EntityType}", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 列表能看到
        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.NotNull(list);
        var card = Assert.Single(list!);
        Assert.Equal(created.EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.False(card.HasEntity);
    }

    private sealed record CreateTypeResponse(string EntityTypeId, string EntityType);
}
```

## 文件 13/21 TreeGraph.Api.Tests/Tests/InodeJsonTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class InodeJsonTests : IntegrationTestBase
{
    private const string EntityType = "InodeJsonTest";

    public InodeJsonTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    [Fact]
    public async Task GetAllAsJson_BasicShape()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 写入数据
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "demo",
                ["amount"] = 42L
            });

        // 拉 JSON
        var json = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 顶层元信息
        Assert.Equal(inodeId, root.GetProperty("inodeId").GetString());
        Assert.True(root.TryGetProperty("generatedAt", out _));

        // entities.Product
        var entity = root.GetProperty("entities")
            .GetProperty(EntityType);
        Assert.Equal("demo", entity.GetProperty("properties")
            .GetProperty("name").GetString());
        Assert.Equal(42, entity.GetProperty("properties")
            .GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task GetAllAsJson_IncludeNull_KeepsKeys()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "only-name" });

        // 默认：amount 不出现在 properties
        var jsonDefault = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");
        using var doc1 = JsonDocument.Parse(jsonDefault);
        var props1 = doc1.RootElement.GetProperty("entities")
            .GetProperty(EntityType).GetProperty("properties");
        Assert.False(props1.TryGetProperty("amount", out _));

        // includeNull=true：amount 出现且为 null
        var jsonInclude = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json?includeNull=true");
        using var doc2 = JsonDocument.Parse(jsonInclude);
        var props2 = doc2.RootElement.GetProperty("entities")
            .GetProperty(EntityType).GetProperty("properties");
        Assert.True(props2.TryGetProperty("amount", out var amount));
        Assert.Equal(JsonValueKind.Null, amount.ValueKind);
    }

    [Fact]
    public async Task GetAllAsJson_InvalidUnits_Returns400()
    {
        var inodeId = NewInodeId();
        var resp = await Client.GetAsync(
            $"/api/inode/{inodeId}/json?units=xxx");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetAllAsJson_EmptyInode_ReturnsEmptyEntities()
    {
        var inodeId = NewInodeId();
        var json = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");

        using var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");
        Assert.Equal(JsonValueKind.Object, entities.ValueKind);
        Assert.Equal(0, entities.EnumerateObject().Count());
    }
}
```

## 文件 14/21 TreeGraph.Api.Tests/Tests/InodeQueryTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 跨 iNode 查询（/api/inode/query）集成测试。
/// </summary>
public class InodeQueryTests : IntegrationTestBase
{
    private const string EntityType = "InodeQueryTest";

    public InodeQueryTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    /// <summary>
    /// 无 inodeId 限定的查询会扫全库同类型实体，
    /// 用每次唯一的 entityType 隔离，避免同类其他用例的数据污染。
    /// </summary>
    private async Task<string> NewTypeAsync()
    {
        var t = "InodeQ" + Guid.NewGuid().ToString("N")[..8];
        await InodeTestData.EnsureTypeAsync(Client, t);
        return t;
    }

    private async Task SeedAsync(
        string entityType, string inodeId, string name, long amount)
    {
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{entityType}",
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["amount"] = amount
            });
    }

    // ============================================================
    // 基础查询
    // ============================================================

    [Fact]
    public async Task Query_ByInodeId_ReturnsOnlyThatInode()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(EntityType, inodeA, "A-item", 10L);
        await SeedAsync(EntityType, inodeB, "B-item", 20L);

        var req = new InodeQueryRequest
        {
            InodeId = inodeA,
            EntityType = EntityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(inodeA, result.Items[0].InodeId);
        Assert.Equal("A-item", result.Items[0].Properties["name"].GetString());
    }

    [Fact]
    public async Task Query_NoInodeId_ReturnsAcrossInodes()
    {
        var entityType = await NewTypeAsync();

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(entityType, inodeA, "A-item", 10L);
        await SeedAsync(entityType, inodeB, "B-item", 20L);

        var req = new InodeQueryRequest
        {
            InodeId = null,       // 跨 iNode
            EntityType = entityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);

        // 每条都带正确的 inodeId
        Assert.Contains(result.Items, x =>
            x.InodeId == inodeA && x.Properties["name"].GetString() == "A-item");
        Assert.Contains(result.Items, x =>
            x.InodeId == inodeB && x.Properties["name"].GetString() == "B-item");
    }

    // ============================================================
    // 属性过滤 + iNode 限定
    // ============================================================

    [Fact]
    public async Task Query_WithAttributeFilter_ReturnsMatches()
    {
        var entityType = await NewTypeAsync();

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(entityType, inodeA, "small", 10L);
        await SeedAsync(entityType, inodeB, "large", 100L);

        var req = new InodeQueryRequest
        {
            EntityType = entityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "amount",
                    Operator = "gt",
                    Value = 50L
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal("large", result.Items[0].Properties["name"].GetString());
    }

    [Fact]
    public async Task Query_WithInodeAndFilter_Intersects()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(EntityType, inodeA, "A-large", 100L);
        await SeedAsync(EntityType, inodeB, "B-large", 200L);

        var req = new InodeQueryRequest
        {
            InodeId = inodeA,
            EntityType = EntityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "amount",
                    Operator = "gt",
                    Value = 50L
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(inodeA, result.Items[0].InodeId);
    }

    // ============================================================
    // 空结果 & 错误
    // ============================================================

    [Fact]
    public async Task Query_InodeWithoutEntity_ReturnsEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var req = new InodeQueryRequest
        {
            InodeId = inodeId,
            EntityType = EntityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Query_MissingEntityType_Returns400()
    {
        var req = new InodeQueryRequest
        {
            EntityType = "",       // 缺失
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
```

## 文件 15/21 TreeGraph.Api.Tests/Tests/MetadataUndeleteTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 元数据软删除恢复的集成测试。
///
/// 注意：`UndeleteAttribute` 里的"同名活动属性已存在"检查是防御性代码——
/// 由于 `uq_attr_catalog` 唯一约束不区分 `IsDeleted`，通过 API 无法构造
/// "同名活动 + 同名软删"共存的场景，因此不写测试。
/// 若未来唯一约束放宽（如加入 IsDeleted 到键），届时再补。
/// </summary>
public class MetadataUndeleteTests : IntegrationTestBase
{
    public MetadataUndeleteTests(EavApiFactory factory) : base(factory) { }

    /// <summary>软删除属性后 undelete → 应恢复。</summary>
    [Fact]
    public async Task UndeleteAttribute_AfterSoftDelete_Succeeds()
    {
        // 先创建实体类型（否则 CreateAttribute 400）
        await TestData.EnsureEntityTypeAsync("UndeleteTest", "恢复测试");

        // 先创建一个独立属性
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "UndeleteTest",
                attributeName = "foo",
                displayName = "foo",
                dataType = "string",
                displayOrder = 1
            });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(created);

        // 软删除
        var delResp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{created!.AttributeId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // 恢复
        var unResp = await Client.PostAsync(
            $"/api/eav/metadata/attributes/{created.AttributeId}/undelete",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, unResp.StatusCode);

        // 验证出现在列表
        var listResp = await Client.GetAsync(
            "/api/eav/metadata/attributes?entityType=UndeleteTest");
        var list = await listResp.Content
            .ReadFromJsonAsync<List<AttributeDetailDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, a =>
            a.AttributeId == created.AttributeId && !a.IsDeleted);
    }

    private sealed record IdResponse(string AttributeId);
}
```

## 文件 16/21 TreeGraph.Api.Tests/Tests/OptimisticLockTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class OptimisticLockTests : IntegrationTestBase
{
    public OptimisticLockTests(EavApiFactory factory) : base(factory) { }

    /// <summary>
    /// 同一瞬间、不同 offset（+08:00）回传，应视为"匹配"。
    /// 修复前：DateTimeOffset.!= 会比较 offset → 误判 409。
    /// </summary>
    [Fact]
    public async Task Put_SameInstantDifferentOffset_Succeeds()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(90001);
        await PutEntityAsync(TestData.EntityType, id,
            new Dictionary<string, object?> { ["amount"] = 10L });

        // 拿 UpdatedAt
        var getResp = await Client.GetAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        var entity = await getResp.Content.ReadFromJsonAsync<DynamicEntityDto>();
        Assert.NotNull(entity?.UpdatedAt);

        // 把 UTC 时间转 +08:00，序列化后回传
        var plus8 = entity!.UpdatedAt!.Value.ToOffset(TimeSpan.FromHours(8));
        var header = plus8.ToString("O");

        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 10L,
                ["label"] = "patched"
            })
        };
        req.Headers.Add("X-Expected-Updated-At", header);

        var resp = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    /// <summary>
    /// 陈旧版本号 → 应返回 409 + currentUpdatedAt。
    /// </summary>
    [Fact]
    public async Task Put_StaleUpdatedAt_Returns409WithCurrentUpdatedAt()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(90002);
        await PutEntityAsync(TestData.EntityType, id,
            new Dictionary<string, object?> { ["amount"] = 20L });

        // 用错误的期望时间（比真实时间早 1 天）
        var stale = DateTimeOffset.UtcNow.AddDays(-1);

        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 21L
            })
        };
        req.Headers.Add("X-Expected-Updated-At", stale.ToString("O"));

        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("currentUpdatedAt", body);
    }
}
```

## 文件 17/21 TreeGraph.Api.Tests/Tests/OptionSetSoftDeleteTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// OptionSet 软删除 / 恢复的集成测试。
///
/// 覆盖：
///   - 软删后默认 GET 404 / includeDeleted=true 返回 isDeleted=true
///   - 软删后默认 List 排除 / includeDeleted=true 包含
///   - partial unique index：软删后同名可重建；恢复撞同名 → 409
///   - 恢复：集合 + 选项一并恢复
///   - 幂等：未删的恢复是 No-Op 204
///   - 软删 = 集合 + 全部选项都标 IsDeleted
///   - GetReferences 不受 IsDeleted 影响
///
/// 边界（通过 API 不可构造，不测）：
///   - 已引用集合被软删（DeleteSet 拒绝被引用的集合）
///   - 软删后同名活动集合与已删集合共存（partial unique 允许，但恢复会 409）
///
/// 隔离策略：本类所有 entityType 以 "OptSoftTest*" 开头，与其它测试类的
/// "TestProduct" / "Product" 等互不干扰。
/// </summary>
public class OptionSetSoftDeleteTests : IntegrationTestBase
{
    public OptionSetSoftDeleteTests(EavApiFactory factory) : base(factory) { }

    private sealed record IdResponse(string OptionSetId);

    // ============================================================
    // 辅助
    // ============================================================

    private async Task<string> CreateSetAsync(
        string entityType, string setName,
        params (string Value, string Label)[] items)
    {
        // 确保实体类型存在（否则后续 CreateAttribute 400）
        await TestData.EnsureEntityTypeAsync(entityType, entityType);

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType, setName, displayName = setName });
        resp.EnsureSuccessStatusCode();

        var created = await resp.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(created);

        foreach (var (value, label) in items)
        {
            var r = await Client.PostAsJsonAsync(
                $"/api/eav/metadata/option-sets/{created!.OptionSetId}/items",
                new { value, label, displayOrder = 0, isDefault = false });
            Assert.True(r.IsSuccessStatusCode,
                $"Create item '{value}' failed: {r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }

        return created!.OptionSetId;
    }

    private async Task DeleteSetAsync(string setId)
    {
        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<HttpResponseMessage> UndeleteSetRawAsync(string setId)
        => await Client.PostAsync(
            $"/api/eav/metadata/option-sets/{setId}/undelete", content: null);

    // ============================================================
    // 软删基础语义
    // ============================================================

    [Fact]
    public async Task SoftDelete_DefaultGet_Returns404()
    {
        var setId = await CreateSetAsync("OptSoftGet404", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync($"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SoftDelete_IncludeDeletedGet_Returns200WithIsDeletedTrue()
    {
        var setId = await CreateSetAsync("OptSoftGetInc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}?includeDeleted=true");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var detail = await resp.Content.ReadFromJsonAsync<OptionSetDetailDto>();
        Assert.NotNull(detail);
        Assert.True(detail!.IsDeleted);
        Assert.Equal(setId, detail.OptionSetId);
    }

    [Fact]
    public async Task SoftDelete_DefaultList_ExcludesDeleted()
    {
        var setId = await CreateSetAsync("OptSoftListExc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            "/api/eav/metadata/option-sets?entityType=OptSoftListExc");
        resp.EnsureSuccessStatusCode();

        var list = await resp.Content.ReadFromJsonAsync<List<OptionSetSummaryDto>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, s => s.OptionSetId == setId);
    }

    [Fact]
    public async Task SoftDelete_IncludeDeletedList_ContainsDeleted()
    {
        var setId = await CreateSetAsync("OptSoftListInc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            "/api/eav/metadata/option-sets?entityType=OptSoftListInc&includeDeleted=true");
        resp.EnsureSuccessStatusCode();

        var list = await resp.Content.ReadFromJsonAsync<List<OptionSetSummaryDto>>();
        Assert.NotNull(list);
        var found = list!.FirstOrDefault(s => s.OptionSetId == setId);
        Assert.NotNull(found);
        Assert.True(found!.IsDeleted);
    }

    // ============================================================
    // Partial unique index 语义
    // ============================================================

    [Fact]
    public async Task Create_WhenActiveNameExists_ReturnsConflict()
    {
        await CreateSetAsync("OptSoftDupActive", "dup", ("a", "A"));

        // 同名活动集合 → 唯一索引拒绝
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType = "OptSoftDupActive", setName = "dup", displayName = "dup2" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Create_AfterSoftDelete_SameName_AllowedByPartialIndex()
    {
        var setId1 = await CreateSetAsync("OptSoftDupAfterDel", "dup", ("a", "A"));
        await DeleteSetAsync(setId1);

        // 软删后同名可重建（partial unique index: WHERE is_deleted = false）
        var setId2 = await CreateSetAsync("OptSoftDupAfterDel", "dup", ("b", "B"));
        Assert.NotEqual(setId1, setId2);
    }

    [Fact]
    public async Task Undelete_WhenActiveDuplicateExists_ReturnsConflict()
    {
        var setId1 = await CreateSetAsync("OptSoftUndupConflict", "dup", ("a", "A"));
        await DeleteSetAsync(setId1);

        // 重建同名活动集合
        await CreateSetAsync("OptSoftUndupConflict", "dup", ("b", "B"));

        // 恢复 setId1 → 409
        var resp = await UndeleteSetRawAsync(setId1);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("同名", body);
    }

    // ============================================================
    // 恢复语义
    // ============================================================

    [Fact]
    public async Task Undelete_RestoresSetAndAllItems()
    {
        var setId = await CreateSetAsync("OptSoftRestoreAll", "s",
            ("a", "A"), ("b", "B"), ("c", "C"));

        await DeleteSetAsync(setId);

        var resp = await UndeleteSetRawAsync(setId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 默认 GET 应 200
        var getResp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        getResp.EnsureSuccessStatusCode();

        var detail = await getResp.Content.ReadFromJsonAsync<OptionSetDetailDto>();
        Assert.NotNull(detail);
        Assert.False(detail!.IsDeleted);
        Assert.Equal(3, detail.Items.Count);
        Assert.All(detail.Items, i => Assert.False(i.IsDeleted));
    }

    [Fact]
    public async Task Undelete_OnActiveSet_Idempotent_204()
    {
        var setId = await CreateSetAsync("OptSoftIdempotent", "s", ("a", "A"));

        // 未删除 → 幂等 204
        var resp = await UndeleteSetRawAsync(setId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 软删的连带效果
    // ============================================================

    [Fact]
    public async Task DeleteSet_SoftDeletesAllItems()
    {
        var setId = await CreateSetAsync("OptSoftCascade", "s",
            ("a", "A"), ("b", "B"));

        await DeleteSetAsync(setId);

        // ListOptionItems 不按 IsDeleted 过滤（管理页需要看到已删项）
        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/items");
        resp.EnsureSuccessStatusCode();

        var items = await resp.Content
            .ReadFromJsonAsync<List<OptionItemDetailDto>>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.All(items, i => Assert.True(i.IsDeleted));
    }

    // ============================================================
    // GetReferences 不受 IsDeleted 影响
    // ============================================================

    [Fact]
    public async Task GetReferences_ReturnsActiveReferences_OnActiveSet()
    {
        var setId = await CreateSetAsync("OptSoftRefs", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptSoftRefsEntity", "引用测试");

        // 建属性引用
        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "OptSoftRefsEntity",
                attributeName = "choice",
                displayName = "选择",
                dataType = "single_choice",
                refOptionSetId = setId,
                displayOrder = 1
            });
        Assert.True(attrResp.IsSuccessStatusCode,
            $"Create attribute failed: {attrResp.StatusCode} {await attrResp.Content.ReadAsStringAsync()}");

        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/references");
        resp.EnsureSuccessStatusCode();

        var refs = await resp.Content
            .ReadFromJsonAsync<List<OptionSetReferenceDto>>();
        Assert.NotNull(refs);
        Assert.Single(refs!);
        Assert.Equal("choice", refs![0].AttributeName);
    }

    // ============================================================
    // DeleteSet 拒绝被引用的集合（保持既有保守策略）
    // ============================================================

    [Fact]
    public async Task DeleteSet_WhenReferencedByActiveAttribute_Returns400()
    {
        var setId = await CreateSetAsync("OptSoftRefGuard", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptSoftRefGuardEntity", "引用守卫");

        await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "OptSoftRefGuardEntity",
                attributeName = "choice",
                displayName = "选择",
                dataType = "single_choice",
                refOptionSetId = setId,
                displayOrder = 1
            });

        // 被引用 → DeleteSet 应 400
        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("引用", body);
    }
}
```

## 文件 18/21 TreeGraph.Api.Tests/Tests/PatchEntityTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// PATCH /entities/{id} 集成测试。
///
/// 语义关键点（与 PUT 的唯一差异）：
///   - values 中**未出现**的属性：保持不变（PUT 会删除）
///   - values 中值为 null 的属性：删除（与 PUT 一致）
///
/// 复用 TestData.EnsureSchemaAsync 里的 amount / price / label / flag 四个属性。
/// </summary>
public class PatchEntityTests : IntegrationTestBase
{
    public PatchEntityTests(EavApiFactory factory) : base(factory) { }

    // ============================================================
    // 部分更新：未提供的属性保持不动
    // ============================================================

    [Fact]
    public async Task Patch_OnlyUpdatesProvidedFields_LeavesOthersIntact()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95001);
        // 初始：amount=1, price=100, label="original"
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["price"] = 100m,
            ["label"] = "original"
        });

        // PATCH 只更新 price
        var patch = new Dictionary<string, object?>
        {
            ["price"] = 200m
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 核对：price=200，其它未变
        var after = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.Equal(1L, after!.Properties["amount"].GetInt64());
        Assert.Equal(200m, after.Properties["price"].GetDecimal());
        Assert.Equal("original", after.Properties["label"].GetString());
    }

    // ============================================================
    // 显式 null 删除
    // ============================================================

    [Fact]
    public async Task Patch_NullValue_DeletesField()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95002);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "to-be-removed"
        });

        // PATCH 显式 null → 删除 label
        var patch = new Dictionary<string, object?>
        {
            ["label"] = null
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var after = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.False(after!.Properties.ContainsKey("label"));   // 已删除
        Assert.Equal(1L, after.Properties["amount"].GetInt64()); // 未动
    }

    // ============================================================
    // 未知属性
    // ============================================================

    [Fact]
    public async Task Patch_UnknownAttribute_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95003);
        var patch = new Dictionary<string, object?>
        {
            ["amoutn"] = 2L   // typo
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("amoutn", body);
    }

    // ============================================================
    // 乐观锁
    // ============================================================

    [Fact]
    public async Task Patch_StaleUpdatedAt_Returns409()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95004);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L
        });

        // 用陈旧时间
        var stale = DateTimeOffset.UtcNow.AddDays(-1);

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 99L
            })
        };
        req.Headers.Add("X-Expected-Updated-At", stale.ToString("O"));

        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ============================================================
    // 空 PATCH 不产生任何变更
    // ============================================================

    [Fact]
    public async Task Patch_EmptyBody_NoOp()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95005);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 42L
        });

        var patch = new Dictionary<string, object?>();
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var after = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.Equal(42L, after!.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 与 PUT 的差异对比
    // ============================================================

    [Fact]
    public async Task Put_OmitsAttribute_DeletesIt_WhilePatchDoesNot()
    {
        await TestData.EnsureSchemaAsync(Client);

        // === 场景 A：PUT 只提供 amount → label 被删除 ===
        var idA = GuidFromInt(95006);
        await PutEntityAsync(TestData.EntityType, idA, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "will-be-deleted-by-put"
        });

        await PutEntityAsync(TestData.EntityType, idA, new Dictionary<string, object?>
        {
            ["amount"] = 2L   // 没提供 label
        });

        var afterA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{idA}");
        Assert.NotNull(afterA);
        Assert.False(afterA!.Properties.ContainsKey("label"));   // PUT 删除了

        // === 场景 B：PATCH 只提供 amount → label 保留 ===
        var idB = GuidFromInt(95007);
        await PutEntityAsync(TestData.EntityType, idB, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "will-be-kept-by-patch"
        });

        var patch = new Dictionary<string, object?>
        {
            ["amount"] = 2L
        };
        var patchResp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{idB}", patch);
        Assert.Equal(HttpStatusCode.NoContent, patchResp.StatusCode);

        var afterB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{idB}");
        Assert.NotNull(afterB);
        Assert.Equal("will-be-kept-by-patch", afterB!.Properties["label"].GetString());
    }
}
```

## 文件 19/21 TreeGraph.Api.Tests/Tests/QueryTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class QueryTests : IntegrationTestBase
{
    public QueryTests(EavApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Query_OrderByDecimalDesc_SortsCorrectly()
    {
        await TestData.EnsureSchemaAsync(Client);

        // 三档 price
        var ids = new[] { GuidFromInt(93001), GuidFromInt(93002), GuidFromInt(93003) };
        var prices = new[] { 10.5m, 99.9m, 5.0m };
        for (int i = 0; i < ids.Length; i++)
        {
            await PutEntityAsync(TestData.EntityType, ids[i],
                new Dictionary<string, object?>
                {
                    ["amount"] = 1L,
                    ["price"] = prices[i]
                });
        }

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            OrderByAttribute = "price",
            OrderDescending = true,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<PagedResult<DynamicEntityDto>>();
        Assert.NotNull(result);

        var sorted = result!.Items
            .Where(x => ids.Contains(x.EntityId))
            .Select(x =>
            {
                Assert.True(x.Properties.TryGetValue("price", out var elem));
                return elem.GetDecimal();
            })
            .ToList();

        Assert.Equal(new[] { 99.9m, 10.5m, 5.0m }, sorted);
    }

    /// <summary>decimal 的 `in` 运算符 → 200（修复前 500）。</summary>
    [Fact]
    public async Task Query_DecimalInOperator_Returns200()
    {
        await TestData.EnsureSchemaAsync(Client);

        await PutEntityAsync(TestData.EntityType, GuidFromInt(93010),
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 42m });
        await PutEntityAsync(TestData.EntityType, GuidFromInt(93011),
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 43m });

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "price",
                    Operator = "in",
                    Value = new[] { 42m, 43m }
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>不可排序属性 → 400。</summary>
    [Fact]
    public async Task Query_OrderByNonSortable_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            OrderByAttribute = "label",   // IsSortable = false
            Page = 1,
            PageSize = 10
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
```

## 文件 20/21 TreeGraph.Api.Tests/Tests/TreeSkyTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// TreeSky 后端集成测试：覆盖 TreeControllerBase&lt;StringTreeNode&gt; 的 9 个端点
/// （roots/children/query/full/POST 创建/POST children 排序/PUT/DELETE/move/ancestors）。
/// 每个测试自建节点、用后清理，互不依赖种子数据。
/// </summary>
public class TreeSkyTests(EavApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Base = "/StringTreeNode";

    // ==================== 辅助 ====================

    private async Task<TreeNodeDto<StringTreeNode>> CreateNodeAsync(
        string name, string? parentId = null, int sortOrder = 0)
    {
        var resp = await Client.PostAsJsonAsync(Base, new TreeNodeDto<StringTreeNode>
        {
            Text = name,
            ParentId = parentId,
            SortOrder = sortOrder,
            Value = new StringTreeNode { Name = name, SortOrder = sortOrder },
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>();
        Assert.True(body!.Success, body.Message);
        return body.Data!;
    }

    private async Task DeleteNodeAsync(string id)
        => await Client.DeleteAsync($"{Base}/{id}");

    // ==================== 查询端点 ====================

    [Fact]
    public async Task Roots_ReturnsSeededRoot()
    {
        var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>($"{Base}/roots");
        Assert.True(body!.Success);
        Assert.Contains(body.Data!, n => n.Text == "物品总类");
    }

    [Fact]
    public async Task Roots_ById_ReturnsSingleRoot()
    {
        var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>($"{Base}/roots/root");
        Assert.True(body!.Success);
        var node = Assert.Single(body.Data!);
        Assert.Equal("root", node.Id);
    }

    [Fact]
    public async Task Children_ReturnsSortedChildren()
    {
        var parent = await CreateNodeAsync("子查询父");
        var c2 = await CreateNodeAsync("B 子", parent.Id, 2);
        var c1 = await CreateNodeAsync("A 子", parent.Id, 1);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{parent.Id}");
            Assert.True(body!.Success);
            Assert.Equal(2, body.Data!.Count);
            Assert.Equal(c1.Id, body.Data![0].Id); // SortOrder 1 在前
            Assert.Equal(c2.Id, body.Data![1].Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id); // 级联删除子节点
        }
    }

    [Fact]
    public async Task Query_FiltersByParentId()
    {
        var parent = await CreateNodeAsync("查询父");
        var child = await CreateNodeAsync("查询子", parent.Id);
        try
        {
            var resp = await Client.PostAsJsonAsync($"{Base}/query",
                new TreeQueryParams { ParentId = parent.Id });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>();
            var hit = Assert.Single(body!.Data!);
            Assert.Equal(child.Id, hit.Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id);
        }
    }

    [Fact]
    public async Task FullTree_NestsAllDescendants()
    {
        var root = await CreateNodeAsync("整树根");
        var child = await CreateNodeAsync("整树子", root.Id);
        var grandchild = await CreateNodeAsync("整树孙", child.Id);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>(
                $"{Base}/full?rootId={root.Id}");
            Assert.True(body!.Success);
            var tree = body.Data!;
            Assert.Equal(root.Id, tree.Id);
            var childDto = Assert.Single(tree.Children!);
            Assert.Equal(child.Id, childDto.Id);
            var grandDto = Assert.Single(childDto.Children!);
            Assert.Equal(grandchild.Id, grandDto.Id);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    [Fact]
    public async Task Ancestors_ReturnsRootToTargetPath()
    {
        var root = await CreateNodeAsync("路径根");
        var child = await CreateNodeAsync("路径子", root.Id);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<List<string>>>($"{Base}/{child.Id}/ancestors");
            Assert.True(body!.Success);
            Assert.Equal(new List<string> { root.Id, child.Id }, body.Data!);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    // ==================== 写入端点 ====================

    [Fact]
    public async Task Create_AssignsIdAndPersists()
    {
        var created = await CreateNodeAsync("新建节点");
        try
        {
            Assert.False(string.IsNullOrWhiteSpace(created.Id));
            Assert.Equal("新建节点", created.Text);
            Assert.False(created.HasChildren);
        }
        finally
        {
            await DeleteNodeAsync(created.Id);
        }
    }

    [Fact]
    public async Task Update_ChangesNameAndSortOrder()
    {
        var created = await CreateNodeAsync("改前");
        try
        {
            var resp = await Client.PutAsJsonAsync($"{Base}/{created.Id}", new TreeNodeDto<StringTreeNode>
            {
                Id = created.Id,
                Text = "改后",
                SortOrder = 7,
                Value = new StringTreeNode { Id = created.Id, Name = "改后", SortOrder = 7 },
            });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>();
            Assert.Equal("改后", body!.Data!.Value!.Name);
            Assert.Equal(7, body.Data.SortOrder);
        }
        finally
        {
            await DeleteNodeAsync(created.Id);
        }
    }

    [Fact]
    public async Task UpdateChildren_ReordersByGivenSequence()
    {
        var parent = await CreateNodeAsync("排序父");
        var a = await CreateNodeAsync("A", parent.Id, 0);
        var b = await CreateNodeAsync("B", parent.Id, 1);
        try
        {
            // 传入 b 在前 a 在后 → SortOrder 应翻转
            var resp = await Client.PostAsJsonAsync($"{Base}/children", new TreeNodeDto<StringTreeNode>
            {
                Id = parent.Id,
                Text = "排序父",
                Children =
                [
                    new TreeNodeDto<StringTreeNode> { Id = b.Id, Text = "B" },
                    new TreeNodeDto<StringTreeNode> { Id = a.Id, Text = "A" },
                ],
            });
            resp.EnsureSuccessStatusCode();

            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{parent.Id}");
            Assert.Equal(b.Id, body!.Data![0].Id);
            Assert.Equal(a.Id, body.Data![1].Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id);
        }
    }

    [Fact]
    public async Task Delete_CascadesAllDescendants()
    {
        var root = await CreateNodeAsync("删除根");
        var child = await CreateNodeAsync("删除子", root.Id);
        var grandchild = await CreateNodeAsync("删除孙", child.Id);

        var resp = await Client.DeleteAsync($"{Base}/{root.Id}");
        resp.EnsureSuccessStatusCode();

        // 子孙应已被级联删除
        var childResp = await Client.GetAsync($"{Base}/{child.Id}/ancestors");
        Assert.Equal(HttpStatusCode.NotFound, childResp.StatusCode);
        var grandResp = await Client.GetAsync($"{Base}/{grandchild.Id}/ancestors");
        Assert.Equal(HttpStatusCode.NotFound, grandResp.StatusCode);
    }

    [Fact]
    public async Task Move_ReparentsNode()
    {
        var p1 = await CreateNodeAsync("父一");
        var p2 = await CreateNodeAsync("父二");
        var child = await CreateNodeAsync("被移动", p1.Id);
        try
        {
            var resp = await Client.PostAsync($"{Base}/move?nodeId={child.Id}&newParentId={p2.Id}", null);
            resp.EnsureSuccessStatusCode();

            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{p2.Id}");
            Assert.Contains(body!.Data!, n => n.Id == child.Id);

            var oldBody = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{p1.Id}");
            Assert.Empty(oldBody!.Data!);
        }
        finally
        {
            await DeleteNodeAsync(p1.Id);
            await DeleteNodeAsync(p2.Id); // 级联删除 child
        }
    }

    [Fact]
    public async Task Move_ToOwnDescendant_IsRejected()
    {
        var root = await CreateNodeAsync("防环根");
        var child = await CreateNodeAsync("防环子", root.Id);
        try
        {
            // 把 root 移到自己的子节点 child 下 → 应失败
            var resp = await Client.PostAsync($"{Base}/move?nodeId={root.Id}&newParentId={child.Id}", null);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

            // 自环同样拒绝
            var selfResp = await Client.PostAsync($"{Base}/move?nodeId={root.Id}&newParentId={root.Id}", null);
            Assert.Equal(HttpStatusCode.BadRequest, selfResp.StatusCode);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    [Fact]
    public async Task Delete_Nonexistent_ReturnsNotFound()
    {
        var resp = await Client.DeleteAsync($"{Base}/no-such-node-id");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

## 文件 21/21 TreeGraph.Api.Tests/Tests/ValidationTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class ValidationTests : IntegrationTestBase
{
    public ValidationTests(EavApiFactory factory) : base(factory) { }

    /// <summary>P0-3：未知属性 → 400 + errors[] 包含拼错的键名。</summary>
    [Fact]
    public async Task Put_UnknownAttribute_Returns400WithErrorList()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(91001);
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["amoutn"] = 2L   // typo
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("amoutn", body);
    }

    /// <summary>int 属性填小数 → 400（不静默截断）。</summary>
    [Fact]
    public async Task Put_IntWithDecimal_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(91002);
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 3.14m
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("int", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>table 类型属性通过 PUT entities 写入 → 400 + 引导正确端点。</summary>
    [Fact]
    public async Task Put_TableAttribute_Returns400WithGuidance()
    {
        // 依赖 EavSeeder 的 Product.certifications 属性
        var payload = new Dictionary<string, object?>
        {
            ["certifications"] = Array.Empty<object>()
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/Product/entities/{GuidFromInt(92001)}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("table", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tables/", body);
    }
}
```

