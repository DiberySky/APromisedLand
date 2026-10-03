# TreeGraph 测试项目 C# 代码清单

- 生成时间：2026-10-03 05:53:59
- 范围：TreeGraph.Api.Tests + TreeGraph.Blazor.Tests（含 .csproj 与全部 .cs）
- 排除：bin/、obj/

## 文件 1 TreeGraph.Api.Tests/TreeGraph.Api.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
        <PackageReference Include="xunit" Version="2.9.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\TreeGraph.Api\TreeGraph.Api.csproj" />
        <ProjectReference Include="..\TreeGraph.Shared\TreeGraph.Shared.csproj" />
    </ItemGroup>

</Project>
```

## 文件 2 TreeGraph.Blazor.Tests/TreeGraph.Blazor.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageReference Include="xunit" Version="2.9.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
        <!-- ★ 新增：bUnit 组件测试框架 -->
        <PackageReference Include="bunit" Version="1.40.0" />
        <!-- ★ 新增：启动级 smoke 测试（WebApplicationFactory<Program>） -->
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\TreeGraph.Blazor\TreeGraph.Blazor.csproj" />
        <ProjectReference Include="..\TreeGraph.Shared\TreeGraph.Shared.csproj" />
    </ItemGroup>

</Project>
```

## 文件 3 TreeGraph.Api.Tests/Fixtures/EavApiFactory.cs

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

## 文件 4 TreeGraph.Api.Tests/Fixtures/InMemoryCaches.cs

```csharp
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;

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

## 文件 5 TreeGraph.Api.Tests/Fixtures/IntegrationTestBase.cs

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

## 文件 6 TreeGraph.Api.Tests/Fixtures/TestData.cs

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

## 文件 7 TreeGraph.Api.Tests/Tests/BatchDeleteTests.cs

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

## 文件 8 TreeGraph.Api.Tests/Tests/CompositeValueServiceTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
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

## 文件 9 TreeGraph.Api.Tests/Tests/CrossRowUniqueTests.cs

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

## 文件 10 TreeGraph.Api.Tests/Tests/MetadataUndeleteTests.cs

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

## 文件 11 TreeGraph.Api.Tests/Tests/OptimisticLockTests.cs

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

## 文件 12 TreeGraph.Api.Tests/Tests/OptionSetSoftDeleteTests.cs

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

## 文件 13 TreeGraph.Api.Tests/Tests/PatchEntityTests.cs

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

## 文件 14 TreeGraph.Api.Tests/Tests/QueryTests.cs

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

## 文件 15 TreeGraph.Api.Tests/Tests/ValidationTests.cs

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

## 文件 16 TreeGraph.Blazor.Tests/Components/CustomTableEditorTests.cs

```csharp
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Components.Shared;
using TreeGraph.Blazor.Services;
using TreeGraph.Blazor.Tests.TestDoubles;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests.Components;

/// <summary>
/// CustomTableEditor 组件测试。
///
/// 焦点：
///   - 加载后标题 chip / 行数 / 列布局
///   - IsUnique 跨行重复 → 出现"存在错误"chip + 错误文案
///   - 加载失败时的 MudAlert
///   - RefTableDefinitionId 缺失 → Snackbar 提示
///
/// 不测：具体控件（MudTextField 等）的键盘交互（依赖 JS）。
/// </summary>
public class CustomTableEditorTests : TestContext
{
    public CustomTableEditorTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ============================================================
    // 测试数据
    // ============================================================

    private static CustomTableDetailDto MakeTable(
        bool uniqueCertName = false,
        bool requiredCertName = false,
        JsonElement? certNameRule = null)
        => new(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "认证证书",
            Version: 1,
            DisplayOrder: 1,
            Columns: new[]
            {
                new CustomTableColumnDto(
                    ColumnId: "00000000-0000-0000-0000-000000000001",
                    ColumnName: "cert_name",
                    DisplayName: "证书名称",
                    DataType: "string",
                    RefCompositeTypeId: null,
                    IsRequired: requiredCertName,
                    IsSearchable: true,
                    IsSortable: false,
                    IsUnique: uniqueCertName,
                    DisplayOrder: 1,
                    DefaultValue: null,
                    AllowedValues: null,
                    ValidationRule: certNameRule),
                new CustomTableColumnDto(
                    ColumnId: "00000000-0000-0000-0000-000000000002",
                    ColumnName: "issuer",
                    DisplayName: "颁发机构",
                    DataType: "string",
                    RefCompositeTypeId: null,
                    IsRequired: false,
                    IsSearchable: true,
                    IsSortable: false,
                    IsUnique: false,
                    DisplayOrder: 2,
                    DefaultValue: null,
                    AllowedValues: null,
                    ValidationRule: null),
            });

    private static CustomTableValue MakeRows(params (string CertName, string Issuer)[] rows)
    {
        var result = new CustomTableValue { TableName = "certs" };
        for (int i = 0; i < rows.Length; i++)
        {
            result.Rows.Add(new CustomTableRowValue
            {
                RowId = $"00000000-0000-0000-0000-{(i + 1):D12}",
                RowOrder = i,
                Fields = new Dictionary<string, object?>
                {
                    ["cert_name"] = rows[i].CertName,
                    ["issuer"] = rows[i].Issuer
                }
            });
        }
        return result;
    }

    private IRenderedComponent<CustomTableEditor> RenderEditor(
        CustomTableDetailDto? table,
        CustomTableValue? value,
        string? refTableDefinitionId = "00000000-0000-0000-0000-000000000100")
    {
        var handler = new TestHttpMessageHandler();
        if (table is not null)
            handler.MapGet($"api/eav/metadata/custom-tables/{table.TableDefinitionId}", table);
        if (value is not null)
            handler.MapGet($"api/eav/Product/entities/1/tables/certs", value);

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);

        Services.AddSingleton(api);
        Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();

        return RenderComponent<CustomTableEditor>(p => p
            .Add(x => x.EntityType, "Product")
            .Add(x => x.EntityId, "1")
            .Add(x => x.AttributeName, "certs")
            .Add(x => x.TableName, "certs")
            .Add(x => x.DisplayName, "认证证书")
            .Add(x => x.RefTableDefinitionId, refTableDefinitionId));
    }

    // ============================================================
    // 基础渲染
    // ============================================================

    [Fact]
    public async Task LoadSuccess_RendersRowsCount()
    {
        var cut = RenderEditor(
            MakeTable(),
            MakeRows(("CE", "TUV"), ("FCC", "SGS")));

        // 等待 OnAfterRenderAsync → LoadAsync
        cut.WaitForState(() => cut.Markup.Contains("2 行"), TimeSpan.FromSeconds(3));

        Assert.Contains("2 行", cut.Markup);
        Assert.Contains("认证证书", cut.Markup);
    }

    [Fact]
    public async Task LoadSuccess_RendersColumnLabels()
    {
        var cut = RenderEditor(MakeTable(), MakeRows(("CE", "TUV")));

        cut.WaitForState(() => cut.Markup.Contains("证书名称"), TimeSpan.FromSeconds(3));

        Assert.Contains("证书名称", cut.Markup);
        Assert.Contains("颁发机构", cut.Markup);
    }

    // ============================================================
    // 已删除列：数据编辑器不显示（与管理页 CustomTables.razor 的语义分工）
    // ============================================================

    [Fact]
    public async Task LoadTable_ExcludesDeletedColumns()
    {
        // 后端 ToCustomTableDto 不过滤 IsDeleted（恢复 UI 需要），
        // 数据编辑器必须自行过滤：3 列中 legacy 已删除。
        var table = new CustomTableDetailDto(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "认证",
            Version: 1,
            DisplayOrder: 1,
            Columns: new[]
            {
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000001", "cert_name", "证书", "string",
                    null, true, true, false, false, 1, null, null, null,
                    IsDeleted: false),
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000002", "issuer", "机构", "string",
                    null, false, true, false, false, 2, null, null, null,
                    IsDeleted: false),
                new CustomTableColumnDto("00000000-0000-0000-0000-000000000003", "legacy", "旧列", "string",
                    null, false, false, false, false, 3, null, null, null,
                    IsDeleted: true),
            });

        // 行数据里同时含活动列与已删除列的键（历史数据）
        var value = new CustomTableValue { TableName = "certs" };
        value.Rows.Add(new CustomTableRowValue
        {
            RowId = "00000000-0000-0000-0000-000000000001",
            RowOrder = 0,
            Fields = new Dictionary<string, object?>
            {
                ["cert_name"] = "CE",
                ["issuer"] = "TUV",
                ["legacy"] = "old-data"
            }
        });

        var cut = RenderEditor(table, value);
        cut.WaitForState(() => cut.Markup.Contains("证书"), TimeSpan.FromSeconds(3));

        Assert.Contains("证书", cut.Markup);
        Assert.Contains("机构", cut.Markup);
        Assert.DoesNotContain("旧列", cut.Markup);
        // 已删除列的值也不得出现在任何控件中（防止随保存回传）
        Assert.DoesNotContain("old-data", cut.Markup);
    }

    // ============================================================
    // IsUnique 跨行重复 → 错误 chip
    // ============================================================

    [Fact]
    public async Task UniqueViolation_ShowsErrorChip()
    {
        var cut = RenderEditor(
            MakeTable(uniqueCertName: true),
            MakeRows(("CE", "TUV"), ("CE", "SGS")));   // 两行 cert_name 相同

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("存在错误", cut.Markup);
        Assert.Contains("在列中重复", cut.Markup);
    }

    [Fact]
    public async Task UniqueOk_NoErrorChip()
    {
        var cut = RenderEditor(
            MakeTable(uniqueCertName: true),
            MakeRows(("CE", "TUV"), ("FCC", "SGS")));

        cut.WaitForState(() => cut.Markup.Contains("2 行"), TimeSpan.FromSeconds(3));

        Assert.DoesNotContain("存在错误", cut.Markup);
        Assert.DoesNotContain("在列中重复", cut.Markup);
    }

    // ============================================================
    // 必填列 + 空值 → 存在错误
    // ============================================================

    [Fact]
    public async Task RequiredColumnWithEmptyValue_ShowsError()
    {
        var cut = RenderEditor(
            MakeTable(requiredCertName: true),
            MakeRows(("", "TUV")));   // cert_name 空

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("必填字段", cut.Markup);
    }

    // ============================================================
    // 列规则：maxLength 越界
    // ============================================================

    [Fact]
    public async Task MaxLengthViolation_ShowsError()
    {
        var rule = JsonDocument.Parse("""{"maxLength": 2}""").RootElement;
        var cut = RenderEditor(
            MakeTable(certNameRule: rule),
            MakeRows(("LONG_NAME", "TUV")));

        cut.WaitForState(
            () => cut.Markup.Contains("存在错误"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("长度不能超过 2", cut.Markup);
    }

    // ============================================================
    // RefTableDefinitionId 缺失
    // ============================================================

    [Fact]
    public async Task MissingRefTableDefinitionId_ShowsWarningAndNoRows()
    {
        var cut = RenderEditor(
            table: MakeTable(),
            value: MakeRows(("CE", "TUV")),
            refTableDefinitionId: null);

        cut.WaitForState(
            () => cut.Markup.Contains("无法加载表结构"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("无法加载表结构", cut.Markup);
    }

    // ============================================================
    // 表结构加载失败（404）→ MudAlert
    // ============================================================

    [Fact]
    public async Task TableDefLoad404_ShowsAlert()
    {
        // 只注册数据 URL，不注册表结构 URL → 表结构 GET 会 404
        var handler = new TestHttpMessageHandler();
        handler.MapGet("api/eav/Product/entities/1/tables/certs", MakeRows(("CE", "TUV")));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);

        Services.AddSingleton(api);
        Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();

        var cut = RenderComponent<CustomTableEditor>(p => p
            .Add(x => x.EntityType, "Product")
            .Add(x => x.EntityId, "1")
            .Add(x => x.AttributeName, "certs")
            .Add(x => x.TableName, "certs")
            .Add(x => x.DisplayName, "认证证书")
            .Add(x => x.RefTableDefinitionId, "00000000-0000-0000-0000-000000000100"));

        cut.WaitForState(
            () => cut.Markup.Contains("无法加载表结构"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("无法加载表结构", cut.Markup);
    }

    // ============================================================
    // 空表结构（无列）→ Info alert
    // ============================================================

    [Fact]
    public async Task TableWithNoColumns_ShowsInfo()
    {
        var emptyTable = new CustomTableDetailDto(
            TableDefinitionId: "00000000-0000-0000-0000-000000000100",
            EntityType: "Product",
            TableName: "certs",
            DisplayName: "空表",
            Version: 1,
            DisplayOrder: 1,
            Columns: Array.Empty<CustomTableColumnDto>());

        var cut = RenderEditor(emptyTable, MakeRows());

        cut.WaitForState(
            () => cut.Markup.Contains("没有定义任何列"),
            TimeSpan.FromSeconds(3));

        Assert.Contains("没有定义任何列", cut.Markup);
    }
}
```

## 文件 17 TreeGraph.Blazor.Tests/Components/QueryFilterBuilderTests.cs

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Components.Shared;
using TreeGraph.Blazor.Services;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests.Components;

/// <summary>
/// QueryFilterBuilder 组件测试。
///
/// 焦点：AddFilter / RemoveFilter / ClearAll 是否触发正确的状态与回调。
/// 不测 MudSelect 的下拉交互（依赖 JS，bUnit 不覆盖）。
/// </summary>
public class QueryFilterBuilderTests : TestContext
{
    public QueryFilterBuilderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static AttributeSchemaDto MakeAttr(
        string name, string dataType = "string", bool searchable = true)
        => new(
            AttributeName: name,
            DisplayName: name,
            DataType: dataType,
            IsRequired: false,
            IsSearchable: searchable,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: null);

    // ============================================================
    // 空状态
    // ============================================================

    [Fact]
    public void NoSupportedAttributes_RendersInfoAlert()
    {
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, Array.Empty<AttributeSchemaDto>()));

        Assert.Contains("没有可搜索的属性", cut.Markup);
    }

    [Fact]
    public void EmptyFilters_DoesNotRenderApplyButton()
    {
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("name")
            }));

        // 无 filters 时，只有「添加条件」，没有「应用查询 / 清空条件」
        Assert.Contains("添加条件", cut.Markup);
        Assert.DoesNotContain("应用查询", cut.Markup);
        Assert.DoesNotContain("清空条件", cut.Markup);
    }

    // ============================================================
    // AddFilter
    // ============================================================

    [Fact]
    public void AddFilter_AddsToFiltersCollection()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        // 找「添加条件」按钮并点击
        var addBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("添加条件"));
        addBtn.Click();

        Assert.Single(filters);
        // 默认属性名与默认运算符由 catalog 决定
        Assert.Equal("name", filters[0].AttributeName);
        Assert.False(string.IsNullOrEmpty(filters[0].Operator));
    }

    [Fact]
    public void AddFilter_WhenFiltersNonEmpty_RendersApplyAndClearButtons()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" }
        };

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        Assert.Contains("应用查询", cut.Markup);
        Assert.Contains("清空条件", cut.Markup);
    }

    // ============================================================
    // RemoveFilter
    // ============================================================

    [Fact]
    public async Task RemoveFilter_RemovesFromCollectionAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
        };
        var appliedCount = 0;

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        // ★ 修复：用 title 属性定位，不依赖 SVG 内部字符串
        var removeBtn = cut.FindAll("button")
            .First(b => b.GetAttribute("title") == "移除"
                     || b.GetAttribute("aria-label") == "移除");
        await cut.InvokeAsync(() => removeBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // ClearAll
    // ============================================================

    [Fact]
    public async Task ClearAll_RemovesAllAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
            new() { AttributeName = "name", Operator = "neq" }
        };
        var appliedCount = 0;

        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        var clearBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("清空条件"));
        await cut.InvokeAsync(() => clearBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // AddFilter 默认运算符来自 FilterOperatorCatalog
    // ============================================================

    [Fact]
    public void AddFilter_ForNumericAttribute_PicksNumericOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("price", dataType: "decimal")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("price", filters[0].AttributeName);
        // 数值属性首个运算符是 eq（catalog 里定义的第一项）
        Assert.Equal("eq", filters[0].Operator);
    }

    [Fact]
    public void AddFilter_ForBoolAttribute_PicksBoolOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = RenderComponent<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("active", dataType: "bool")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("eq", filters[0].Operator);
    }
}
```

## 文件 18 TreeGraph.Blazor.Tests/EavFieldValidatorTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Blazor.Services;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// EavFieldValidator 的完整校验链测试。
///
/// 焦点是 **EavFieldValidator 自身逻辑**（FieldValidationRules 已在
/// FieldValidationRulesTests 覆盖）：
///   - 必填检查 + single_choice 默认值放行
///   - int 拒绝小数（EavFieldValidator 独有）
///   - NumericInput 的 UnitId 约束（attr.Unit 为 null 时不允许指定）
///   - 组合类型递归
///   - date / time / json 的解析失败
///   - IsEmpty 对空数组的处理
/// </summary>
public class EavFieldValidatorTests
{
    private readonly EavFieldValidator _validator = new();

    // ============================================================
    // 构造助手
    // ============================================================

    private static AttributeSchemaDto Attr(
        string dataType,
        bool isRequired = false,
        JsonElement? rule = null,
        JsonElement? allowedValues = null,
        CompositeTypeSchemaDto? composite = null,
        UnitSchemaDto? unit = null,
        IReadOnlyList<UnitSchemaDto>? availableUnits = null,
        OptionSetSchemaDto? optionSet = null)
        => new(
            AttributeName: "field",
            DisplayName: "字段",
            DataType: dataType,
            IsRequired: isRequired,
            IsSearchable: true,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: allowedValues,
            ValidationRule: rule,
            CompositeType: composite,
            Unit: unit,
            AvailableUnits: availableUnits,
            OptionSet: optionSet);

    private static CompositeTypeSchemaDto Composite(
        params CompositeFieldSchemaDto[] fields)
        => new("TestType", fields);

    private static CompositeFieldSchemaDto Field(
        string name,
        string dataType,
        bool isRequired = false,
        bool isArray = false,
        JsonElement? rule = null,
        CompositeTypeSchemaDto? nested = null,
        OptionSetSchemaDto? optionSet = null)
        => new(
            FieldName: name,
            DisplayName: name,
            DataType: dataType,
            IsArray: isArray,
            IsRequired: isRequired,
            IsSearchable: true,
            DisplayOrder: 0,
            ValidationRule: rule,
            NestedType: nested,
            OptionSet: optionSet);

    private static UnitSchemaDto Unit(string symbol = "kg")
        => new(Guid.NewGuid(), "weight", symbol, symbol, IsBaseUnit: true);

    private static OptionSetSchemaDto OptionSet(
        params (string Value, bool IsDefault)[] items)
        => new(
            OptionSetId: "00000000-0000-0000-0000-000000000001",
            SetName: "test",
            DisplayName: "test",
            Items: items.Select((x, i) => new OptionItemSchemaDto(
                $"00000000-0000-0000-0000-{(i + 1):D12}", x.Value, x.Value, i, x.IsDefault)).ToList());

    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    // ============================================================
    // int 拒绝小数（EavFieldValidator 独有）
    // ============================================================

    [Fact]
    public void Int_WithDecimalValue_ReturnsError()
    {
        var errors = _validator.Validate(Attr("int"), 3.14m);
        Assert.Single(errors);
        Assert.Contains("int 类型不接受小数", errors[0].Message);
    }

    [Fact]
    public void Int_WithLongValue_Passes()
    {
        var errors = _validator.Validate(Attr("int"), 42L);
        Assert.Empty(errors);
    }

    [Fact]
    public void Int_WithStringDecimalRepresentation_ReturnsError()
    {
        var errors = _validator.Validate(Attr("int"), "3.14");
        Assert.Single(errors);
        Assert.Contains("int 类型不接受小数", errors[0].Message);
    }

    // ============================================================
    // 必填
    // ============================================================

    [Fact]
    public void Required_NullValue_ReturnsError()
    {
        var errors = _validator.Validate(Attr("string", isRequired: true), null);
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Required_EmptyString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("string", isRequired: true), "   ");
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Required_EmptyArray_ReturnsError()
    {
        var errors = _validator.Validate(
            Attr("string", isRequired: true),
            new List<object?>());
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    // ============================================================
    // single_choice 默认值放行
    // ============================================================

    [Fact]
    public void RequiredSingleChoice_WithDefaultOption_NullValue_Passes()
    {
        var optionSet = OptionSet(("a", true), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", isRequired: true, optionSet: optionSet),
            null);
        Assert.Empty(errors);
    }

    [Fact]
    public void RequiredSingleChoice_NoDefaultOption_NullValue_ReturnsError()
    {
        var optionSet = OptionSet(("a", false), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", isRequired: true, optionSet: optionSet),
            null);
        Assert.Single(errors);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void SingleChoice_InvalidValue_ReturnsError()
    {
        var optionSet = OptionSet(("a", false), ("b", false));
        var errors = _validator.Validate(
            Attr("single_choice", optionSet: optionSet),
            "c");
        Assert.Single(errors);
        Assert.Contains("不在选项集中", errors[0].Message);
    }

    // ============================================================
    // NumericInput 的 UnitId 约束（P0-5）
    // ============================================================

    [Fact]
    public void NumericInput_WhenAttrHasNoUnit_WithUnitId_ReturnsError()
    {
        var value = new NumericInput { Value = 5m, UnitId = Guid.NewGuid() };
        var errors = _validator.Validate(Attr("decimal"), value);
        Assert.Single(errors);
        Assert.Contains("未绑定基准单位", errors[0].Message);
    }

    [Fact]
    public void NumericInput_WhenAttrHasUnit_WithForeignUnitId_ReturnsError()
    {
        var baseUnit = Unit("kg");
        var foreignUnit = Unit("cm");
        var value = new NumericInput { Value = 5m, UnitId = foreignUnit.Id };

        var errors = _validator.Validate(
            Attr("decimal",
                unit: baseUnit,
                availableUnits: new[] { baseUnit }),
            value);

        Assert.Single(errors);
        Assert.Contains("不属于该属性的可用单位", errors[0].Message);
    }

    [Fact]
    public void NumericInput_WhenAttrHasUnit_WithMatchingUnitId_Passes()
    {
        var baseUnit = Unit("kg");
        var value = new NumericInput { Value = 5m, UnitId = baseUnit.Id };

        var errors = _validator.Validate(
            Attr("decimal",
                unit: baseUnit,
                availableUnits: new[] { baseUnit }),
            value);

        Assert.Empty(errors);
    }

    [Fact]
    public void NumericInput_BareDecimal_NoUnit_Passes()
    {
        var errors = _validator.Validate(Attr("decimal"), 5m);
        Assert.Empty(errors);
    }

    // ============================================================
    // 组合递归
    // ============================================================

    [Fact]
    public void Composite_ValueNotDictionary_ReturnsError()
    {
        var composite = Composite(
            Field("name", "string"));

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            "not-a-dictionary");

        Assert.Single(errors);
        Assert.Contains("期望字典结构", errors[0].Message);
    }

    [Fact]
    public void Composite_RequiredNestedField_Missing_ReturnsError()
    {
        var composite = Composite(
            Field("name", "string", isRequired: true));

        // ★ 修复：非空字典，仅缺目标字段
        // （空字典会被 IsEmpty 判定为"整个 composite 未提供"，走顶层必填路径）
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Single(errors);
        Assert.Contains("name", errors[0].Path);
        Assert.Contains("必填字段", errors[0].Message);
    }

    [Fact]
    public void Composite_NestedSingleChoiceWithDefault_Missing_Passes()
    {
        var optionSet = OptionSet(("x", true));
        var composite = Composite(
            Field("choice", "single_choice",
                isRequired: true, optionSet: optionSet));

        // ★ 修复：非空字典，缺 choice 字段；因选项集有默认值 → 放行
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Empty(errors);
    }

    [Fact]
    public void Composite_ArrayFieldWithRequiredElements_Missing_ReturnsError()
    {
        var composite = Composite(
            Field("items", "string", isRequired: true, isArray: true));

        // ★ 修复：非空字典，缺 items 字段
        var dict = new Dictionary<string, object?> { ["unrelated"] = "x" };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Single(errors);
        Assert.Contains("items", errors[0].Path);
    }

    [Fact]
    public void Composite_ArrayFieldWithElements_ValidatesEach()
    {
        var composite = Composite(
            Field("items", "string", isArray: true));

        var dict = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "a", "b", "c" }
        };

        var errors = _validator.Validate(
            Attr("composite", composite: composite),
            dict);

        Assert.Empty(errors);
    }

    // ============================================================
    // date / time / json 解析
    // ============================================================

    [Fact]
    public void Date_InvalidString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("date"), "not-a-date");
        Assert.Single(errors);
        Assert.Contains("日期格式错误", errors[0].Message);
    }

    [Fact]
    public void Date_ValidString_Passes()
    {
        var errors = _validator.Validate(Attr("date"), "2026-10-02");
        Assert.Empty(errors);
    }

    [Fact]
    public void Time_InvalidString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("time"), "not-a-time");
        Assert.Single(errors);
        Assert.Contains("时间格式错误", errors[0].Message);
    }

    [Fact]
    public void Json_InvalidJsonString_ReturnsError()
    {
        var errors = _validator.Validate(Attr("json"), "{ not valid json");
        Assert.Single(errors);
        Assert.Contains("JSON 格式错误", errors[0].Message);
    }

    [Fact]
    public void Json_ValidJsonString_Passes()
    {
        var errors = _validator.Validate(Attr("json"), """{"a": 1}""");
        Assert.Empty(errors);
    }

    // ============================================================
    // 顶层规则（走 FieldValidationRules）
    // ============================================================

    [Fact]
    public void Numeric_BelowMin_ReturnsError()
    {
        var rule = Json("""{"min": 5}""");
        var errors = _validator.Validate(Attr("decimal", rule: rule), 3m);
        Assert.Single(errors);
        Assert.Contains("不能小于 5", errors[0].Message);
    }

    [Fact]
    public void String_RegexMismatch_ReturnsError()
    {
        var rule = Json("""{"regex": "^\\d{4}$", "message": "必须是4位数字"}""");
        var errors = _validator.Validate(Attr("string", rule: rule), "12");
        Assert.Single(errors);
        Assert.Equal("必须是4位数字", errors[0].Message);
    }

    // ============================================================
    // ValidateJsonText（独立入口）
    // ============================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateJsonText_NullOrWhitespace_ReturnsNull(string? text)
    {
        Assert.Null(_validator.ValidateJsonText(text));
    }

    [Fact]
    public void ValidateJsonText_ValidJson_ReturnsNull()
    {
        Assert.Null(_validator.ValidateJsonText("""{"a": 1}"""));
    }

    [Fact]
    public void ValidateJsonText_InvalidJson_ReturnsMessage()
    {
        var error = _validator.ValidateJsonText("{not-json");
        Assert.NotNull(error);
        Assert.Contains("JSON 格式错误", error);
    }
}
```

## 文件 19 TreeGraph.Blazor.Tests/FieldValidationRulesTests.cs

```csharp
using System.Text.Json;
using TreeGraph.Blazor.Services;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// FieldValidationRules 纯静态规则引擎的单测。
///
/// 覆盖：
///   - ValidateNumeric：min / max / 两者 / 空 rule / 非对象 rule
///   - ValidateString：AllowedValues / minLength / maxLength / regex（默认+自定义 message）
///   - ValidateDate：minDate / maxDate
///   - ValidateTime：minTime / maxTime
///   - TryGetDecimal：number / string / 非法输入
///
/// 关键约束：非法正则必须被吞掉（不抛异常），与 EavFieldValidator 的容错语义一致。
/// </summary>
public class FieldValidationRulesTests
{
    // ============================================================
    // ValidateNumeric
    // ============================================================

    [Fact]
    public void ValidateNumeric_NullRule_ReturnsEmpty()
    {
        var errors = FieldValidationRules.ValidateNumeric(5m, null);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_NonObjectRule_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("\"not an object\"").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(5m, rule);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_BelowMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"min": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(3m, rule);
        Assert.Single(errors);
        Assert.Contains("不能小于 5", errors[0]);
    }

    [Fact]
    public void ValidateNumeric_AboveMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"max": 10}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(15m, rule);
        Assert.Single(errors);
        Assert.Contains("不能大于 10", errors[0]);
    }

    [Fact]
    public void ValidateNumeric_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"min": 0, "max": 100}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(50m, rule);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNumeric_AtBoundary_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"min": 5, "max": 10}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateNumeric(5m, rule));
        Assert.Empty(FieldValidationRules.ValidateNumeric(10m, rule));
    }

    [Fact]
    public void ValidateNumeric_InvertedRange_ValueInBetween_ReturnsTwoErrors()
    {
        // min=10, max=5（元数据配置错误）；value=7 同时越界两侧
        var rule = JsonDocument.Parse("""{"min": 10, "max": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateNumeric(7m, rule);
        Assert.Equal(2, errors.Count);
    }

    // ============================================================
    // ValidateString — AllowedValues
    // ============================================================

    [Fact]
    public void ValidateString_NotInAllowed_ReturnsError()
    {
        var allowed = JsonDocument.Parse("""["a","b","c"]""").RootElement;
        var errors = FieldValidationRules.ValidateString("x", null, allowed);
        Assert.Single(errors);
        Assert.Contains("值必须是以下之一", errors[0]);
    }

    [Fact]
    public void ValidateString_InAllowed_ReturnsEmpty()
    {
        var allowed = JsonDocument.Parse("""["a","b"]""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("a", null, allowed));
    }

    [Fact]
    public void ValidateString_EmptyAllowedArray_NoConstraint()
    {
        var allowed = JsonDocument.Parse("[]").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("anything", null, allowed));
    }

    // ============================================================
    // ValidateString — minLength / maxLength
    // ============================================================

    [Fact]
    public void ValidateString_TooShort_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minLength": 5}""").RootElement;
        var errors = FieldValidationRules.ValidateString("abc", rule, null);
        Assert.Single(errors);
        Assert.Contains("长度不能少于 5", errors[0]);
    }

    [Fact]
    public void ValidateString_TooLong_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxLength": 3}""").RootElement;
        var errors = FieldValidationRules.ValidateString("abcd", rule, null);
        Assert.Single(errors);
        Assert.Contains("长度不能超过 3", errors[0]);
    }

    [Fact]
    public void ValidateString_ExactLength_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"minLength": 3, "maxLength": 3}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("abc", rule, null));
    }

    // ============================================================
    // ValidateString — regex
    // ============================================================

    [Fact]
    public void ValidateString_RegexFails_ReturnsDefaultMessage()
    {
        var rule = JsonDocument.Parse("""{"regex": "^\\d{4}$"}""").RootElement;
        var errors = FieldValidationRules.ValidateString("12", rule, null);
        Assert.Single(errors);
        Assert.Contains("格式不正确", errors[0]);
    }

    [Fact]
    public void ValidateString_RegexFails_ReturnsCustomMessage()
    {
        var rule = JsonDocument.Parse(
            """{"regex": "^\\d{4}$", "message": "必须是4位数字"}""").RootElement;
        var errors = FieldValidationRules.ValidateString("12", rule, null);
        Assert.Single(errors);
        Assert.Equal("必须是4位数字", errors[0]);
    }

    [Fact]
    public void ValidateString_RegexPasses_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse("""{"regex": "^\\d{4}$"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("2026", rule, null));
    }

    [Fact]
    public void ValidateString_InvalidRegex_SilentlyIgnored()
    {
        // 非法正则不能抛异常（用户手输规则可能写错）
        var rule = JsonDocument.Parse("""{"regex": "[unclosed"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateString("x", rule, null));
    }

    // ============================================================
    // ValidateDate
    // ============================================================

    [Fact]
    public void ValidateDate_BeforeMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minDate": "2026-01-01"}""").RootElement;
        var errors = FieldValidationRules.ValidateDate(new DateOnly(2025, 12, 31), rule);
        Assert.Single(errors);
        Assert.Contains("日期不能早于", errors[0]);
    }

    [Fact]
    public void ValidateDate_AfterMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxDate": "2026-12-31"}""").RootElement;
        var errors = FieldValidationRules.ValidateDate(new DateOnly(2027, 1, 1), rule);
        Assert.Single(errors);
        Assert.Contains("日期不能晚于", errors[0]);
    }

    [Fact]
    public void ValidateDate_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse(
            """{"minDate": "2026-01-01", "maxDate": "2026-12-31"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateDate(new DateOnly(2026, 6, 15), rule));
    }

    // ============================================================
    // ValidateTime
    // ============================================================

    [Fact]
    public void ValidateTime_BeforeMin_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"minTime": "09:00:00"}""").RootElement;
        var errors = FieldValidationRules.ValidateTime(new TimeOnly(8, 0), rule);
        Assert.Single(errors);
        Assert.Contains("时间不能早于", errors[0]);
    }

    [Fact]
    public void ValidateTime_AfterMax_ReturnsError()
    {
        var rule = JsonDocument.Parse("""{"maxTime": "17:00:00"}""").RootElement;
        var errors = FieldValidationRules.ValidateTime(new TimeOnly(18, 0), rule);
        Assert.Single(errors);
        Assert.Contains("时间不能晚于", errors[0]);
    }

    [Fact]
    public void ValidateTime_InRange_ReturnsEmpty()
    {
        var rule = JsonDocument.Parse(
            """{"minTime": "09:00:00", "maxTime": "17:00:00"}""").RootElement;
        Assert.Empty(FieldValidationRules.ValidateTime(new TimeOnly(12, 0), rule));
    }

    // ============================================================
    // TryGetDecimal
    // ============================================================

    [Theory]
    [InlineData("42", 42)]
    [InlineData("3.14", 3.14)]
    [InlineData("-5", -5)]
    [InlineData("0", 0)]
    public void TryGetDecimal_FromNumericString_Parses(string input, double expected)
    {
        var elem = JsonDocument.Parse($"\"{input}\"").RootElement;
        Assert.True(FieldValidationRules.TryGetDecimal(elem, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void TryGetDecimal_FromNumber_Parses()
    {
        var elem = JsonDocument.Parse("3.14").RootElement;
        Assert.True(FieldValidationRules.TryGetDecimal(elem, out var value));
        Assert.Equal(3.14m, value);
    }

    [Fact]
    public void TryGetDecimal_FromInvalidString_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("\"not-a-number\"").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }

    [Fact]
    public void TryGetDecimal_FromObject_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("""{"x":1}""").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }

    [Fact]
    public void TryGetDecimal_FromNull_ReturnsFalse()
    {
        var elem = JsonDocument.Parse("null").RootElement;
        Assert.False(FieldValidationRules.TryGetDecimal(elem, out _));
    }
}
```

## 文件 20 TreeGraph.Blazor.Tests/StartupTests.cs

```csharp
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// 启动级 smoke 测试：在测试宿主里跑完整的 Program.cs pipeline，
/// 触发所有 <c>ValidateOnStart</c> 的 Options 校验。
///
/// 目的：防止配置错误（如 EavApiClient 的 Resilience 参数）潜伏到运行期。
/// 背景：曾因 <c>MaxRetryAttempts = 0</c> + <c>SamplingDuration</c> 不满足
/// 约束，Aspire 编排启动时抛 OptionsValidationException，
/// 但 dotnet build 通过、单元测试也通过。
///
/// 首次构造 <see cref="BlazorAppFactory"/> 时，工厂会调用
/// <c>IHost.StartAsync()</c>，此时所有 <c>ValidateOnStart</c> 的 Options 会被校验；
/// 任何非法配置都会在测试中直接抛异常。
/// </summary>
public class StartupTests : IClassFixture<BlazorAppFactory>
{
    private readonly BlazorAppFactory _factory;

    public StartupTests(BlazorAppFactory factory) => _factory = factory;

    /// <summary>
    /// 宿主启动不抛异常即通过。工厂构造时已触发校验。
    /// </summary>
    [Fact]
    public void Host_Starts_WithValidOptions()
    {
        Assert.NotNull(_factory.Server);
    }

    /// <summary>
    /// 打通 HTTP pipeline：请求 Aspire 默认端点的 /alive。
    /// 覆盖：路由注册 + 中间件顺序 + 无意外依赖解析失败。
    /// </summary>
    [Fact]
    public async Task Health_Endpoint_Is_Reachable()
    {
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/alive");

        // MapDefaultEndpoints 在 Development 环境注册 /alive；
        // 若注册未生效（环境判断出错），会 404，我们把它视为异常。
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

/// <summary>
/// 测试宿主工厂：覆盖外部依赖，让 Program.cs 完整 pipeline 能在无 Aspire、
/// 无真实 DB、无 OTEL collector 的环境下启动。
/// </summary>
public sealed class BlazorAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 与生产启动保持一致的环境（影响 MapDefaultEndpoints 是否注册 /alive）
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 假连接串：不会真连（Blazor 不注册 DbContext，测试也不触达 DB 路径），
                // 仅作为占位配置保留。
                ["ConnectionStrings:TreeGraphDb"] =
                    "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",

                // AddServiceDefaults 检测到此为空 → 不启用 OTLP exporter，
                // 避免测试环境尝试连接 collector。
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",

                // 关闭 Aspire 服务发现探测（若存在），避免无谓的 DNS 查询。
                // EavApiClient 的 BaseAddress 只是被设置，测试不真实发请求。
                ["Aspire:ServiceDiscovery:Enabled"] = "false",
            });
        });
    }
}
```

## 文件 21 TreeGraph.Blazor.Tests/TestDoubles/TestHttpMessageHandler.cs

```csharp
using System.Net;
using System.Text;
using System.Text.Json;

namespace TreeGraph.Blazor.Tests.TestDoubles;

/// <summary>
/// 轻量 HTTP handler：按 URL 前缀匹配返回预置 JSON。
/// 用于 bUnit 测试中替代真实网络。
///
/// 用法：
///   var handler = new TestHttpMessageHandler()
///       .Map("GET api/eav/metadata/custom-tables/1", dto)
///       .Map("GET api/eav/Product/entities/1/tables/certs", value);
///
/// 未匹配的请求返回 404，便于测试时立即暴露遗漏。
/// </summary>
public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string Method, string UrlKey, string Json)> _routes = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public TestHttpMessageHandler Map(string method, string urlKey, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        _routes.Add((method.ToUpperInvariant(), urlKey, json));
        return this;
    }

    public TestHttpMessageHandler MapGet(string urlKey, object payload)
        => Map("GET", urlKey, payload);

    public TestHttpMessageHandler MapPut(string urlKey, object payload)
        => Map("PUT", urlKey, payload);

    public TestHttpMessageHandler MapPost(string urlKey, object payload)
        => Map("POST", urlKey, payload);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method.ToUpperInvariant();
        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (m, key, json) in _routes)
        {
            if (m != method) continue;
            if (!url.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $"No route for {method} {url}",
                Encoding.UTF8, "text/plain")
        });
    }
}
```

# 不存在于磁盘的条目

- TreeGraph.Shared.Tests/TreeGraph.Shared.Tests.csproj
