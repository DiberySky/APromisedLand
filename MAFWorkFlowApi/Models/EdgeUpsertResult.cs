using LiteGraph.Sdk;

namespace MAFWorkFlowApi.Models;

public sealed record EdgeUpsertResult(
    bool Success, string? ErrorCode, string? ErrorMessage,
    Edge? Edge, bool IsNew)
{
    public static EdgeUpsertResult Ok(Edge edge, bool isNew)
        => new(true, null, null, edge, isNew);

    public static EdgeUpsertResult Fail(
        string code, string message)
        => new(false, code, message, null, false);
}