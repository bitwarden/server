using System.Text.Json.Serialization;
using Bit.Core.Billing.Pricing.JSON;
using OneOf;

namespace Bit.Core.Billing.Pricing.Models;

#nullable enable

[JsonConverter(typeof(PurchasableDTOJsonConverter))]
public class PurchasableDTO(OneOf<FreeDTO, PackagedDTO, ScalableDTO> input) : OneOfBase<FreeDTO, PackagedDTO, ScalableDTO>(input)
{
    public static implicit operator PurchasableDTO(FreeDTO free) => new(free);
    public static implicit operator PurchasableDTO(PackagedDTO packaged) => new(packaged);
    public static implicit operator PurchasableDTO(ScalableDTO scalable) => new(scalable);

    public T? FromFree<T>(Func<FreeDTO, T> select, Func<PurchasableDTO, T>? fallback = null) =>
        IsT0 ? select(AsT0) : fallback != null ? fallback(this) : default;

    public T? FromPackaged<T>(Func<PackagedDTO, T> select, Func<PurchasableDTO, T>? fallback = null) =>
        IsT1 ? select(AsT1) : fallback != null ? fallback(this) : default;

    public T? FromScalable<T>(Func<ScalableDTO, T> select, Func<PurchasableDTO, T>? fallback = null) =>
        IsT2 ? select(AsT2) : fallback != null ? fallback(this) : default;

    public bool IsFree => IsT0;
    public bool IsPackaged => IsT1;
    public bool IsScalable => IsT2;
}

[JsonConverter(typeof(FreeOrScalableDTOJsonConverter))]
public class FreeOrScalableDTO(OneOf<FreeDTO, ScalableDTO> input) : OneOfBase<FreeDTO, ScalableDTO>(input)
{
    public static implicit operator FreeOrScalableDTO(FreeDTO freeDTO) => new(freeDTO);
    public static implicit operator FreeOrScalableDTO(ScalableDTO scalableDTO) => new(scalableDTO);

    public T? FromFree<T>(Func<FreeDTO, T> select, Func<FreeOrScalableDTO, T>? fallback = null) =>
        IsT0 ? select(AsT0) : fallback != null ? fallback(this) : default;

    public T? FromScalable<T>(Func<ScalableDTO, T> select, Func<FreeOrScalableDTO, T>? fallback = null) =>
        IsT1 ? select(AsT1) : fallback != null ? fallback(this) : default;

    public bool IsFree => IsT0;
    public bool IsScalable => IsT1;
}

public class FreeDTO
{
    public int Quantity { get; set; }
    public string Type => "free";
}

public class PackagedDTO
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

public class ScalableDTO
{
    public int Provided { get; set; }
    public string StripePriceId { get; set; } = null!;
    public decimal Price { get; set; }
    public string Type => "scalable";
}
