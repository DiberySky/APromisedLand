namespace NebulaGraphApiService.Services;

public interface INebulaGraphSeedService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}