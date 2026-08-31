// using APromisedLand.Shared.DiberyTree.Attributes.Enums;
// using APromisedLand.Shared.DiberyTree.Attributes.Models;
// using APromisedLand.Shared.DiberyTree.Models;
//
// namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;
//
// public class AttributeDefinition
// {
//     public string Id { get; set; } = null!;
//     public string Name { get; set; } = null!;
//     public AttributeTypeEnum AttributeType { get; set; }
//     public int? MaxLength { get; set; }  
//     public int? Lines { get; set; }
//     public int? Precision { get; set; }
//     public int? Scale { get; set; }
//     public string? UnitId { get; set; }
//     public UnitTree? Unit { get; set; }
//
//     // ===== 动态表专用（列定义时启用）=====
//     public string? ParentId { get; set; }
//     public bool HasDate { get; set; }
//     public bool HasTime { get; set; }
//     public bool HasRowNo { get; set; }
//     public int Order { get; set; }
//     public bool IsRequired { get; set; }
//     public string? DefaultValue { get; set; }
//
//     public Models.AttributeDefinition Definition()
//     {
//         return new Models.AttributeDefinition(AttributeType)
//         {
//             Id = Id,
//             Name = Name,
//             AttributeTypeId = AttributeType.ToAttributeTypeId(),
//             Lines = Lines,
//             MaxLength = MaxLength ?? 30,
//             Precision = Precision,
//             Scale = Scale,
//             UnitId = Unit?.Id,
//             Unit = Unit,
//             ParentId = ParentId,
//             HasDate = HasDate,
//             HasTime = HasTime,
//             HasRowNo = HasRowNo,
//             Order = Order,  
//             IsRequired = IsRequired,
//             DefaultValue = DefaultValue,
//         };
//     }
// }