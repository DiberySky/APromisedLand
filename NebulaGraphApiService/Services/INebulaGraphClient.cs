namespace NebulaGraphApiService.Services;

public interface INebulaGraphClient
{
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
    Task<bool> SpaceExistsAsync(string spaceName, CancellationToken cancellationToken);
    Task CreateSpaceAsync(string spaceName, CancellationToken cancellationToken);
    Task<bool> SpaceReadyAsync(string spaceName, CancellationToken cancellationToken);
    Task UseSpaceAsync(string spaceName, CancellationToken cancellationToken);
    Task<bool> TagExistsAsync(string tagName, CancellationToken cancellationToken);
    Task<bool> EdgeExistsAsync(string edgeName, CancellationToken cancellationToken);
    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken);
    Task<bool> VertexExistsAsync(string vid, CancellationToken cancellationToken);
    Task<bool> EdgeExistsBetweenAsync(string src, string dst, string edgeName, CancellationToken cancellationToken);
    Task ExecuteAsync(string statement, CancellationToken cancellationToken);
}