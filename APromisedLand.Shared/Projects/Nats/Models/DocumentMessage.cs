using APromisedLand.Shared.Helper;

namespace APromisedLand.Shared.Projects.Nats.Models;

public class DocumentMessage
{
    public NatsOperation Operation { get; set; } = NatsOperation.Create; // "create", "update", "delete"
    public required DocumentData Document { get; set; }
}