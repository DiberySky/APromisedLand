using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Models;

public class PageInfo
{
    public required string Name { get; set; }
    public bool Authorized { get; set; }

    public bool IsDialog { get; set; }
}
