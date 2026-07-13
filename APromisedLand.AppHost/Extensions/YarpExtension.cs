namespace APromisedLand.AppHost.Extensions;

public static class YarpExtension
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
                if (context.WeatherApi is not null) 
                {
                    yarp.AddRoute("/WeatherForecast/{**catch-all}", context.WeatherApi);
                }
                if (context.QuestionService is not null) 
                {
                    yarp.AddRoute("/Questions/{**catch-all}", context.QuestionService);
                    yarp.AddRoute("/tags/{**catch-all}", context.QuestionService);
                }
                if (context.TypesenseService is not null) 
                {
                    yarp.AddRoute("/search-mini/{**catch-all}", context.TypesenseService);
                    yarp.AddRoute("/typesense/{**catch-all}", context.TypesenseService);
                }
                if (context.ElasticService is not null) 
                {
                    yarp.AddRoute("/elastic/{**catch-all}", context.ElasticService); 
                }
                if (context.FileTransService is not null) 
                {
                    yarp.AddRoute("/filetrans/{**catch-all}", context.FileTransService);
                }
                if (context.DiberyTreeService is not null) 
                {
                    yarp.AddRoute("/DiberyTree/{**catch-all}", context.DiberyTreeService);
                }
            })
            .WithHttpEndpoint(port: 8919, targetPort: 8919, name: "http")
            .WithOtlpExporter();

        return builder;
    }
}