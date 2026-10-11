using System.Text.Json;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// CompositeValueService 的序列化/反序列化单测。
/// 不触 DB：用内存 stub 提供 type / option set。
/// 单位仅作字符串记录（unit_trees 节点 Id），不做任何换算。
/// </summary>
public class CompositeValueServiceTests
{
    private readonly InMemoryCompositeTypeCache _compositeCache = new();
    private readonly InMemoryOptionSetCache _optionSetCache = new();
    private readonly CompositeValueService _service;

    // 测试类型 ID（GUID 字符串）
    private const string SpecsTypeId = "00000000-0000-0000-0000-000000000100";
    private const string BrandTypeId = "00000000-0000-0000-0000-000000000101";
    private const string NoUnitTypeId = "00000000-0000-0000-0000-000000000200";

    // 单位树节点 Id（仅作记录）
    private const string Kg = "unit-kg";
    private const string G = "unit-g";

    public CompositeValueServiceTests()
    {
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
                        UnitId = Kg, DisplayOrder = 2 },
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

        var validator = new EavValidationService(_compositeCache, _optionSetCache);
        _service = new CompositeValueService(_compositeCache, validator);
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
    // Serialize — 带单位 decimal（单位仅作记录，不换算）
    // ============================================================

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputIsBareNumber_OutputsBareNumber()
    {
        var v = MakeValue("Specs", ("weight", 5m));
        var doc = _service.Serialize(v, SpecsTypeId);

        Assert.Equal(JsonValueKind.Number, Root(doc).GetProperty("weight").ValueKind);
        Assert.Equal(5m, Root(doc).GetProperty("weight").GetDecimal());
    }

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputHasUnit_OutputsValueAsIsWithUnitId()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(1000m, G)));
        var doc = _service.Serialize(v, SpecsTypeId);

        var weight = Root(doc).GetProperty("weight");
        Assert.Equal(JsonValueKind.Object, weight.ValueKind);
        Assert.Equal(1000m, weight.GetProperty("value").GetDecimal());
        Assert.Equal(G, weight.GetProperty("unitId").GetString());
    }

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputIsNumericValueWithFieldUnit_OutputsObject()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(3m, Kg)));
        var doc = _service.Serialize(v, SpecsTypeId);

        var weight = Root(doc).GetProperty("weight");
        Assert.Equal(JsonValueKind.Object, weight.ValueKind);
        Assert.Equal(3m, weight.GetProperty("value").GetDecimal());
        Assert.Equal(Kg, weight.GetProperty("unitId").GetString());
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
    public void Deserialize_WithUnitObject_RestoresValueAndUnitAsIs()
    {
        var json = $$"""
        {
            "weight": { "value": 1, "unitId": "{{G}}" }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId);

        var weight = result["weight"];
        Assert.IsType<NumericValue>(weight);
        var nv = (NumericValue)weight!;
        Assert.Equal(1m, nv.Value);
        Assert.Equal(G, nv.UnitId);
    }

    [Fact]
    public void Deserialize_BareNumberWithUnitField_UsesFieldUnit()
    {
        var json = """{"weight": 5}""";

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(5m, nv.Value);
        Assert.Equal(Kg, nv.UnitId);
    }

    [Fact]
    public void Deserialize_UnitObjectWithoutUnitId_FallsBackToFieldUnit()
    {
        var json = """{"weight": { "value": 2 }}""";

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(2m, nv.Value);
        Assert.Equal(Kg, nv.UnitId);
    }

    // ============================================================
    // 往返
    // ============================================================

    [Fact]
    public void RoundTrip_WithUnit_PreservesOriginalValueAndUnit()
    {
        var v = MakeValue("Specs", ("weight", new NumericValue(1500m, G)));

        var doc = _service.Serialize(v, SpecsTypeId);

        var stored = Root(doc).GetProperty("weight");
        Assert.Equal(1500m, stored.GetProperty("value").GetDecimal());

        var result = _service.Deserialize(doc, SpecsTypeId);
        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(1500m, nv.Value);
        Assert.Equal(G, nv.UnitId);
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
