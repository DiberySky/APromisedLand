using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

public class UnitTree : ITreeNodeBase<UnitTree>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    
    public string Abbreviation  { get; set; }  = string.Empty;
    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public UnitTree? Parent { get; set; }
    public string Text()
    {
        return Name;
    }

    public static List<UnitTree> SeedData()
{
    var nodes = new List<UnitTree>();
    int sortOrder = 0;

    // 1. 定义所有分类节点并存储其 ID
    var lengthId = Guid.NewGuid().ToString();
    var massId = Guid.NewGuid().ToString();
    var timeId = Guid.NewGuid().ToString();
    var tempId = Guid.NewGuid().ToString();
    var currentId = Guid.NewGuid().ToString();
    var voltageId = Guid.NewGuid().ToString();
    var powerId = Guid.NewGuid().ToString();
    var areaId = Guid.NewGuid().ToString();
    var volumeId = Guid.NewGuid().ToString();
    var speedId = Guid.NewGuid().ToString();
    var pressureId = Guid.NewGuid().ToString();
    var energyId = Guid.NewGuid().ToString();
    var frequencyId = Guid.NewGuid().ToString();
    var angleId = Guid.NewGuid().ToString();

    // 添加分类节点
    nodes.Add(new UnitTree
    {
        Id = lengthId,
        Name = "长度",
        Abbreviation = "",
        Description = "长度计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = massId,
        Name = "质量",
        Abbreviation = "",
        Description = "质量计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = timeId,
        Name = "时间",
        Abbreviation = "",
        Description = "时间计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = tempId,
        Name = "温度",
        Abbreviation = "",
        Description = "温度计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = currentId,
        Name = "电流",
        Abbreviation = "",
        Description = "电流计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = voltageId,
        Name = "电压",
        Abbreviation = "",
        Description = "电压计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = powerId,
        Name = "功率",
        Abbreviation = "",
        Description = "功率计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = areaId,
        Name = "面积",
        Abbreviation = "",
        Description = "面积计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = volumeId,
        Name = "体积",
        Abbreviation = "",
        Description = "体积计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = speedId,
        Name = "速度",
        Abbreviation = "",
        Description = "速度计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = pressureId,
        Name = "压力",
        Abbreviation = "",
        Description = "压力计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = energyId,
        Name = "能量",
        Abbreviation = "",
        Description = "能量计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = frequencyId,
        Name = "频率",
        Abbreviation = "",
        Description = "频率计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });
    nodes.Add(new UnitTree
    {
        Id = angleId,
        Name = "角度",
        Abbreviation = "",
        Description = "角度计量单位",
        ParentId = null,
        IsActive = true,
        SortOrder = sortOrder++,
        HasChildren = true,
        Parent = null
    });

    // 2. 定义所有单位节点
    // 长度单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "米", Abbreviation = "m", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千米", Abbreviation = "km", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "厘米", Abbreviation = "cm", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "毫米", Abbreviation = "mm", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "英里", Abbreviation = "mi", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "码", Abbreviation = "yd", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "英尺", Abbreviation = "ft", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "英寸", Abbreviation = "in", Description = "", ParentId = lengthId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 质量单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千克", Abbreviation = "kg", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "克", Abbreviation = "g", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "毫克", Abbreviation = "mg", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "吨", Abbreviation = "t", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "磅", Abbreviation = "lb", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "盎司", Abbreviation = "oz", Description = "", ParentId = massId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 时间单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "秒", Abbreviation = "s", Description = "", ParentId = timeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "分钟", Abbreviation = "min", Description = "", ParentId = timeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "小时", Abbreviation = "h", Description = "", ParentId = timeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "天", Abbreviation = "d", Description = "", ParentId = timeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 温度单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "摄氏度", Abbreviation = "°C", Description = "", ParentId = tempId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "华氏度", Abbreviation = "°F", Description = "", ParentId = tempId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "开尔文", Abbreviation = "K", Description = "", ParentId = tempId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 电流单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "安培", Abbreviation = "A", Description = "", ParentId = currentId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "毫安", Abbreviation = "mA", Description = "", ParentId = currentId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "微安", Abbreviation = "µA", Description = "", ParentId = currentId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 电压单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "伏特", Abbreviation = "V", Description = "", ParentId = voltageId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千伏", Abbreviation = "kV", Description = "", ParentId = voltageId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "毫伏", Abbreviation = "mV", Description = "", ParentId = voltageId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 功率单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "瓦特", Abbreviation = "W", Description = "", ParentId = powerId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千瓦", Abbreviation = "kW", Description = "", ParentId = powerId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "兆瓦", Abbreviation = "MW", Description = "", ParentId = powerId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "马力", Abbreviation = "hp", Description = "", ParentId = powerId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 面积单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "平方米", Abbreviation = "m²", Description = "", ParentId = areaId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "平方公里", Abbreviation = "km²", Description = "", ParentId = areaId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "公顷", Abbreviation = "ha", Description = "", ParentId = areaId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "亩", Abbreviation = "亩", Description = "", ParentId = areaId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 体积单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "立方米", Abbreviation = "m³", Description = "", ParentId = volumeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "升", Abbreviation = "L", Description = "", ParentId = volumeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "毫升", Abbreviation = "mL", Description = "", ParentId = volumeId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 速度单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "米/秒", Abbreviation = "m/s", Description = "", ParentId = speedId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千米/小时", Abbreviation = "km/h", Description = "", ParentId = speedId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "英里/小时", Abbreviation = "mph", Description = "", ParentId = speedId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 压力单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "帕斯卡", Abbreviation = "Pa", Description = "", ParentId = pressureId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千帕", Abbreviation = "kPa", Description = "", ParentId = pressureId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "兆帕", Abbreviation = "MPa", Description = "", ParentId = pressureId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "巴", Abbreviation = "bar", Description = "", ParentId = pressureId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 能量单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "焦耳", Abbreviation = "J", Description = "", ParentId = energyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千焦", Abbreviation = "kJ", Description = "", ParentId = energyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千瓦时", Abbreviation = "kWh", Description = "", ParentId = energyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 频率单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "赫兹", Abbreviation = "Hz", Description = "", ParentId = frequencyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "千赫", Abbreviation = "kHz", Description = "", ParentId = frequencyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "兆赫", Abbreviation = "MHz", Description = "", ParentId = frequencyId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 角度单位
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "度", Abbreviation = "°", Description = "", ParentId = angleId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });
    nodes.Add(new UnitTree { Id = Guid.NewGuid().ToString(), Name = "弧度", Abbreviation = "rad", Description = "", ParentId = angleId, IsActive = true, SortOrder = sortOrder++, HasChildren = false });

    // 返回所有节点
    return nodes;
}
}