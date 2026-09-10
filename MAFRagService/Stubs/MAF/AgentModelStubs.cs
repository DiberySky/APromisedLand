// 桩代码：MAF 模型接口

namespace MAFRagService.Stubs.MAF;

public interface IAgentModel
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}
