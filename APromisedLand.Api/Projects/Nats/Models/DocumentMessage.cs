using APromisedLand.Api.Helper;

namespace APromisedLand.Api.Projects.Nats.Models;

public class DocumentMessage
{
    public NatsOperation Operation { get; set; } = NatsOperation.Create; // "create", "update", "delete"
    public required DocumentData Document { get; set; }
}