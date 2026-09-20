using Markdig;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// Markdown → HTML 渲染工具。
/// 禁用原始 HTML 以防 XSS；启用常用扩展（表格、任务列表、自动链接）。
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()   // 表格、任务列表、删除线、自动链接
        .DisableHtml()             // ★ 关键：禁止原始 HTML，防止 XSS
        .Build();

    /// <summary>把 Markdown 文本转换为 HTML 字符串。</summary>
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, Pipeline);
    }
}