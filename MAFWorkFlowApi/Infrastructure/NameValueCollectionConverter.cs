using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// 让 System.Text.Json 把 NameValueCollection 序列化为 JSON 对象（key-value），
/// 而不是默认的字符串数组。
/// </summary>
public sealed class NameValueCollectionConverter : JsonConverter<NameValueCollection>
{
    public override NameValueCollection? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("期望 JSON 对象");

        var result = new NameValueCollection();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return result;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("期望属性名");

            var key = reader.GetString()!;
            reader.Read();
            var value = reader.TokenType == JsonTokenType.Null
                ? null
                : reader.GetString();
            result[key] = value;
        }
        throw new JsonException("未闭合的对象");
    }

    public override void Write(
        Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var key in value.AllKeys)
        {
            if (key is null) continue;
            writer.WriteString(key, value[key]);
        }
        writer.WriteEndObject();
    }
}