using System.Text.Json;
using MessagePack;
using MessagePack.Formatters;

namespace Bit.Notifications;

/// <summary>
/// Resolves a MessagePack formatter for <see cref="JsonElement"/>, so a payload carried as raw JSON
/// can be written to SignalR clients. Every other type is left to the resolvers that follow it.
/// </summary>
internal sealed class JsonElementResolver : IFormatterResolver
{
    public static readonly JsonElementResolver Instance = new();

    private JsonElementResolver()
    {
    }

    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        return FormatterCache<T>.Formatter;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter =
            typeof(T) == typeof(JsonElement)
                ? (IMessagePackFormatter<T>)(object)JsonElementFormatter.Instance
                : null;
    }
}

internal sealed class JsonElementFormatter : IMessagePackFormatter<JsonElement>
{
    public static readonly JsonElementFormatter Instance = new();

    private JsonElementFormatter()
    {
    }

    public void Serialize(ref MessagePackWriter writer, JsonElement value, MessagePackSerializerOptions options)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                var arrayElements = value.EnumerateArray().ToArray();
                writer.WriteArrayHeader(arrayElements.Length);
                foreach (var arrayElement in arrayElements)
                {
                    Serialize(ref writer, arrayElement, options);
                }
                break;
            case JsonValueKind.Object:
                var objectProperties = value.EnumerateObject().ToArray();
                writer.WriteMapHeader(objectProperties.Length);
                foreach (var objectProp in objectProperties)
                {
                    writer.Write(objectProp.Name);
                    Serialize(ref writer, objectProp.Value, options);
                }
                break;
            case JsonValueKind.Number:
                // Write(long) and Write(ulong) choose the smallest encoding, the same as the
                // typed path, so small values keep their pinned bytes.
                if (value.TryGetInt64(out var longValue))
                {
                    writer.Write(longValue);
                }
                else if (value.TryGetUInt64(out var ulongValue))
                {
                    writer.Write(ulongValue);
                }
                else
                {
                    writer.Write(value.GetDouble());
                }
                break;
            case JsonValueKind.String:
                // Any string that parses as a date is sent as a timestamp, including one that is a
                // string on the typed payload. That difference is accepted for now.
                if (value.TryGetDateTime(out var dateTimeValue))
                {
                    writer.Write(dateTimeValue);
                }
                else
                {
                    writer.Write(value.GetString());
                }
                break;
            case JsonValueKind.False:
                writer.Write(false);
                break;
            case JsonValueKind.True:
                writer.Write(true);
                break;
            case JsonValueKind.Null:
                writer.WriteNil();
                break;
            default:
                throw new InvalidOperationException($"Got an unexpected json value kind: {value.ValueKind}");
        }
    }

    public JsonElement Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        throw new NotImplementedException("Deserialization is not expected.");
    }
}
