using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore;

public class Plan
{
    public PlanType Type { get; set; }
    public ProductType Product { get; set; }
    public string Name { get; set; }
    public bool IsAnnual { get; set; }
    public string NameLocalizationKey { get; set; }
    public string DescriptionLocalizationKey { get; set; }
    public bool CanBeUsedByBusiness { get; set; }
    public int BaseSeats { get; set; }
    public short? BaseStorageGb { get; set; }
    public short? MaxCollections { get; set; }
    public short? MaxUsers { get; set; }
    public bool AllowSeatAutoscale { get; set; }

    public bool HasAdditionalSeatsOption { get; set; }
    public int? MaxAdditionalSeats { get; set; }
    public bool HasAdditionalStorageOption { get; set; }
    public short? MaxAdditionalStorage { get; set; }
    public bool HasPremiumAccessOption { get; set; }
    public int? TrialPeriodDays { get; set; }

    public bool HasSelfHost { get; set; }
    public bool HasPolicies { get; set; }
    public bool HasGroups { get; set; }
    public bool HasDirectory { get; set; }
    public bool HasEvents { get; set; }
    public bool HasTotp { get; set; }
    public bool Has2fa { get; set; }
    public bool HasApi { get; set; }
    public bool HasSso { get; set; }
    public bool HasKeyConnector { get; set; }
    public bool HasScim { get; set; }
    public bool HasResetPassword { get; set; }
    public bool UsersGetPremium { get; set; }

    public int UpgradeSortOrder { get; set; }
    public int DisplaySortOrder { get; set; }
    public int? LegacyYear { get; set; }
    public bool Disabled { get; set; }

    public string StripePlanId { get; set; }
    public string StripeSeatPlanId { get; set; }
    public string StripeStoragePlanId { get; set; }
    public string StripePremiumAccessPlanId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal SeatPrice { get; set; }
    public decimal AdditionalStoragePricePerGb { get; set; }
    public decimal PremiumAccessOptionPrice { get; set; }
}
