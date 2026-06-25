using APromisedLand.Shared.Helper;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Models;

public class ExpansionPanel
{
    public required string Title { get; set; }
    public bool Expanded { get; set; }

    public List<PageInfo> PageInfos { get; set; } = [];
}

public class PageInfo
{
    public required string Title { get; set; }
    public required string Name { get; set; }
    public GroupEnum Group { get; set; } = GroupEnum.User;
    public bool Authorized { get; set; }
    public bool IsDialog { get; set; }
}
