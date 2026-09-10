// 桩代码：MAF 工具类型

namespace MAFRagService.Stubs.MAF;

public interface ITool
{
}

[AttributeUsage(AttributeTargets.Class)]
public class ToolAttribute : Attribute
{
    public string Name { get; }

    public ToolAttribute(string name)
    {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class ToolFunctionAttribute : Attribute
{
    public string Description { get; }

    public ToolFunctionAttribute(string description)
    {
        Description = description;
    }
}
