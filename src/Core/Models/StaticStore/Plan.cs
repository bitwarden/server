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
    protected PasswordManagerPlanFeatures PasswordManager { get; init;}
    protected SecretsManagerPlanFeatures SecretsManager { get; init;}

    protected record SecretsManagerPlanFeatures
    {
        public short? MaxServiceAccounts { get; init;}
        protected bool AllowServiceAccountsAutoscale { get; init;}
        protected string StripeServiceAccountPlanId { get; init;}
        protected decimal? AdditionalPricePerServiceAccount { get; init;}
        protected short? BaseServiceAccount { get; init;}
        protected short? MaxAdditionalServiceAccount { get; init;}
        protected bool HasAdditionalServiceAccountOption { get; init;}
        protected string StripeSeatPlanId { get; init;}
        protected decimal BasePrice { get; init;}
        protected decimal SeatPrice { get; init;}
        protected bool AllowSeatAutoscale { get; init;}
        protected bool HasAdditionalSeatsOption { get; init;}
        protected int? MaxAdditionalSeats { get; init;}
        protected int BaseSeats { get; init;}
        protected int MaxProjects { get; init; }
        protected int MaxUsers { get; init; }
    }

    protected record PasswordManagerPlanFeatures
    {
        protected short? BaseStorageGb { get; init;}
        protected short? MaxCollections { get; init;}
        protected bool HasAdditionalStorageOption { get; init;}
        protected short? MaxAdditionalStorage { get; init;}
        protected bool HasPremiumAccessOption { get; init;}
        protected int? LegacyYear { get; }
        protected string StripePremiumAccessPlanId { get; init;}
        protected decimal AdditionalStoragePricePerGb { get; init;}
        protected decimal PremiumAccessOptionPrice { get; init;}
        protected string StripeStoragePlanId { get; init;}
        public string StripePlanId { get; init;}
        protected string StripeSeatPlanId { get; init;}
        protected decimal BasePrice { get; init;}
        protected decimal SeatPrice { get; init;}
        protected bool AllowSeatAutoscale { get; init;}
        protected bool HasAdditionalSeatsOption { get; init;}
        protected int? MaxAdditionalSeats { get; init;}
        protected int BaseSeats { get; init;}
        protected int MaxUsers { get; init; }
    }
}
