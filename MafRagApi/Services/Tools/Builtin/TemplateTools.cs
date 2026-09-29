using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;

namespace MafRagApi.Services.Tools;

public sealed class TemplateTools(IInstructionTemplateStore store)
{
    [Description("列出服务端所有可用的指令模板。")]
    public string ListTemplates()
    {
        var all = store.List();
        if (all.Count == 0) return "当前没有可用模板。";

        var sb = new StringBuilder();
        foreach (var t in all)
        {
            sb.AppendLine($"- id: {t.Id}");
            sb.AppendLine($"  名称: {t.Name}");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.AppendLine($"  描述: {t.Description}");
        }
        return sb.ToString();
    }

    [Description("获取指定指令模板的完整指令内容。")]
    public string GetTemplate(
        [Description("模板 id，可通过 list_templates 获取。")] string id)
    {
        if (!store.TryGet(id, out var tpl))
            return $"未找到模板 '{id}'。";

        var sb = new StringBuilder();
        sb.AppendLine($"id: {tpl.Id}");
        sb.AppendLine($"name: {tpl.Name}");
        sb.AppendLine($"instruction: {tpl.Instruction}");
        if (tpl.OutputFormat is not null)
            sb.AppendLine($"outputFormat: {tpl.OutputFormat}");
        return sb.ToString();
    }

    public IReadOnlyList<ToolDescriptor> GetTools()
    {
        var fnList = AIFunctionFactory.Create(
            ListTemplates,
            name: "list_templates",
            description: "列出所有可用的指令模板。");

        var fnGet = AIFunctionFactory.Create(
            GetTemplate,
            name: "get_template",
            description: "获取指定指令模板的完整内容。");

        return new[]
        {
            new ToolDescriptor
            {
                Name = "list_templates", Function = fnList,
                Description = "列出指令模板",
                Tags = new[] { "template", "safe" },
            },
            new ToolDescriptor
            {
                Name = "get_template", Function = fnGet,
                Description = "获取指令模板详情",
                Tags = new[] { "template", "safe" },
            },
        };
    }
}