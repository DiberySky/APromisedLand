using Typesense;

namespace SearchService.Data;

public static class SearchInitializer
{
    public static async Task EnsureIndexExists(ITypesenseClient client)
    {
        const string schemaName = "questions";

        try
        {
            await client.RetrieveCollection(schemaName);
            Console.WriteLine($"集合模式{schemaName}已创建。");
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine($"集合模式{schemaName}尚未创建。");
        }

        var schema = new Schema(schemaName, new List<Field>
        {
            new("id", FieldType.String),
            new("title", FieldType.String),
            new("content", FieldType.String),
            new("tags", FieldType.StringArray),
            new("createdAt", FieldType.Int64),
            new("answerCount", FieldType.Int32),
            new("hasAcceptedAnswer", FieldType.Bool),
        })
        {
            DefaultSortingField = "createdAt"
        };
        
        await client.CreateCollection(schema);
        Console.WriteLine($"集合模式{schemaName}已创建");
    }
}