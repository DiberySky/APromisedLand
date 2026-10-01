using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreeGraph.Api.Services;

/// <summary>数量运行时值序列化为 { value, unitId }（输入走 EAV 管道，不支持直接反序列化）</summary>
public sealed class NumericValueJsonConverter : JsonConverter<NumericValue>
{
    public override NumericValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("数量值请通过 EAV 属性写入管道提交（裸数值或 { value, unitId }）");

    public override void Write(Utf8JsonWriter writer, NumericValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        if (value.UnitId is { } uid)
            writer.WriteString("unitId", uid);
        else
            writer.WriteNull("unitId");
        writer.WriteEndObject();
    }
}

/// <summary>组合类型运行时值序列化为字段字典（输入走 EAV 管道，不支持直接反序列化）</summary>
public sealed class DynamicCompositeValueJsonConverter : JsonConverter<DynamicCompositeValue>
{
    public override DynamicCompositeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("组合类型值请通过 EAV 属性写入管道提交");

    public override void Write(Utf8JsonWriter writer, DynamicCompositeValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, val) in value.Fields)
        {
            writer.WritePropertyName(name);
            if (val is null)
                writer.WriteNullValue();
            else
                JsonSerializer.Serialize(writer, val, val.GetType(), options);
        }
        writer.WriteEndObject();
    }
}
