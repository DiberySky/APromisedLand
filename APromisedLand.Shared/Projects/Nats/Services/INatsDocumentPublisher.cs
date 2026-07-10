using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Projects.Nats.Models;

namespace APromisedLand.Shared.Projects.Nats.Services;

public interface INatsDocumentPublisher
{
    Task PublishAsync(DocumentData document, NatsOperation operation);
}