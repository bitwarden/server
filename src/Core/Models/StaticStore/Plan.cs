using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore;

public abstract record Plan
{
    public PlanType Type { get; protected init; }
    public ProductType Product { get; protected init;}
    public string Name { get; protected init;}
    public bool IsAnnual { get; protected init;}
    public string NameLocalizationKey { get; protected init;}
    public string DescriptionLocalizationKey { get; protected init;}
    public bool CanBeUsedByBusiness { get; protected init;}
    public short? MaxUsers { get; protected init;}
    public int? TrialPeriodDays { get; protected init;}
    public bool HasSelfHost { get; protected init;}
    public bool HasPolicies { get; protected init;}
    public bool HasGroups { get; protected init;}
    public bool HasDirectory { get; protected init;}
    public bool HasEvents { get; protected init;}
    public bool HasTotp { get; protected init;}
    public bool Has2fa { get; protected init;}
    public bool HasApi { get; protected init;}
    public bool HasSso { get; protected init;}
    public bool HasKeyConnector { get; protected init;}
    public bool HasScim { get; protected init;}
    public bool HasResetPassword { get; protected init;}
    public bool UsersGetPremium { get; protected init;}
    public bool HasCustomPermissions { get; protected init;}
    public int UpgradeSortOrder { get; protected init;}
    public int DisplaySortOrder { get; protected init;}
    public int? LegacyYear { get; protected init;}
    public bool Disabled { get; protected init;}
    public PasswordManagerPlanFeatures PasswordManager { get; protected init;}
    public SecretsManagerPlanFeatures SecretsManager { get; protected init;}

    public record SecretsManagerPlanFeatures
    {
        public short? MaxServiceAccounts { get; protected init;}
        public bool AllowServiceAccountsAutoscale { get; protected init;}
        public string StripeServiceAccountPlanId { get; protected init;}
        public decimal? AdditionalPricePerServiceAccount { get; protected init;}
        public short? BaseServiceAccount { get;  protected init;}
        public short? MaxAdditionalServiceAccount { get; protected init;}
        public bool HasAdditionalServiceAccountOption { get; protected init;}
        public string StripeSeatPlanId { get; protected init;}
        public decimal BasePrice { get;  protected init;}
        public decimal SeatPrice { get; protected init;}
        public bool AllowSeatAutoscale { get; protected init;}
        public bool HasAdditionalSeatsOption { get; protected init;}
        public int? MaxAdditionalSeats { get; protected init;}
        public int BaseSeats { get; protected init;}
        public short? MaxProjects { get; protected init; }
        public int MaxUsers { get; protected init; }
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
