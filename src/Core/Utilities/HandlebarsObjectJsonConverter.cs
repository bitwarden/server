using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bit.Core.Utilities;

public class HandlebarsObjectJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => true;
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
    }
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
