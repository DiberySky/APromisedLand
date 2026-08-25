using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeLocationDto
{
    public string LocationId { get; set; } = null!;
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}