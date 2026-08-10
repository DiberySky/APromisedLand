using APromisedLand.Shared.DiberyTree.Attributes.Enums;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class AttributeType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();    // 由种子数据提供
    public string Name { get; set; } = nameof(AttributeTypeEnum.文本);
    public string? Description { get; set; }
    public AttributeTypeEnum SystemType { get; set; } = AttributeTypeEnum.文本;

    public ICollection<AttributeDefinition> Definitions { get; set; } = new List<AttributeDefinition>();
    
    public static List<AttributeType> SeedData()
    {
        return
        [
            new AttributeType
            {
                Id = "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", Name = "文本", Description = "用于存储短文本、备注、名称等单行或多行文字信息",
                SystemType = AttributeTypeEnum.文本
            },
            new AttributeType
            {
                Id = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", Name = "整数", Description = "用于存储整数值，如数量、计数、等级等",
                SystemType = AttributeTypeEnum.整数
            },
            new AttributeType
            {
                Id = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", Name = "小数", Description = "用于存储带小数的数值，如价格、重量等",
                SystemType = AttributeTypeEnum.小数
            },
            new AttributeType
            {
                Id = "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", Name = "日期", Description = "用于存储日期（不含时间）",
                SystemType = AttributeTypeEnum.日期
            },
            new AttributeType
            {
                Id = "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", Name = "时间", Description = "用于存储时间（不含日期）",
                SystemType = AttributeTypeEnum.时间
            },
            new AttributeType
            {
                Id = "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", Name = "日期时间", Description = "用于存储精确的日期和时间",
                SystemType = AttributeTypeEnum.日期时间
            },
            new AttributeType
            {
                Id = "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", Name = "文件", Description = "用于上传和存储文件，记录文件路径、名称等",
                SystemType = AttributeTypeEnum.文件
            },
            new AttributeType
            {
                Id = "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", Name = "定位", Description = "用于存储地理位置信息（经度、纬度）",
                SystemType = AttributeTypeEnum.定位
            }
        ];
    }
}