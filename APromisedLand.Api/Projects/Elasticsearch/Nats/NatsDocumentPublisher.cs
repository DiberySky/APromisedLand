using System.Text.Json;
using APromisedLand.Api.Helper;
using APromisedLand.Api.Projects.Nats.Models;
using APromisedLand.Api.Projects.Nats.Services;
using APromisedLand.Shared.Helper;
using NATS.Client.Core;

namespace APromisedLand.Api.Projects.Elasticsearch.Nats;

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