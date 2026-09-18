using LiteGraph.Sdk;

namespace MAFWorkFlowApi.Models;

public sealed record NodeUpsertResult(
    bool Success, string? ErrorCode, string? ErrorMessage,
    Node? Node, bool IsNew, string? PathName = null)
{
    public static NodeUpsertResult Ok(
        Node node, bool isNew, string pathName)
        => new(true, null, null, node, isNew, pathName);

    public static NodeUpsertResult Fail(
        string code, string message)
        => new(false, code, message, null, false);
}