using TreeGraph.Blazor.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// EavDataTypes 目录不变量：13 个类型的层级/引用/宿主规则是唯一权威，
/// 这些测试锁住「容器不进容器」「single_choice 不进表列」等核心规则，
/// 防止后续改动把规则改散。
/// </summary>
public class EavDataTypeCatalogTests
{
    private static readonly string[] Scalars =
    {
        EavDataTypes.String, EavDataTypes.Int, EavDataTypes.Decimal, EavDataTypes.Bool,
        EavDataTypes.Datetime, EavDataTypes.Date, EavDataTypes.Time, EavDataTypes.Json
    };

    [Fact]
    public void Catalog_CoversAllThirteenConstants()
    {
        var constants = typeof(EavDataTypes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        Assert.Equal(13, constants.Length);
        Assert.Equal(13, EavDataTypes.Catalog.Count);
        Assert.Equal(13, EavDataTypes.All.Count);
        foreach (var c in constants)
            Assert.True(EavDataTypes.Catalog.ContainsKey(c), $"目录缺少常量: {c}");
    }

    [Fact]
    public void Scalars_CanGoEverywhere()
    {
        foreach (var s in Scalars)
        {
            var info = EavDataTypes.InfoOf(s);
            Assert.Equal(EavTypeLayer.Scalar, info.Layer);
            Assert.Equal(EavRefKind.None, info.Ref);
            Assert.Equal(
                EavHost.TopLevel | EavHost.CompositeField | EavHost.TableColumn,
                info.Hosts);
        }
    }

    [Fact]
    public void Containers_AreTopLevelOnly_AndRequireRef()
    {
        var composite = EavDataTypes.InfoOf(EavDataTypes.Composite);
        Assert.Equal(EavTypeLayer.Container, composite.Layer);
        Assert.Equal(EavRefKind.CompositeType, composite.Ref);
        Assert.Equal(EavHost.TopLevel, composite.Hosts);

        var table = EavDataTypes.InfoOf(EavDataTypes.Table);
        Assert.Equal(EavTypeLayer.Container, table.Layer);
        Assert.Equal(EavRefKind.TableDefinition, table.Ref);
        Assert.Equal(EavHost.TopLevel, table.Hosts);

        // 容器不进容器
        Assert.DoesNotContain(EavDataTypes.Composite, EavDataTypes.CompositeFieldAllowed);
        Assert.DoesNotContain(EavDataTypes.Composite, EavDataTypes.TableColumnAllowed);
        Assert.DoesNotContain(EavDataTypes.Table, EavDataTypes.CompositeFieldAllowed);
        Assert.DoesNotContain(EavDataTypes.Table, EavDataTypes.TableColumnAllowed);
    }

    [Fact]
    public void SpecialLeaves_AreTopLevelOnly()
    {
        foreach (var s in new[] { EavDataTypes.File, EavDataTypes.Location })
        {
            var info = EavDataTypes.InfoOf(s);
            Assert.Equal(EavTypeLayer.SpecialLeaf, info.Layer);
            Assert.Equal(EavHost.TopLevel, info.Hosts);
            Assert.DoesNotContain(s, EavDataTypes.CompositeFieldAllowed);
            Assert.DoesNotContain(s, EavDataTypes.TableColumnAllowed);
        }
    }

    [Fact]
    public void SingleChoice_IsRefLeaf_NotAllowedInTableColumns()
    {
        var info = EavDataTypes.InfoOf(EavDataTypes.SingleChoice);
        Assert.Equal(EavTypeLayer.RefLeaf, info.Layer);
        Assert.Equal(EavRefKind.OptionSet, info.Ref);

        Assert.Contains(EavDataTypes.SingleChoice, EavDataTypes.CompositeFieldAllowed);
        Assert.DoesNotContain(EavDataTypes.SingleChoice, EavDataTypes.TableColumnAllowed);
    }

    [Fact]
    public void DerivedSets_HaveExpectedSizes()
    {
        Assert.Equal(9, EavDataTypes.CompositeFieldAllowed.Count); // 8 标量 + single_choice
        Assert.Equal(8, EavDataTypes.TableColumnAllowed.Count);    // 8 标量
    }

    [Fact]
    public void IsContainer_OnlyForCompositeAndTable()
    {
        Assert.True(EavDataTypes.IsContainer(EavDataTypes.Composite));
        Assert.True(EavDataTypes.IsContainer(EavDataTypes.Table));
        Assert.False(EavDataTypes.IsContainer(EavDataTypes.String));
        Assert.False(EavDataTypes.IsContainer("nonexistent"));
    }

    [Fact]
    public void OptionsFor_MatchesCatalogHosts_AndHasLabels()
    {
        Assert.Equal(13, EavDataTypeLabels.OptionsFor(EavHost.TopLevel).Count);
        Assert.Equal(9, EavDataTypeLabels.OptionsFor(EavHost.CompositeField).Count);
        Assert.Equal(8, EavDataTypeLabels.OptionsFor(EavHost.TableColumn).Count);

        foreach (var (value, label) in EavDataTypeLabels.OptionsFor(EavHost.TopLevel))
        {
            Assert.False(string.IsNullOrWhiteSpace(label), $"类型 {value} 缺少中文标签");
        }
    }
}
