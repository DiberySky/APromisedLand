using APromisedLand.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static Dictionary<string, PageInfo> StartDict { get; set;  } = new Dictionary<string, PageInfo>
    {
        { "天气", new PageInfo() { Name="Weather",  Authorized=true }},
        {"测试", new PageInfo() { Name="TestingApl",  Authorized=false }},
        {"操作系统", new PageInfo() { Name="os-info",  Authorized=false }},
        {"屏幕分辨率", new PageInfo() { Name="screen-info",  Authorized=false }},
        {"窗口大小",new PageInfo() { Name="window-size",  Authorized=false }},
        {"关于",new PageInfo() { Name="About",  IsDialog=true }},
    };
}
