using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bit.Core.Utilities;

public class EpochDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return CoreHelpers.FromEpocMilliseconds(reader.GetInt64());
    }
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(CoreHelpers.ToEpocMilliseconds(value));
    }
}
