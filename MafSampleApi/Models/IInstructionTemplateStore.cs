using MafSampleApi.Models;

namespace MafSampleApi.Services;

/// <summary>指令模板存储抽象。</summary>
public interface IInstructionTemplateStore
{
    IReadOnlyList<InstructionTemplate> List();
    bool TryGet(string id, out InstructionTemplate template);
    bool TryAdd(InstructionTemplate template);
    bool TryRemove(string id);
}