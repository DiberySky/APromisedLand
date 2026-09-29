using System.ComponentModel;
using System.Data;
using Microsoft.Extensions.AI;

namespace MafRagApi.Services.Tools;

public sealed class MathTools
{
    [Description("计算一个数学表达式。支持 + - * / % 和括号。")]
    public string Evaluate(
        [Description("要计算的表达式，例如 (1+2)*3 或 100/4。")] string expression)
    {
        try
        {
            var dt = new DataTable();
            var result = dt.Compute(expression, string.Empty);
            return result?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"错误: 无法计算 '{expression}' — {ex.Message}";
        }
    }

    public IReadOnlyList<ToolDescriptor> GetTools()
    {
        var fn = AIFunctionFactory.Create(
            Evaluate,
            name: "evaluate_math",
            description: "计算数学表达式。支持 + - * / % 和括号。");

        return new[]
        {
            new ToolDescriptor
            {
                Name = "evaluate_math",
                Function = fn,
                Description = "计算数学表达式",
                Tags = new[] { "math", "safe" },
                IsBase = true,
            },
        };
    }
}