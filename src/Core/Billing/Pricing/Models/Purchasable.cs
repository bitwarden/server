using System.Text.Json;
using System.Text.Json.Serialization;
using OneOf;

namespace Bit.Core.Billing.Pricing.Models;

[JsonConverter(typeof(PurchasableJsonConverter))]
public class Purchasable(OneOf<Free, Packaged, Scalable> input) : OneOfBase<Free, Packaged, Scalable>(input)
{
    public static implicit operator Purchasable(Free free) => new(free);
    public static implicit operator Purchasable(Packaged packaged) => new(packaged);
    public static implicit operator Purchasable(Scalable scalable) => new(scalable);

    public T? FromFree<T>(Func<Free, T> select, Func<Purchasable, T>? fallback = null) =>
        IsT0 ? select(AsT0) : fallback != null ? fallback(this) : default;

    public T? FromPackaged<T>(Func<Packaged, T> select, Func<Purchasable, T>? fallback = null) =>
        IsT1 ? select(AsT1) : fallback != null ? fallback(this) : default;

    public T? FromScalable<T>(Func<Scalable, T> select, Func<Purchasable, T>? fallback = null) =>
        IsT2 ? select(AsT2) : fallback != null ? fallback(this) : default;

    public bool IsFree => IsT0;
    public bool IsPackaged => IsT1;
    public bool IsScalable => IsT2;
}

internal class PurchasableJsonConverter : JsonConverter<Purchasable>
{
    private static readonly string _typePropertyName = nameof(Free.Type).ToLower();

    public override Purchasable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);

        if (!element.TryGetProperty(options.PropertyNamingPolicy?.ConvertName(_typePropertyName) ?? _typePropertyName, out var typeProperty))
        {
            throw new JsonException(
                $"Failed to deserialize {nameof(Purchasable)}: missing '{_typePropertyName}' property");
        }

        var type = typeProperty.GetString();

        return type switch
        {
            "free" => element.Deserialize<Free>(options)!,
            "packaged" => element.Deserialize<Packaged>(options)!,
            "scalable" => element.Deserialize<Scalable>(options)!,
            _ => throw new JsonException($"Failed to deserialize {nameof(Purchasable)}: invalid '{_typePropertyName}' value - '{type}'"),
        };
    }

    public override void Write(Utf8JsonWriter writer, Purchasable value, JsonSerializerOptions options)
        => value.Switch(
            free => JsonSerializer.Serialize(writer, free, options),
            packaged => JsonSerializer.Serialize(writer, packaged, options),
            scalable => JsonSerializer.Serialize(writer, scalable, options)
        );
}

[JsonConverter(typeof(FreeOrScalableJsonConverter))]
public class FreeOrScalable(OneOf<Free, Scalable> input) : OneOfBase<Free, Scalable>(input)
{
    public static implicit operator FreeOrScalable(Free free) => new(free);
    public static implicit operator FreeOrScalable(Scalable scalable) => new(scalable);

    public T? FromFree<T>(Func<Free, T> select, Func<FreeOrScalable, T>? fallback = null) =>
        IsT0 ? select(AsT0) : fallback != null ? fallback(this) : default;

    public T? FromScalable<T>(Func<Scalable, T> select, Func<FreeOrScalable, T>? fallback = null) =>
        IsT1 ? select(AsT1) : fallback != null ? fallback(this) : default;

    public bool IsFree => IsT0;
    public bool IsScalable => IsT1;
}

public class FreeOrScalableJsonConverter : JsonConverter<FreeOrScalable>
{
    private static readonly string _typePropertyName = nameof(Free.Type).ToLower();

    public override FreeOrScalable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);

        if (!element.TryGetProperty(options.PropertyNamingPolicy?.ConvertName(_typePropertyName) ?? _typePropertyName, out var typeProperty))
        {
            throw new JsonException(
                $"Failed to deserialize {nameof(FreeOrScalable)}: missing '{_typePropertyName}' property");
        }

        var type = typeProperty.GetString();

        return type switch
        {
            "free" => element.Deserialize<Free>(options)!,
            "scalable" => element.Deserialize<Scalable>(options)!,
            _ => throw new JsonException($"Failed to deserialize {nameof(FreeOrScalable)}: invalid '{_typePropertyName}' value - '{type}'"),
        };
    }

    public override void Write(Utf8JsonWriter writer, FreeOrScalable value, JsonSerializerOptions options)
        => value.Switch(
            free => JsonSerializer.Serialize(writer, free, options),
            scalable => JsonSerializer.Serialize(writer, scalable, options)
        );
}

public class Free
{
    public int Quantity { get; set; }
    public string Type => "free";
}

public class Packaged
{
    public int Quantity { get; set; }
    public string StripePriceId { get; set; } = null!;
    public decimal Price { get; set; }
    public AdditionalSeats? Additional { get; set; }
    public string Type => "packaged";

    public class AdditionalSeats
    {
        public string StripePriceId { get; set; } = null!;
        public decimal Price { get; set; }
    }
}

public class Scalable
{
    public int Provided { get; set; }
    public string StripePriceId { get; set; } = null!;
    public decimal Price { get; set; }
    public string Type => "scalable";
}
