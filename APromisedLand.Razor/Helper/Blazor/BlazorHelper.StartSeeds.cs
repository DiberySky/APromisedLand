using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Models;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    public static List<ExpansionPanel> ExpansionPanels { get; set; } =
    [
        new ExpansionPanel
        {
            Title = "功能", Expanded = true,
                PageInfos =
                [
                    new PageInfo { Title = "CategoryTreeDialogPage", Name = "CategoryTree", IsDialog = true, Authorized = false },
                    new PageInfo { Title = "CategoryTreePage", Name = "category-tree", Authorized = false },
                    new PageInfo { Title = "单位注册", Name = "UnitTree", IsDialog = true, Authorized = false },
                    new PageInfo { Title = "附加属性", Name = "attribute-definitions", IsDialog = false, Authorized = false },
                    new PageInfo { Title = "单位", Name = "UnitOfMeasure", IsDialog = true, Authorized = false },
                    new PageInfo { Title = "计量单位", Name = "UnitOfMeasurePage", Authorized = false },
                    new PageInfo { Title = "DiberyTree", Name = "TreePage", Authorized = false },
                    new PageInfo { Title = "天气-api", Name = "Weather", Authorized = true },
                    new PageInfo { Title = "天气-Client", Name = "weatherclient", Authorized = true },
                    new PageInfo { Title = "天气-Factory", Name = "weatherfactory", Authorized = true },
                    new PageInfo { Title = "测试", Name = "TestingApl", Authorized = true , Group = GroupEnum.DiberySky}
                ]
        },
        new ExpansionPanel
        {
            Title = "系统", Expanded = false,
                PageInfos =
                [
                    new PageInfo { Title = "操作系统Test", Name = "os-info", Authorized = false, Group = GroupEnum.Admin },
                    new PageInfo { Title = "屏幕分辨率", Name = "screen-info", Authorized = false },
                    new PageInfo { Title = "窗口尺寸", Name = "window-size", Authorized = true, Group = GroupEnum.Admin },
                    new PageInfo { Title = "关于", Name = "about", Authorized = false, IsDialog = true }
                ]
        },
    ];
}
