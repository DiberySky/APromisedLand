using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

public class UnitTree : ITreeNodeBase<UnitTree>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public bool CanHaveChildren { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public UnitTree? Parent { get; set; }

    public string Text()
    {
        var text = string.IsNullOrEmpty(Abbreviation) ? Name : $"{Name} 【{Abbreviation}】";
        return text;
    }

    public static List<UnitTree> SeedData()
    {
        var nodes = new List<UnitTree>();
        int sortOrder = 0;

        // ========== 分类节点（固定 GUID） ==========
        const string CURRENCY_ID = "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f";
        const string LENGTH_ID = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";
        const string MASS_ID = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e";
        const string TIME_ID = "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f";
        const string TEMP_ID = "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a";
        const string CURRENT_ID = "e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b";
        const string VOLTAGE_ID = "f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c";
        const string POWER_ID = "a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d";
        const string AREA_ID = "b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e";
        const string VOLUME_ID = "c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f";
        const string SPEED_ID = "d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a";
        const string PRESSURE_ID = "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b";
        const string ENERGY_ID = "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c";
        const string FREQUENCY_ID = "a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d";
        const string ANGLE_ID = "b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e";

        // 根节点（允许有子项，且有子项）
        nodes.Add(new UnitTree
        {
            Id = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            Name = "计量单位",
            Abbreviation = "",
            Description = "",
            ParentId = null,
            CanHaveChildren = true,
            SortOrder = 0,
            HasChildren = true
        });

        // 分类节点（允许有子项，且有子项）
        nodes.Add(new UnitTree
        {
            Id = CURRENCY_ID,
            Name = "货币",
            Abbreviation = "",
            Description = "货币计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = LENGTH_ID,
            Name = "长度",
            Abbreviation = "",
            Description = "长度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = MASS_ID,
            Name = "质量",
            Abbreviation = "",
            Description = "质量计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = TIME_ID,
            Name = "时间",
            Abbreviation = "",
            Description = "时间计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = TEMP_ID,
            Name = "温度",
            Abbreviation = "",
            Description = "温度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = CURRENT_ID,
            Name = "电流",
            Abbreviation = "",
            Description = "电流计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = VOLTAGE_ID,
            Name = "电压",
            Abbreviation = "",
            Description = "电压计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = POWER_ID,
            Name = "功率",
            Abbreviation = "",
            Description = "功率计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = AREA_ID,
            Name = "面积",
            Abbreviation = "",
            Description = "面积计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = VOLUME_ID,
            Name = "体积",
            Abbreviation = "",
            Description = "体积计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = SPEED_ID,
            Name = "速度",
            Abbreviation = "",
            Description = "速度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = PRESSURE_ID,
            Name = "压力",
            Abbreviation = "",
            Description = "压力计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = ENERGY_ID,
            Name = "能量",
            Abbreviation = "",
            Description = "能量计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = FREQUENCY_ID,
            Name = "频率",
            Abbreviation = "",
            Description = "频率计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });
        nodes.Add(new UnitTree
        {
            Id = ANGLE_ID,
            Name = "角度",
            Abbreviation = "",
            Description = "角度计量单位",
            ParentId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B",
            CanHaveChildren = true,
            SortOrder = sortOrder++,
            HasChildren = true
        });

        // ========== 单位节点（不允许有子项，且当前无子项） ==========
        // 货币单位
        nodes.Add(new UnitTree
        {
            Id = "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c", // 人民币
            Name = "元",
            Abbreviation = "CNY",
            Description = "人民币",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e6b7c8d9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", // 美元
            Name = "美元",
            Abbreviation = "USD",
            Description = "美元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5c6d7e8-f9a0-4b1c-2d3e-4f5a6b7c8d9e", // 欧元
            Name = "欧元",
            Abbreviation = "EUR",
            Description = "欧元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c4d5e6f7-a8b9-4c0d-1e2f-3a4b5c6d7e8f", // 英镑
            Name = "英镑",
            Abbreviation = "GBP",
            Description = "英镑",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b3c4d5e6-f7a8-4b9c-0d1e-2f3a4b5c6d7e", // 日元
            Name = "日元",
            Abbreviation = "JPY",
            Description = "日元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
// 新增港元和澳元
        nodes.Add(new UnitTree
        {
            Id = "a5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", // 港元
            Name = "港元",
            Abbreviation = "HKD",
            Description = "港元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", // 澳元
            Name = "澳元",
            Abbreviation = "AUD",
            Description = "澳元",
            ParentId = CURRENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        // 长度单位
        nodes.Add(new UnitTree
        {
            Id = "c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c",
            Name = "米",
            Abbreviation = "m",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
            Name = "千米",
            Abbreviation = "km",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
            Name = "厘米",
            Abbreviation = "cm",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f",
            Name = "毫米",
            Abbreviation = "mm",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a",
            Name = "英里",
            Abbreviation = "mi",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b",
            Name = "码",
            Abbreviation = "yd",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c",
            Name = "英尺",
            Abbreviation = "ft",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d",
            Name = "英寸",
            Abbreviation = "in",
            Description = "",
            ParentId = LENGTH_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 质量单位
        nodes.Add(new UnitTree
        {
            Id = "e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e",
            Name = "千克",
            Abbreviation = "kg",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f",
            Name = "克",
            Abbreviation = "g",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a",
            Name = "毫克",
            Abbreviation = "mg",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
            Name = "吨",
            Abbreviation = "t",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c",
            Name = "磅",
            Abbreviation = "lb",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d",
            Name = "盎司",
            Abbreviation = "oz",
            Description = "",
            ParentId = MASS_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 时间单位
        nodes.Add(new UnitTree
        {
            Id = "e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e",
            Name = "秒",
            Abbreviation = "s",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f",
            Name = "分钟",
            Abbreviation = "min",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a",
            Name = "小时",
            Abbreviation = "h",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b",
            Name = "天",
            Abbreviation = "d",
            Description = "",
            ParentId = TIME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 温度单位
        nodes.Add(new UnitTree
        {
            Id = "c8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c",
            Name = "摄氏度",
            Abbreviation = "°C",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d",
            Name = "华氏度",
            Abbreviation = "°F",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e",
            Name = "开尔文",
            Abbreviation = "K",
            Description = "",
            ParentId = TEMP_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 电流单位
        nodes.Add(new UnitTree
        {
            Id = "f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
            Name = "安培",
            Abbreviation = "A",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a",
            Name = "毫安",
            Abbreviation = "mA",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b",
            Name = "微安",
            Abbreviation = "µA",
            Description = "",
            ParentId = CURRENT_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 电压单位
        nodes.Add(new UnitTree
        {
            Id = "c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c",
            Name = "伏特",
            Abbreviation = "V",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d",
            Name = "千伏",
            Abbreviation = "kV",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e",
            Name = "毫伏",
            Abbreviation = "mV",
            Description = "",
            ParentId = VOLTAGE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 功率单位
        nodes.Add(new UnitTree
        {
            Id = "f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f",
            Name = "瓦特",
            Abbreviation = "W",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a",
            Name = "千瓦",
            Abbreviation = "kW",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b",
            Name = "兆瓦",
            Abbreviation = "MW",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a00be966-be2e-484e-92b6-9706494ac775",
            Name = "马力",
            Abbreviation = "hp",
            Description = "",
            ParentId = POWER_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 面积单位
        nodes.Add(new UnitTree
        {
            Id = "e88b04db-40ac-4bb7-b420-1f3b37180673",
            Name = "平方米",
            Abbreviation = "m²",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "0d7ebe17-93ae-4e4c-92f4-063a124cd181",
            Name = "平方公里",
            Abbreviation = "km²",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56",
            Name = "公顷",
            Abbreviation = "ha",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "fefa26a5-d608-411c-b637-469a886e558c",
            Name = "亩",
            Abbreviation = "亩",
            Description = "",
            ParentId = AREA_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 体积单位
        nodes.Add(new UnitTree
        {
            Id = "2d21c35a-4251-479e-b814-060b2fc84445",
            Name = "立方米",
            Abbreviation = "m³",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "7f0af6a9-ba1a-469c-b967-e32afe43cad2",
            Name = "升",
            Abbreviation = "L",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "780e7a01-350d-45ec-b963-b36a996de614",
            Name = "毫升",
            Abbreviation = "mL",
            Description = "",
            ParentId = VOLUME_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 速度单位
        nodes.Add(new UnitTree
        {
            Id = "4cbec89d-3f52-4db3-9ab0-faeeb841ffbf",
            Name = "米/秒",
            Abbreviation = "m/s",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "64e918fb-ee9d-45c7-b35a-2a55f5a5fe62",
            Name = "千米/小时",
            Abbreviation = "km/h",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "405ae7a3-8a13-479d-bc1a-6f9d3c15e521",
            Name = "英里/小时",
            Abbreviation = "mph",
            Description = "",
            ParentId = SPEED_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 压力单位
        nodes.Add(new UnitTree
        {
            Id = "221d3c45-911f-4e94-9c3e-c13e6f2bcc76",
            Name = "帕斯卡",
            Abbreviation = "Pa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "883a9940-84ec-4daa-8448-609461b984ea",
            Name = "千帕",
            Abbreviation = "kPa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "d5306eb8-324f-4088-a867-6fbc7141fd59",
            Name = "兆帕",
            Abbreviation = "MPa",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "8981bd9a-bd99-4f4c-b8af-038975b799be",
            Name = "巴",
            Abbreviation = "bar",
            Description = "",
            ParentId = PRESSURE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 能量单位
        nodes.Add(new UnitTree
        {
            Id = "5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81",
            Name = "焦耳",
            Abbreviation = "J",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "279a6b18-6d01-4437-b95b-0480ca7adc98",
            Name = "千焦",
            Abbreviation = "kJ",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "e3c1f025-3b2b-461a-a5a1-015cb4e3fe38",
            Name = "千瓦时",
            Abbreviation = "kWh",
            Description = "",
            ParentId = ENERGY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 频率单位
        nodes.Add(new UnitTree
        {
            Id = "5e880060-9410-40d7-bcb4-545ccd0c1bb6",
            Name = "赫兹",
            Abbreviation = "Hz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "1a80ed3b-1b36-4d8b-b80b-3070dbc7979d",
            Name = "千赫",
            Abbreviation = "kHz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "41572712-95dd-4caf-b316-e1b924bc57c3",
            Name = "兆赫",
            Abbreviation = "MHz",
            Description = "",
            ParentId = FREQUENCY_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        // 角度单位
        nodes.Add(new UnitTree
        {
            Id = "ed1b66d2-454b-453b-9d43-12605dffa456",
            Name = "度",
            Abbreviation = "°",
            Description = "",
            ParentId = ANGLE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });
        nodes.Add(new UnitTree
        {
            Id = "3d9088a3-7283-4f8f-b995-b193a57a6c2a",
            Name = "弧度",
            Abbreviation = "rad",
            Description = "",
            ParentId = ANGLE_ID,
            CanHaveChildren = false,
            SortOrder = sortOrder++,
            HasChildren = false
        });

        return nodes;
    }
}