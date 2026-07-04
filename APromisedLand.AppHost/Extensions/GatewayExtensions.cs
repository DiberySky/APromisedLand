namespace APromisedLand.AppHost.Extensions;

public static class GatewayExtensions
{
    public static IDistributedApplicationBuilder AddGateway(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        if (context.WeatherApi is null || context.QuestionService is null ||
            context.TypesenseService is null || context.ElasticService is null ||
            context.FileTransService is null) return builder;

        context.YarpGateway = builder.AddYarp("Yarp")
            .WithConfiguration(yarp =>
            {
                yarp.AddRoute("/WeatherForecast/{**catch-all}", context.WeatherApi);
                yarp.AddRoute("/Questions/{**catch-all}", context.QuestionService);
                yarp.AddRoute("/tags/{**catch-all}", context.QuestionService);
                yarp.AddRoute("/search-mini/{**catch-all}", context.TypesenseService);
                yarp.AddRoute("/typesense/{**catch-all}", context.TypesenseService);
                yarp.AddRoute("/elastic/{**catch-all}", context.ElasticService); 
                yarp.AddRoute("/filetrans/{**catch-all}", context.FileTransService);
            })
            .WithHttpEndpoint(port: 8090, targetPort: 8090, name: "http")
            .WithOtlpExporter();

        return builder;
    }
}