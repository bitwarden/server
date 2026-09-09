using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bit.Core.Utilities;

/// <summary>
/// A custom JSON converter for a string property that already contains a serialized JSON value
/// (e.g. Policy.Data). Avoids an unnecessary deserialize/re-serialize round trip through an
/// intermediate object graph.
/// </summary>
/// <remarks>
/// On write, the string's contents are written directly to the output as raw JSON. On read,
/// the value is validated as well-formed JSON via
/// <see cref="JsonDocument"/> and captured as its raw text.
/// </remarks>
public class RawJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value);
    }
}
