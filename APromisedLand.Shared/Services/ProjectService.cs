using APromisedLand.Shared.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static string Version { get; set; } = "2026.06.23.01";

    public static bool Debug { get; set; } = false;

    public static string Copyright { get; set; } = $"\u00A9 {DateTime.Now.Year} 武汉浩瀚科技有限公司";
}
