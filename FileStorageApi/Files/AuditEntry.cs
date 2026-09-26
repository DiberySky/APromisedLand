namespace FileStorageApi.Files;

public sealed record AuditEntry(
    string  DocId,
    string  Tenant,
    string  Action,
    string? Actor,
    string? DetailsJson);