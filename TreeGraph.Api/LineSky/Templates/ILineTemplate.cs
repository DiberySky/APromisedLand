using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Templates;

/// <summary>
/// 线路结构模板：预填端子角色/状态默认值（R7：拓扑模式已下线，结构由边连接定义）。
/// 模板是"建议层"——只在 UI 层预填默认值，不参与后端校验。
/// 未来新增模板只要实现此接口 + 注册到 TemplateRegistry。
/// </summary>
public interface ILineTemplate
{
    string Key { get; }
    string DisplayName { get; }
    string Description { get; }
    TerminalRole DefaultTerminalRole { get; }
    TerminalOperatingState DefaultOperatingState { get; }

    /// <summary>转 DTO 给 API 返回（枚举转 string 避免 MudBlazor source generator bug）。</summary>
    LineTemplateDto ToDto() => new(
        Key, DisplayName, Description,
        DefaultTerminalRole.ToString(), DefaultOperatingState.ToString());
}
