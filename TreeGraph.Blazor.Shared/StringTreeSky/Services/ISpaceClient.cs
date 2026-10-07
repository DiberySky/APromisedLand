using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>空间客户端。</summary>
public interface ISpaceClient
{
    Task<List<SpaceDto>> ListSpacesAsync(CancellationToken ct = default);
    Task<SpaceDto?> GetSpaceAsync(string id, CancellationToken ct = default);
    Task<SpaceDto> CreateSpaceAsync(SpaceDto dto, CancellationToken ct = default);
    Task<SpaceDto?> UpdateSpaceAsync(SpaceDto dto, CancellationToken ct = default);
    Task<bool> DeleteSpaceAsync(string id, CancellationToken ct = default);
}
