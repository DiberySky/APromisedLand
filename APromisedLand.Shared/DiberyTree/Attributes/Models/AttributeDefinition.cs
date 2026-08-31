using System.ComponentModel.DataAnnotations.Schema;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class AttributeDefinition
{
    public AttributeDefinition()
    {
        AttributeTypeId = AttributeTypeEnum.文本.ToAttributeTypeId();
    }
    
    public AttributeDefinition(AttributeTypeEnum typeEnum)
    {
        AttributeTypeId = typeEnum.ToAttributeTypeId();
    }
    
    public string Id { get; set; } = Guid.NewGuid().ToString(); // 由种子数据提供固定 GUID
    public string Name { get; set; } = "名称";

    // 外键改为 string，匹配 AttributeType.Id
    public string AttributeTypeId { get; set; } = null!;
    // [NotMapped]
    // public AttributeType AttributeType { get; set; } = null!;

    // 文本专用
    public int? Lines { get; set; }

    // 文本最大长度（字符数）
    public int MaxLength { get; set; } = 30;

    // 数字专用
    public int? Precision { get; set; }
    public int? Scale { get; set; }

    // 单位（可选），引用 UnitTree.Id（string）
    public string? UnitId { get; set; }
    public UnitTree? Unit { get; set; }

    // ===== 动态表专用（AttributeType=表 时启用）=====
    // 自引用：列定义指向所属的"表定义"；表定义自身为 null
    public string? ParentId { get; set; }
    public AttributeDefinition? Parent { get; set; }

    public bool HasDate { get; set; }
    public bool HasTime { get; set; }
    public bool HasRowNo { get; set; }
    
    // 列在表内的显示顺序
    public int Order { get; set; }

    // 该列是否必填
    public bool IsRequired { get; set; }

    // 列默认值（字符串形式，按列类型解析）
    public string? DefaultValue { get; set; }

    public string TypeName
        => AttributeTypeId.ToAttributeTypeEnum().ToString(); 
    public AttributeTypeEnum TypeEnum
        => AttributeTypeId.ToAttributeTypeEnum(); 
    
//     public DTOs.AttributeDefinition ToDto()
// {
//         return new DTOs.AttributeDefinition
//         {
//             Id = Id,
//             Name = Name,
//             AttributeType = AttributeTypeId.ToAttributeTypeEnum(),
//             Lines = Lines,
//             MaxLength = MaxLength,
//             Precision = Precision,
//             Scale = Scale,
//             UnitId = UnitId,
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
    // ---------- 种子数据 ----------
    public static List<AttributeDefinition> SeedData()
    {
        // ===== 引用 AttributeType 的固定 GUID（必须与 AttributeType.SeedData() 完全一致） =====
        var textTypeId = "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a";
        var intTypeId = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";
        var decimalTypeId = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e";
        var dateTypeId = "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f";
        var timeTypeId = "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a";
        var dateTimeTypeId = "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b";
        var fileTypeId = "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c";
        var locationTypeId = "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d";
        var tableTypeId = "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e";

        // ===== 引用 UnitTree 中常用单位的固定 GUID（从 UnitTree.SeedData() 中提取） =====
        // 长度单位
        const string UNIT_METER = "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c"; // 米
        const string UNIT_CENTIMETER = "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"; // 厘米
        // 质量单位
        const string UNIT_KILOGRAM = "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e"; // 千克

        // 温度、电流、电压等其他单位可自行引用，此处仅示例常用

        return new List<AttributeDefinition>
        {
            // ---------- 文本类型 ----------
            new(AttributeTypeEnum.文本)
            {
                Id = "b1c2d3e4-f5a6-4b7c-8d9e-0f1a2b3c4d5e",
                Name = "名称",
                AttributeTypeId = textTypeId,
                Lines = 1,
                MaxLength = 50
            }, // 最大 50 字符

            new(AttributeTypeEnum.文本)
            {
                Id = "c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f",
                Name = "描述",
                AttributeTypeId = textTypeId,
                Lines = 5,
                MaxLength = 500
            }, // 最大 500 字符

            new(AttributeTypeEnum.文本)
            {
                Id = "d3e4f5a6-b7c8-4d9e-0f1a-2b3c4d5e6f7a",
                Name = "备注",
                AttributeTypeId = textTypeId,
                Lines = 10,
                MaxLength = 1000
            }, // 最大 1000 字符
            // ---------- 整数类型 ----------
            new(AttributeTypeEnum.整数)
            {
                Id = "e4f5a6b7-c8d9-4e0f-1a2b-3c4d5e6f7a8b", Name = "数量", AttributeTypeId = intTypeId,
                UnitId = UNIT_METER
            }, // 单位：米
            new(AttributeTypeEnum.整数) { Id = "f5a6b7c8-d9e0-4f1a-2b3c-4d5e6f7a8b9c", Name = "等级", AttributeTypeId = intTypeId },

            // ---------- 小数类型 ----------
            new(AttributeTypeEnum.小数)
            {
                Id = "a6b7c8d9-e0f1-4a2b-3c4d-5e6f7a8b9c0d", Name = "价格", AttributeTypeId = decimalTypeId,
                Precision = 18, Scale = 2
            },
            new(AttributeTypeEnum.小数)
            {
                Id = "b7c8d9e0-f1a2-4b3c-4d5e-6f7a8b9c0d1e", Name = "重量", AttributeTypeId = decimalTypeId,
                Precision = 10, Scale = 3, UnitId = UNIT_KILOGRAM
            },
            new(AttributeTypeEnum.小数)
            {
                Id = "c8d9e0f1-a2b3-4c4d-5e6f-7a8b9c0d1e2f", Name = "长度", AttributeTypeId = decimalTypeId,
                Precision = 8, Scale = 2, UnitId = UNIT_CENTIMETER
            },

            // ---------- 日期类型（多个种子，满足你的需求） ----------
            new(AttributeTypeEnum.日期) { Id = "d9e0f1a2-b3c4-4d5e-6f7a-8b9c0d1e2f3a", Name = "生产日期", AttributeTypeId = dateTypeId },
            new(AttributeTypeEnum.日期) { Id = "e0f1a2b3-c4d5-4e6f-7a8b-9c0d1e2f3a4b", Name = "试验日期", AttributeTypeId = dateTypeId },
            new(AttributeTypeEnum.日期) { Id = "f1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c", Name = "出厂日期", AttributeTypeId = dateTypeId },

            // ---------- 时间类型 ----------
            new(AttributeTypeEnum.时间) { Id = "a2b3c4d5-e6f7-4a8b-9c0d-1e2f3a4b5c6d", Name = "开始时间", AttributeTypeId = timeTypeId },
            new(AttributeTypeEnum.时间) { Id = "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", Name = "结束时间", AttributeTypeId = timeTypeId },

            // ---------- 日期时间类型 ----------
            new(AttributeTypeEnum.日期时间) { Id = "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", Name = "创建时间", AttributeTypeId = dateTimeTypeId },
            new(AttributeTypeEnum.日期时间) { Id = "d5e6f7a8-b9c0-4d1e-2f3a-4b5c6d7e8f9a", Name = "更新时间", AttributeTypeId = dateTimeTypeId },

            // ---------- 文件类型 ----------
            new(AttributeTypeEnum.文件) { Id = "e6f7a8b9-c0d1-4e2f-3a4b-5c6d7e8f9a0b", Name = "附件", AttributeTypeId = fileTypeId },
            new(AttributeTypeEnum.文件) { Id = "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", Name = "图片", AttributeTypeId = fileTypeId },

            // ---------- 定位类型 ----------
            new(AttributeTypeEnum.定位) { Id = "a8b9c0d1-e2f3-4a4b-5c6d-7e8f9a0b1c2d", Name = "位置坐标", 
                AttributeTypeId = locationTypeId },

            // ---------- 动态表类型 ----------
            // 表定义：规格表（ParentId = null，自身不存单值，仅作为"虚拟表"容器）
            new(AttributeTypeEnum.表格)
            {
                Id = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5e", Name = "规格表",
                AttributeTypeId = tableTypeId
            },
            // 表「规格表」的列定义（ParentId 指向表定义）
            new(AttributeTypeEnum.文本)
            {
                Id = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6f", Name = "规格-材质",
                AttributeTypeId = textTypeId, 
                ParentId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5e",
                Lines = 1,
                Order = 1, IsRequired = true, MaxLength = 50
            },
            new(AttributeTypeEnum.小数)
            {
                Id = "3c4d5e6f-7a8b-4c9d-0e1f-2a3b4c5d6e7a", Name = "规格-长度",
                AttributeTypeId = decimalTypeId, ParentId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5e",
                Order = 2, IsRequired = false, Precision = 8, Scale = 2, UnitId = UNIT_CENTIMETER
            },
            
            // ---------- 动态记录类型 ----------
            // 表定义：规格表（ParentId = null，自身不存单值，仅作为"虚拟表"容器）
            new(AttributeTypeEnum.表格)
            {
                Id = "0C1E0FB8-B731-4F38-9379-A96B9F13FC1F", Name = "生长日记",
                AttributeTypeId = tableTypeId, HasDate = true
            },
            // 表「生长记录」的列定义（ParentId 指向表定义）
            // new()
            // {
            //     Id = "2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6f", Name = "规格-材质",
            //     AttributeTypeId = textTypeId, 
            //     ParentId = "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5e",
            //     Lines = 1,
            //     Order = 1, IsRequired = true, MaxLength = 50
            // },
            new(AttributeTypeEnum.小数)
            {
                Id = "97868637-556F-433F-BD31-E9DED37A5FE8", Name = "高度",
                AttributeTypeId = decimalTypeId, ParentId = "0C1E0FB8-B731-4F38-9379-A96B9F13FC1F",
                Order = 2, IsRequired = false, Precision = 8, Scale = 2, UnitId = UNIT_CENTIMETER
            },
        };
    }
}