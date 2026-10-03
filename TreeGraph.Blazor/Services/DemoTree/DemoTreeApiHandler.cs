using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.TreeSky.Models;

namespace TreeGraph.Blazor.Services.DemoTree;

/// <summary>
/// 演示用内存后端：拦截 <see cref="TreeGraph.TreeSky.Services.DiberyTreeApiClient{T}"/>
/// 对 “StringTreeNode/*” 路由的全部调用并转发到 <see cref="InMemoryTreeStore"/>，
/// 使树的增/删/改/排序/移动在没有真实 API 的情况下全链路可演示。
/// 该 Handler 不调用 InnerHandler，请求不会真正发出。
/// </summary>
public class DemoTreeApiHandler(InMemoryTreeStore store) : DelegatingHandler
{
    private const string Base = "StringTreeNode";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
            // 期望形态：["StringTreeNode", ...]
            if (path.Length == 0 || path[0] != Base)
                return NotFound($"未知资源：{request.RequestUri.AbsolutePath}");

            var action = path.Length > 1 ? path[1] : null;

            switch (request.Method.Method, action)
            {
                case ("GET", "roots"):
                    var rootId = path.Length > 2 ? Uri.UnescapeDataString(path[2]) : null;
                    return Ok(store.GetRoots(rootId));

                case ("GET", "children") when path.Length > 2:
                    return Ok(store.GetChildren(Uri.UnescapeDataString(path[2])));

                case ("GET", "full"):
                    // 演示用：完整树以根层表示（前端按懒加载逐层取子节点）
                    return Ok(store.GetRoots(null).FirstOrDefault());

                case ("GET", _) when action != null && path.Length > 2 && path[2] == "ancestors":
                    return Ok(store.GetAncestorPath(Uri.UnescapeDataString(path[1])));

                case ("POST", "query"):
                    return Ok(store.GetRoots(null));

                case ("POST", null):
                {
                    var dto = await ReadAsync<TreeNodeDto<StringTreeNode>>(request);
                    return Ok(store.Create(dto!));
                }

                case ("POST", "children"):
                {
                    var dto = await ReadAsync<TreeNodeDto<StringTreeNode>>(request);
                    return Ok(store.UpdateChildren(dto!));
                }

                case ("POST", "move"):
                {
                    var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                    var nodeId = query["nodeId"];
                    var newParentId = string.IsNullOrEmpty(query["newParentId"]) ? null : query["newParentId"];
                    if (string.IsNullOrEmpty(nodeId))
                        return BadRequest("缺少 nodeId");

                    // 复用 Update 完成移动
                    var moved = store.Update(nodeId, new TreeNodeDto<StringTreeNode>
                    {
                        Id = nodeId,
                        ParentId = newParentId,
                    });
                    return Ok(true);
                }

                case ("PUT", _) when action != null:
                {
                    var dto = await ReadAsync<TreeNodeDto<StringTreeNode>>(request);
                    return Ok(store.Update(Uri.UnescapeDataString(action), dto!));
                }

                case ("DELETE", _) when action != null:
                    var deleted = store.Delete(Uri.UnescapeDataString(action));
                    return deleted ? Ok(true) : NotFound("节点不存在");

                default:
                    return NotFound($"不支持的调用：{request.Method} {request.RequestUri.AbsolutePath}");
            }
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    // ==================== 响应辅助 ====================

    private HttpResponseMessage Ok<T>(T data)
        => Json(HttpStatusCode.OK, ApiResponse<T>.Ok(data));

    private HttpResponseMessage NotFound(string message)
        => Json(HttpStatusCode.NotFound, ApiResponse<object>.Fail(message));

    private HttpResponseMessage BadRequest(string message)
        => Json(HttpStatusCode.BadRequest, ApiResponse<object>.Fail(message));

    private HttpResponseMessage Fail(string message)
        => Json(HttpStatusCode.InternalServerError, ApiResponse<object>.Fail(message));

    private HttpResponseMessage Json<T>(HttpStatusCode status, ApiResponse<T> payload)
        => new(status)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

    private static async Task<T?> ReadAsync<T>(HttpRequestMessage request)
    {
        if (request.Content == null) return default;
        return await request.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}

