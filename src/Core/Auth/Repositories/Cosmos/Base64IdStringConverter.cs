using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Auth.Repositories.Cosmos;

public class Base64IdStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ToKey(reader.GetString());

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToId(value));

    public static string? ToId(string? key)
    {
        if (key == null)
        {
            return null;
        }
        return CoreHelpers.TransformToBase64Url(key);
    }

    public static string? ToKey(string? id)
    {
        if (id == null)
        {
            return null;
        }
        return CoreHelpers.TransformFromBase64Url(id);
    }
}
