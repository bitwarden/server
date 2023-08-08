namespace Bit.Core.Models.StaticStore;

public class PasswordManagerPlanFeatures
{
    public short? BaseStorageGb { get; set; }
    public short? MaxCollections { get; set; }
    public bool HasAdditionalStorageOption { get; set; }
    public short? MaxAdditionalStorage { get; set; }
    public bool HasPremiumAccessOption { get; set; }
    public int? LegacyYear { get; set; }
    public string StripePremiumAccessPlanId { get; set; }
    public decimal AdditionalStoragePricePerGb { get; set; }
    public decimal PremiumAccessOptionPrice { get; set; }
    public string StripeStoragePlanId { get; set; }
    public string StripeSeatPlanId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal SeatPrice { get; set; }
    public bool AllowSeatAutoscale { get; set; }
    public bool HasAdditionalSeatsOption { get; set; }
    public int? MaxAdditionalSeats { get; set; }
    public int BaseSeats { get; set; }
}
