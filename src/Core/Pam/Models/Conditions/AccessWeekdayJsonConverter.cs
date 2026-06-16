using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models.Conditions;

/// <summary>
/// (De)serializes <see cref="AccessWeekday"/> as the lowercase three-letter tokens the conditions JSON uses
/// (<c>"sun".."sat"</c>), keeping the wire format stable while the value is strongly typed in C#. This is the
/// single source of truth for the accepted day vocabulary; an unknown token fails closed with a
/// <see cref="JsonException"/>.
/// </summary>
public sealed class AccessWeekdayJsonConverter : JsonConverter<AccessWeekday>
{
    private static readonly IReadOnlyDictionary<string, AccessWeekday> _fromToken =
        new Dictionary<string, AccessWeekday>(StringComparer.OrdinalIgnoreCase)
        {
            ["sun"] = AccessWeekday.Sun,
            ["mon"] = AccessWeekday.Mon,
            ["tue"] = AccessWeekday.Tue,
            ["wed"] = AccessWeekday.Wed,
            ["thu"] = AccessWeekday.Thu,
            ["fri"] = AccessWeekday.Fri,
            ["sat"] = AccessWeekday.Sat,
        };

    public override AccessWeekday Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            _fromToken.TryGetValue(reader.GetString()!, out var day))
        {
            return day;
        }

        throw new JsonException("Invalid day. Expected one of: sun, mon, tue, wed, thu, fri, sat.");
    }

    public override void Write(Utf8JsonWriter writer, AccessWeekday value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToToken(value));

    private static string ToToken(AccessWeekday day) => day switch
    {
        AccessWeekday.Sun => "sun",
        AccessWeekday.Mon => "mon",
        AccessWeekday.Tue => "tue",
        AccessWeekday.Wed => "wed",
        AccessWeekday.Thu => "thu",
        AccessWeekday.Fri => "fri",
        AccessWeekday.Sat => "sat",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, null),
    };
}
