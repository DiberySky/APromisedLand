// 桩代码：替代 MAF（Microsoft Agent Framework）基础类型
// 实际使用时替换为真实的 MAF NuGet 包

namespace MAFRagService.Stubs.MAF;

[AttributeUsage(AttributeTargets.Method)]
public class AgentFunctionAttribute : Attribute
{
    public string Name { get; }

    public AgentFunctionAttribute(string name)
    {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class ParameterAttribute : Attribute
{
    public string Description { get; set; } = string.Empty;
}

public abstract class Agent
{
}
