using APromisedLand.Api.Helper;
using APromisedLand.Api.Projects.Nats.Models;

namespace APromisedLand.Api.Projects.Nats.Services;

public interface INatsDocumentPublisher
{
    Task PublishAsync(DocumentData document, NatsOperation operation);
}