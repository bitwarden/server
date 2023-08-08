namespace Bit.Core.Models.StaticStore;

public class SecretsManagerPlanFeatures
{
    public short? MaxServiceAccounts { get; set; }
    public bool AllowServiceAccountsAutoscale { get; set; }
    public string StripeServiceAccountPlanId { get; set; }
    public decimal? AdditionalPricePerServiceAccount { get; set; }
    public short? BaseServiceAccount { get; set; }
    public short? MaxAdditionalServiceAccount { get; set; }
    public bool HasAdditionalServiceAccountOption { get; set; }
    public string StripeSeatPlanId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal SeatPrice { get; set; }
    public bool AllowSeatAutoscale { get; set; }
    public bool HasAdditionalSeatsOption { get; set; }
    public int? MaxAdditionalSeats { get; set; }
    public int BaseSeats { get; set; }
}
