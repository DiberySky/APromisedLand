using System.Collections.Concurrent;
using MafRagApi.Models;

namespace MafRagApi.Services;

/// <summary>
/// 进程内指令模板存储。启动时预置一批内置模板，运行期可增删。
/// 单实例适用；多副本部署需换 Redis 等。
/// </summary>
public sealed class InMemoryInstructionTemplateStore : IInstructionTemplateStore
{
    private readonly ConcurrentDictionary<string, InstructionTemplate> _store
        = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryInstructionTemplateStore()
    {
        // ─── 内置模板 ──────────────────────────────────
        Add(new InstructionTemplate
        {
            Id          = "proofread",
            Name        = "中文校对",
            Description = "修正错别字、标点、语序，保持原意。",
            Instruction = "你是中文校对员。修正输入中的错别字、标点、语序，保持原意。只输出修正后的文本。",
        });

        Add(new InstructionTemplate
        {
            Id          = "summarize",
            Name        = "一句话摘要",
            Description = "压缩为不超过 50 字，保留核心数据与结论。",
            Instruction = "将输入压缩成一句话，不超过 50 字，保留核心数据与结论。只输出摘要。",
        });

        Add(new InstructionTemplate
        {
            Id           = "extract-json",
            Name         = "结构化抽取（公司 / 人物 / 金额）",
            Description  = "从文本抽取 company / person / amount，找不到填 null。",
            Instruction  = "抽取文本中的公司、人物、金额。找不到的字段填 null。",
            OutputFormat = "json",
            Examples     = new[]
            {
                new InstructExampleDto
                {
                    Input  = "张三在字节跳动拿到 50 万签字费",
                    Output = """{"company":"字节跳动","person":"张三","amount":500000}""",
                },
                new InstructExampleDto
                {
                    Input  = "李四入职腾讯，月薪 3 万",
                    Output = """{"company":"腾讯","person":"李四","amount":30000}""",
                },
            },
        });

        Add(new InstructionTemplate
        {
            Id          = "translate-en",
            Name        = "中译英（保术语）",
            Description = "技术术语保留原文 + 括号中文。",
            Instruction = "将输入翻译成英语。技术术语保留原文并在括号里给出中文，例如 \"latency（延迟）\"。只输出译文。",
        });

        Add(new InstructionTemplate
        {
            Id          = "classify",
            Name        = "文本分类",
            Description = "归入 技术 / 产品 / 运营 / 财务 / 其他。",
            Instruction = "将输入归入以下类别之一：技术/产品/运营/财务/其他。只输出类别名。",
        });
    }

    // ─── 读 / 写 ────────────────────────────────────
    public IReadOnlyList<InstructionTemplate> List()
        => _store.Values.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool TryGet(string id, out InstructionTemplate template)
        => _store.TryGetValue(id, out template!);

    public bool TryAdd(InstructionTemplate template)
        => _store.TryAdd(template.Id, template);

    public bool TryRemove(string id) => _store.TryRemove(id, out _);

    // ─── 私有：仅构造函数使用的内置模板添加 ─────────
    /// <summary>内部使用：添加内置模板；id 重复时 fail-fast。</summary>
    private void Add(InstructionTemplate tpl)
    {
        if (!_store.TryAdd(tpl.Id, tpl))
            throw new InvalidOperationException(
                $"内置模板 id 重复: '{tpl.Id}'。请检查 InMemoryInstructionTemplateStore 构造函数。");
    }
}