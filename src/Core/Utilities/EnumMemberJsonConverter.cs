using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bit.Core.Utilities;

/// <summary>
/// A custom JSON converter for enum types that respects the <see cref="EnumMemberAttribute"/> when serializing and deserializing.
/// </summary>
/// <typeparam name="T">The enum type to convert. Must be a struct and implement Enum.</typeparam>
/// <remarks>
/// This converter builds lookup dictionaries at initialization to efficiently map between enum values and their
/// string representations. If an enum value has an <see cref="EnumMemberAttribute"/>, the attribute's Value
/// property is used as the JSON string; otherwise, the enum's ToString() value is used.
/// </remarks>
public class EnumMemberJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly Dictionary<T, string> _enumToString = new();
    private readonly Dictionary<string, T> _stringToEnum = new();

    public EnumMemberJsonConverter()
    {
        var type = typeof(T);
        var values = Enum.GetValues<T>();

        foreach (var value in values)
        {
            var fieldInfo = type.GetField(value.ToString());
            var attribute = fieldInfo?.GetCustomAttribute<EnumMemberAttribute>();

            var stringValue = attribute?.Value ?? value.ToString();
            _enumToString[value] = stringValue;
            _stringToEnum[stringValue] = value;
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();

        if (!string.IsNullOrEmpty(stringValue) && _stringToEnum.TryGetValue(stringValue, out var enumValue))
        {
            return enumValue;
        }

        throw new JsonException($"Unable to convert '{stringValue}' to {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteStringValue(_enumToString[value]);
}
