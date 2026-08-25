namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class LocationAttributeDefValue : AttributeValueBase
{
    /// <summary>
    /// 定位名称，用于定位 Id。
    /// </summary>
    public string Value { get; set; } = null!;
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}