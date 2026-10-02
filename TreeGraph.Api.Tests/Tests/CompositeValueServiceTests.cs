using System.Text.Json;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// CompositeValueService 的序列化/反序列化单测。
///
/// 焦点：
///   - 组合内带单位 decimal 的 `{ value, unitId }` 序列化
///   - 无单位 decimal 的裸数值序列化
///   - originalUnits=true 时的单位还原
///   - 未知 unitId 的降级
///   - 数组字段 / 嵌套组合递归
///
/// 不触 DB：用内存 stub 提供 type / unit / option set。
/// </summary>
public class CompositeValueServiceTests
{
    private readonly InMemoryCompositeTypeCache _compositeCache = new();
    private readonly InMemoryUnitCache _unitCache = new();
    private readonly InMemoryOptionSetCache _optionSetCache = new();
    private readonly UnitConverter _converter;
    private readonly CompositeValueService _service;

    // 测试类型 ID
    private const long SpecsTypeId = 100;
    private const long BrandTypeId = 101;

    // 测试单位
    private Guid _kg;
    private Guid _g;
    private Guid _cm;   // 不同分类

    public CompositeValueServiceTests()
    {
        _converter = new UnitConverter(_unitCache);

        // 构造单位
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

        // 构造组合类型 Specs：{ color, weight(kg), brand(嵌套 Brand) }
        var brandType = new CompositeTypeDefinition
        {
            CompositeTypeId = BrandTypeId,
            TypeName = "Brand",
            DisplayName = "品牌",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = 11, FieldName = "name", DataType = EavDataTypes.String, DisplayOrder = 1 },
                new() { FieldId = 12, FieldName = "origin", DataType = EavDataTypes.String, DisplayOrder = 2 }
            }
        };
        _compositeCache.Add(brandType);

        var specsType = new CompositeTypeDefinition
        {
            CompositeTypeId = SpecsTypeId,
            TypeName = "Specs",
            DisplayName = "规格",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = 1, FieldName = "color", DataType = EavDataTypes.String, DisplayOrder = 1 },
                new() { FieldId = 2, FieldName = "weight", DataType = EavDataTypes.Decimal,
                        UnitId = _kg, DisplayOrder = 2 },
                new() { FieldId = 3, FieldName = "brand", DataType = EavDataTypes.Composite,
                        RefCompositeTypeId = BrandTypeId, DisplayOrder = 3 }
            }
        };
        _compositeCache.Add(specsType);

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
        // 临时把 weight 的 UnitId 去掉，构造独立 type
        var tempType = new CompositeTypeDefinition
        {
            CompositeTypeId = 200,
            TypeName = "NoUnit",
            Fields = new List<CompositeFieldDefinition>
            {
                new() { FieldId = 20, FieldName = "ratio",
                        DataType = EavDataTypes.Decimal }
            }
        };
        _compositeCache.Add(tempType);

        var v = MakeValue("NoUnit", ("ratio", 1.5m));
        var doc = _service.Serialize(v, 200);

        Assert.Equal(JsonValueKind.Number, Root(doc).GetProperty("ratio").ValueKind);
        Assert.Equal(1.5m, Root(doc).GetProperty("ratio").GetDecimal());
    }

    // ============================================================
    // Serialize — 带单位 decimal → { value, unitId }
    // ============================================================

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputIsBaseUnit_OutputsBareNumber()
    {
        // 输入用基准单位，且没有 unitId 引用 → 裸数值
        var v = MakeValue("Specs", ("weight", 5m));
        var doc = _service.Serialize(v, SpecsTypeId);

        Assert.Equal(JsonValueKind.Number, Root(doc).GetProperty("weight").ValueKind);
        Assert.Equal(5m, Root(doc).GetProperty("weight").GetDecimal());
    }

    [Fact]
    public void Serialize_DecimalWithUnit_WhenInputHasUnit_OutputsObjectWithNormalizedValue()
    {
        // 输入 1000 克 → 归一化到 1 千克
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
    // Deserialize — 无 originalUnits → 基准单位
    // ============================================================

    [Fact]
    public void Deserialize_WithUnitObject_DefaultReturnsBaseUnitValue()
    {
        // JSON: { weight: { value: 1, unitId: <g> } } — 存储已归一化到 kg 的值
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
        Assert.Equal(_kg, nv.UnitId);   // 无 originalUnits → 返回基准单位 kg
    }

    // ============================================================
    // Deserialize — originalUnits=true → 还原
    // ============================================================

    [Fact]
    public void Deserialize_WithUnitObject_OriginalUnits_RestoresInputUnit()
    {
        // 存储 1 kg（基准），原始输入是克
        var json = $$"""
        {
            "weight": { "value": 1, "unitId": "{{_g}}" }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(1000m, nv.Value);   // 1 kg → 1000 g
        Assert.Equal(_g, nv.UnitId);
    }

    [Fact]
    public void Deserialize_BareNumberWithUnitField_DefaultsToBaseUnit()
    {
        // 无 unitId 引用（旧数据兼容）→ 按基准单位
        var json = """{"weight": 5}""";

        using var doc = JsonDocument.Parse(json);
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);

        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(5m, nv.Value);
        Assert.Equal(_kg, nv.UnitId);
    }

    // ============================================================
    // Deserialize — 未知 unitId → 降级到基准单位
    // ============================================================

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
        Assert.Equal(_kg, nv.UnitId);   // 无法还原时退回基准单位
    }

    // ============================================================
    // 往返一致性
    // ============================================================

    [Fact]
    public void RoundTrip_WithUnit_PreservesOriginalValue()
    {
        // 输入 1500 克
        var v = MakeValue("Specs", ("weight", new NumericValue(1500m, _g)));

        var doc = _service.Serialize(v, SpecsTypeId);

        // 存储后 JSON 里的 value 应为 1.5 kg
        var stored = Root(doc).GetProperty("weight");
        Assert.Equal(1.5m, stored.GetProperty("value").GetDecimal());

        // 反序列化 originalUnits=true → 应还原为 1500 克
        var result = _service.Deserialize(doc, SpecsTypeId, originalUnits: true);
        var nv = (NumericValue)result["weight"]!;
        Assert.Equal(1500m, nv.Value);
        Assert.Equal(_g, nv.UnitId);
    }

    // ============================================================
    // 嵌套组合往返
    // ============================================================

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
    // 缺失字段的处理
    // ============================================================

    [Fact]
    public void Deserialize_MissingField_LeavesItUnset()
    {
        var json = """{"color": "blue"}""";   // 无 weight / brand
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
