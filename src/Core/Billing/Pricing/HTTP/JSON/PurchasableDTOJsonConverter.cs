using System.Text.Json;
using Bit.Core.Billing.Pricing.HTTP.Models;

namespace Bit.Core.Billing.Pricing.HTTP.JSON;

#nullable enable
internal class PurchasableDTOJsonConverter : TypeReadingJsonConverter<PurchasableDTO>
{
    public override PurchasableDTO? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = ReadType(reader);

        return type switch
        {
            "free" => JsonSerializer.Deserialize<FreeDTO>(ref reader, options) switch
            {
                null => null,
                var free => new PurchasableDTO(free)
            },
            "packaged" => JsonSerializer.Deserialize<PackagedDTO>(ref reader, options) switch
            {
                null => null,
                var packaged => new PurchasableDTO(packaged)
            },
            "scalable" => JsonSerializer.Deserialize<ScalableDTO>(ref reader, options) switch
            {
                null => null,
                var scalable => new PurchasableDTO(scalable)
            },
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, PurchasableDTO value, JsonSerializerOptions options)
        => value.Switch(
            free => JsonSerializer.Serialize(writer, free, options),
            packaged => JsonSerializer.Serialize(writer, packaged, options),
            scalable => JsonSerializer.Serialize(writer, scalable, options)
        );
}
