using System.Text.Json;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Projects.Elasticsearch.Models;
using APromisedLand.Shared.Projects.Nats;
using APromisedLand.Shared.Projects.Nats.Models;
using APromisedLand.Shared.Projects.Nats.Services;
using NATS.Client.Core;

namespace APromisedLand.Shared.Projects.Elasticsearch.Nats;

public class NatsDocumentPublisher(NatsConnection nats) : INatsDocumentPublisher
{
    public async Task PublishAsync(DocumentData document, NatsOperation operation)
    {
        var message = new DocumentMessage()
        {
            Operation = operation,
            Document = document
        };
        
        var json = JsonSerializer.Serialize(message);
        
        await nats.PublishAsync(SolutionHelper.NatsDocumentSubject, json);
    }
}