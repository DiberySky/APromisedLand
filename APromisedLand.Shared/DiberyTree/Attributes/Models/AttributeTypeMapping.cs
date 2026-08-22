using APromisedLand.Shared.DiberyTree.Attributes.Enums;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models
{
    public static class AttributeTypeMapping
    {
        // 直接公开静态只读字典（也可用 IReadOnlyDictionary 接口）
        public static readonly IReadOnlyDictionary<AttributeTypeEnum, string> EnumToId =
            new Dictionary<AttributeTypeEnum, string>
            {
                { AttributeTypeEnum.文本, "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a" },
                { AttributeTypeEnum.整数, "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d" },
                { AttributeTypeEnum.小数, "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e" },
                { AttributeTypeEnum.日期, "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f" },
                { AttributeTypeEnum.时间, "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a" },
                { AttributeTypeEnum.日期时间, "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b" },
                { AttributeTypeEnum.文件, "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c" },
                { AttributeTypeEnum.定位, "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d" },
                { AttributeTypeEnum.表格, "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e" }
            };

        // 反向映射（如果需要），可以直接基于上述字典反转
        public static readonly IReadOnlyDictionary<string, AttributeTypeEnum> IdToEnum =
            new Dictionary<string, AttributeTypeEnum>(
                // 通过循环构建，避免手动重复
                new Dictionary<string, AttributeTypeEnum>
                {
                    { "3f8f6d3a-9c2b-4d8f-9a6e-5b7c8d9e0f1a", AttributeTypeEnum.文本 },
                    { "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", AttributeTypeEnum.整数 },
                    { "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", AttributeTypeEnum.小数 },
                    { "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", AttributeTypeEnum.日期 },
                    { "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", AttributeTypeEnum.时间 },
                    { "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", AttributeTypeEnum.日期时间 },
                    { "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", AttributeTypeEnum.文件 },
                    { "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", AttributeTypeEnum.定位 },
                    { "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", AttributeTypeEnum.表格 }
                }
            );

        // 也可以提供扩展方法风格的辅助方法（可选）
        public static string ToAttributeTypeId(this AttributeTypeEnum type) => EnumToId[type];
        public static AttributeTypeEnum ToAttributeTypeEnum(this string id) => IdToEnum[id];
        public static string DefaultId() => EnumToId[AttributeTypeEnum.文本];
        
        public static AttributeTypeEnum DefaultType() => IdToEnum[DefaultId()];
        public static List<AttributeTypeEnum> All => EnumToId.Keys.ToList();
        
        public static string? ToName(this AttributeTypeEnum type) => Enum.GetName(type);
        public static string? ToAttributeTypeName(this string id) => Enum.GetName(IdToEnum[id]);
        
    }
}