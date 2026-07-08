namespace APromisedLand.AppHost.Extensions;

public static class YarpExtensions
{
    public static IDistributedApplicationBuilder AddYarp(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // if (context.WeatherApi is null || context.QuestionService is null ||
        //     context.TypesenseService is null || context.ElasticService is null ||
        //     context.FileTransService is null) return builder;

        context.YarpGateway = builder.AddYarp("Yarp")
            .WithConfiguration(yarp =>
            {
                yarp.AddRoute("/WeatherForecast/{**catch-all}", context.WeatherApi);
                yarp.AddRoute("/Questions/{**catch-all}", context.QuestionService);
                yarp.AddRoute("/tags/{**catch-all}", context.QuestionService);
                // yarp.AddRoute("/search-mini/{**catch-all}", context.TypesenseService);
                yarp.AddRoute("/typesense/{**catch-all}", context.TypesenseService);
                // yarp.AddRoute("/elastic/{**catch-all}", context.ElasticService); 
                // yarp.AddRoute("/filetrans/{**catch-all}", context.FileTransService);
            })
            .WithHttpEndpoint(port: 8919, targetPort: 8919, name: "http")
            .WithOtlpExporter();

        return builder;
    }
}