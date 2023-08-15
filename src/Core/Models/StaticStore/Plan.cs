using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore;

public abstract record Plan
{
    public PlanType Type { get; init; }
    public ProductType Product { get; init;}
    public string Name { get; init;}
    public bool IsAnnual { get; init;}
    public string NameLocalizationKey { get; init;}
    public string DescriptionLocalizationKey { get; init;}
    public bool CanBeUsedByBusiness { get;init;}
    public short? MaxUsers { get; init;}
    public int? TrialPeriodDays { get; init;}
    public bool HasSelfHost { get; init;}
    public bool HasPolicies { get; init;}
    public bool HasGroups { get; init;}
    public bool HasDirectory { get; init;}
    public bool HasEvents { get; init;}
    public bool HasTotp { get; init;}
    public bool Has2fa { get; init;}
    public bool HasApi { get; init;}
    public bool HasSso { get; init;}
    public bool HasKeyConnector { get; init;}
    public bool HasScim { get; init;}
    public bool HasResetPassword { get; init;}
    public bool UsersGetPremium { get; init;}
    public bool HasCustomPermissions { get; init;}
    public int UpgradeSortOrder { get; init;}
    public int DisplaySortOrder { get; init;}
    public int? LegacyYear { get; init;}
    public bool Disabled { get; init;}
    public short? MaxProjects { get; init;}
    public PasswordManagerPlanFeatures PasswordManager { get; init;}
    public SecretsManagerPlanFeatures SecretsManager { get; init;}

    public record SecretsManagerPlanFeatures
    {
        public short? MaxServiceAccounts { get; init;}
        public bool AllowServiceAccountsAutoscale { get; init;}
        public string StripeServiceAccountPlanId { get; init;}
        public decimal? AdditionalPricePerServiceAccount { get; init;}
        public short? BaseServiceAccount { get; init;}
        public short? MaxAdditionalServiceAccount { get; init;}
        public bool HasAdditionalServiceAccountOption { get; init;}
        public string StripeSeatPlanId { get; init;}
        public decimal BasePrice { get; init;}
        public decimal SeatPrice { get; init;}
        public bool AllowSeatAutoscale { get; init;}
        public bool HasAdditionalSeatsOption { get; init;}
        public int? MaxAdditionalSeats { get; init;}
        public int BaseSeats { get; init;}
        public int MaxProjects { get; init; }
        public int MaxUsers { get; init; }
    }

    public record PasswordManagerPlanFeatures
    {
        public short? BaseStorageGb { get; init;}
        public short? MaxCollections { get; init;}
        public bool HasAdditionalStorageOption { get; init;}
        public short? MaxAdditionalStorage { get; init;}
        public bool HasPremiumAccessOption { get; init;}
        public int? LegacyYear { get; }
        public string StripePremiumAccessPlanId { get; init;}
        public decimal AdditionalStoragePricePerGb { get; init;}
        public decimal PremiumAccessOptionPrice { get; init;}
        public string StripeStoragePlanId { get; init;}
        public string StripePlanId { get; init;}
        public string StripeSeatPlanId { get; init;}
        public decimal BasePrice { get; init;}
        public decimal SeatPrice { get; init;}
        public bool AllowSeatAutoscale { get; init;}
        public bool HasAdditionalSeatsOption { get; init;}
        public int? MaxAdditionalSeats { get; init;}
        public int BaseSeats { get; init;}
        public int MaxUsers { get; init; }
    }
}
