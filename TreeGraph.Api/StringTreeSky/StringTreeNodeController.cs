using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeSky;
using TreeGraph.Blazor.Shared.Trees.Models;

namespace TreeGraph.Api.StringTreeSky;

/// <summary>
/// 字符串节点树 API。路由 /StringTreeNode 与 TreeSky 组件客户端
/// DiberyTreeApiClient&lt;StringTreeNode&gt; 的 _basePath（typeof(T).Name）对齐。
/// </summary>
[ApiController]
[Route("[controller]")]
public class StringTreeNodeController(
    ITreeService<StringTreeNode> treeService,
    ILogger<StringTreeNodeController> logger)
    : TreeControllerBase<StringTreeNode>(treeService, logger);
