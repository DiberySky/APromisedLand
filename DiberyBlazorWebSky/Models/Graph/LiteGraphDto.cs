namespace DiberyBlazorWebSky.Models.Graph;

public sealed class EnumerateResponse<T>
{
    public long TotalRecords { get; set; }
    public Guid? ContinuationToken { get; set; }
    public List<T>? Objects { get; set; }
}

public sealed record GraphNodeDto(Guid GUID, string Name);
public sealed record GraphEdgeDto(Guid GUID, Guid From, Guid To, string Name);
public sealed record GraphVectorDto(Guid? NodeGUID, Guid? EdgeGUID, List<float>? Vectors, string? Content);
