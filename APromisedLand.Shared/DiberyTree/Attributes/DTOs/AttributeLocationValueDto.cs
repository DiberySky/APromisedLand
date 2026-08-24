namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

/// <summary>定位属性值返回 DTO。</summary>
public class AttributeLocationValueDto
{
    public string ValueId { get; set; } = null!;
    public string NodeId { get; set; } = null!;
    public string DefinitionId { get; set; } = null!;
    public string? DefinitionName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>新增定位值 DTO。</summary>
public class AddAttributeLocationValueDto
{
    public string AttributeDefinitionId { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>更新定位值 DTO。</summary>
public class UpdateAttributeLocationValueDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
