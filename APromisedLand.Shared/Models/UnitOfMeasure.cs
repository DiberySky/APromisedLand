namespace APromisedLand.Shared.Models;

public class UnitOfMeasure
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// EF Core 种子数据使用的纯扁平数据（不包含导航属性）
    /// </summary>
    public static List<UnitOfMeasure> SeedData()
    {
        return new List<UnitOfMeasure>
        {
            new UnitOfMeasure
            {
                Id = "7d9b6f2e-1a3c-48f7-9b8d-5e6a1c2d3f40", Name = "千克", Symbol = "kg", Description = "公斤",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "a3c8e0f5-6b4d-4a1e-9f2c-8d7b1e5a6f30", Name = "克", Symbol = "g", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "b2d4f6a8-c0e2-4f1b-8a3d-1c5e7b9d2f44", Name = "毫克", Symbol = "mg", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "c1e3a5f7-d9b1-4e8c-9d2a-5b7c0e1f3a62", Name = "吨", Symbol = "t", Description = "公吨",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "f8a2c4e6-1b3d-4f5c-8a9b-7d0e1f2a3b55", Name = "升", Symbol = "L", Description = "公升",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "e7d9b1f3-2c4a-4e6b-8d5c-0f1a2b3c4d66", Name = "毫升", Symbol = "mL", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "d6f8a0b2-3c4e-4a6d-8b1c-9d0e1f2a3b77", Name = "米", Symbol = "m", Description = "公尺",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "c5e7b9d1-4f0a-4c2e-8a3b-7b8d9e0f1c88", Name = "厘米", Symbol = "cm", Description = "公分",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "b4d6f8a0-5e1b-4d3c-9a2b-6c7d8e9f0a99", Name = "毫米", Symbol = "mm", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "a3c5e7f9-6b1d-4f4a-8c2b-5d6e7f8a9b00", Name = "千米", Symbol = "km", Description = "公里",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "92b4d6f8-7c0e-4a5b-9d1a-4c5d6e7f8a11", Name = "个", Symbol = "pcs", Description = "件、只",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "81a3c5e7-8d1f-4b6c-8e2a-3b4c5d6e7f22", Name = "箱", Symbol = "box", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "70f2a4b6-9e0c-4d7a-8f3b-2a3b4c5d6e33", Name = "包", Symbol = "bag", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "6e1f3a5b-0d8e-4c8a-9d4b-1a2b3c4d5e44", Name = "瓶", Symbol = "bottle", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "5d0e2f4a-1c9f-4b9c-8e5a-0f1a2b3c4d55", Name = "罐", Symbol = "can", Description = "听",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "4c9f1e3b-2d0a-4c8e-9f6a-9e0f1a2b3c66", Name = "双", Symbol = "pair", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "3b8e0d2c-3f1b-4d9f-8a7b-8d9e0f1a2b77", Name = "套", Symbol = "set", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "2a7d9f1b-4e2c-4e0a-9b8c-7c8d9e0f1a88", Name = "卷", Symbol = "roll", Description = null,
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "1c6e8a0d-5f3d-4f1b-8c9d-6b7c8d9e0f99", Name = "平方米", Symbol = "m²", Description = "平方公尺",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "0b5d7f9e-6a4e-4f2c-9d0e-5a6b7c8d9e00", Name = "立方米", Symbol = "m³", Description = "立方公尺",
                IsActive = true
            },

            // ========== 供电领域常用单位 ==========
            new UnitOfMeasure
            {
                Id = "f9a1b3c5-7d0e-4a2f-8b4d-3c5e7f9a1b11", Name = "伏特", Symbol = "V", Description = "电压单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "e8b2c4d6-1f3a-4b5c-9d6e-2d4f6a8b0c22", Name = "千伏", Symbol = "kV", Description = "千伏特",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "d7c3d5e7-2a4b-4c6d-8e7f-1c3e5a7b9d33", Name = "安培", Symbol = "A", Description = "电流单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "c6d4e6f8-3b5c-4d7e-9f8a-0b2d4f6a8c44", Name = "毫安", Symbol = "mA", Description = "毫安培",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "b5e5f7a9-4c6d-4e8f-8a9b-9a1c3e5b7d55", Name = "瓦特", Symbol = "W", Description = "功率单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "a4f6a8b0-5d7e-4f9a-9b0c-8b2d4f6a8c66", Name = "千瓦", Symbol = "kW", Description = "千瓦特",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "93e7b9c1-6e8f-4a0b-8c1d-7c3e5a7b9d77", Name = "兆瓦", Symbol = "MW", Description = "兆瓦特",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "82d8c0d2-7f9a-4b1c-9d2e-6d4f6a8b0c88", Name = "千瓦时", Symbol = "kWh", Description = "度，电能单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "71c9d1e3-8a0b-4c2d-8e3f-5e5f7a9b1c99", Name = "兆瓦时", Symbol = "MWh", Description = "千度，电能单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "60b0e2f4-9b1c-4d3e-9f4a-4f6a8b0c2d00", Name = "焦耳", Symbol = "J", Description = "能量单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "5f1a3b5c-0c2d-4e4f-8a5b-3e5f7a9b1c11", Name = "赫兹", Symbol = "Hz", Description = "频率单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "4e2b4c6d-1d3e-4f5a-9b6c-2d4f6a8b0c22", Name = "千赫", Symbol = "kHz", Description = "千赫兹",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "3d3c5d7e-2e4f-4a6b-8c7d-1c3e5a7b9d33", Name = "欧姆", Symbol = "Ω", Description = "电阻单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "2c4d6e8f-3f5a-4b7c-9d8e-0b2d4f6a8c44", Name = "伏安", Symbol = "VA", Description = "视在功率单位",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "1b5e7f9a-4a6b-4c8d-8e9f-9a1c3e5b7d55", Name = "乏", Symbol = "var", Description = "无功功率单位",
                IsActive = true
            },

            // ========== 温度常用单位 ==========
            new UnitOfMeasure
            {
                Id = "8f2e6a1d-3b4c-45f7-9e1a-2c3d4e5f6a70", Name = "摄氏度", Symbol = "°C", Description = "摄氏温标",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "9c4a7b2e-5d6e-41a8-bf3c-0d1e2f3a4b81", Name = "华氏度", Symbol = "°F", Description = "华氏温标",
                IsActive = true
            },
            new UnitOfMeasure
            {
                Id = "0e1d6c3b-7f8a-42c9-9d4e-1f2a3b4c5d92", Name = "开尔文", Symbol = "K", Description = "热力学温标",
                IsActive = true
            }
        };
    }
}