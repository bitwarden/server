using System.Text.Json;
using Bit.Core.Billing.Pricing.HTTP.Models;

namespace Bit.Core.Billing.Pricing.HTTP.JSON;

#nullable enable

public class FreeOrScalableDTOJsonConverter : TypeReadingJsonConverter<FreeOrScalableDTO>
{
    public override FreeOrScalableDTO? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = ReadType(reader);

        return type switch
        {
            "free" => JsonSerializer.Deserialize<FreeDTO>(ref reader, options) switch
            {
                null => null,
                var free => new FreeOrScalableDTO(free)
            },
            "scalable" => JsonSerializer.Deserialize<ScalableDTO>(ref reader, options) switch
            {
                null => null,
                var scalable => new FreeOrScalableDTO(scalable)
            },
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, FreeOrScalableDTO value, JsonSerializerOptions options)
        => value.Switch(
            free => JsonSerializer.Serialize(writer, free, options),
            scalable => JsonSerializer.Serialize(writer, scalable, options)
        );
}
