using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.RabbitMQ;

namespace QuestionService.Configs;

public static class WolverineToTypesenseConfig
{

    public static void AddWolverineToTypesenseService(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
        {
            traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(builder.Environment.ApplicationName))
                .AddSource("Wolverine");
        });

        builder.Host.UseWolverine(opts =>
        {
            opts.UseRabbitMqUsingNamedConnection("RabbitMQ").AutoProvision();
            opts.PublishAllMessages().ToRabbitExchange("questions");
        });

    }
}