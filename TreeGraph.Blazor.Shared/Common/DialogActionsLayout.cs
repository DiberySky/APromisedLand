namespace TreeGraph.Blazor.Shared.Common;

/// <summary>对话框操作区布局方式。</summary>
public enum DialogActionsLayout
{
    /// <summary>自动：移动端 Column，桌面端 Row。</summary>
    Auto,
    /// <summary>水平排列（桌面默认）。</summary>
    Row,
    /// <summary>垂直堆叠（移动端：主按钮在上、全部满宽）。</summary>
    Column
}
