namespace APromisedLand.Shared.Projects.SeaweedFS.Services;

public class SeaweedFsOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8888";
    public string MasterUrl { get; set; } = "http://localhost:9333";
    public string? VolumeUrl { get; set; }
}