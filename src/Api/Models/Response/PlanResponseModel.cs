using Bit.Core.Billing.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.StaticStore;

namespace Bit.Api.Models.Response;

public class PlanResponseModel : ResponseModel
{
    public PlanResponseModel(Plan plan, string obj = "plan")
        : base(obj)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        Type = plan.Type;
        ProductTier = plan.ProductTier;
        Name = plan.Name;
        IsAnnual = plan.IsAnnual;
        NameLocalizationKey = plan.NameLocalizationKey;
        DescriptionLocalizationKey = plan.DescriptionLocalizationKey;
        CanBeUsedByBusiness = plan.CanBeUsedByBusiness;
        TrialPeriodDays = plan.TrialPeriodDays;
        HasSelfHost = plan.HasSelfHost;
        HasPolicies = plan.HasPolicies;
        HasGroups = plan.HasGroups;
        HasDirectory = plan.HasDirectory;
        HasEvents = plan.HasEvents;
        HasTotp = plan.HasTotp;
        Has2fa = plan.Has2fa;
        HasSso = plan.HasSso;
        HasResetPassword = plan.HasResetPassword;
        UsersGetPremium = plan.UsersGetPremium;
        UpgradeSortOrder = plan.UpgradeSortOrder;
        DisplaySortOrder = plan.DisplaySortOrder;
        LegacyYear = plan.LegacyYear;
        Disabled = plan.Disabled;
        if (plan.SecretsManager != null)
        {
            SecretsManager = new SecretsManagerPlanFeaturesResponseModel(plan.SecretsManager);
        }

        PasswordManager = new PasswordManagerPlanFeaturesResponseModel(plan.PasswordManager);
    }

    public PlanType Type { get; set; }
    public ProductTierType ProductTier { get; set; }
    public string Name { get; set; }
    public bool IsAnnual { get; set; }
    public string NameLocalizationKey { get; set; }
    public string DescriptionLocalizationKey { get; set; }
    public bool CanBeUsedByBusiness { get; set; }
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
    public bool HasResetPassword { get; set; }
    public bool UsersGetPremium { get; set; }

    public int UpgradeSortOrder { get; set; }
    public int DisplaySortOrder { get; set; }
    public int? LegacyYear { get; set; }
    public bool Disabled { get; set; }
    public SecretsManagerPlanFeaturesResponseModel SecretsManager { get; protected init; }
    public PasswordManagerPlanFeaturesResponseModel PasswordManager { get; protected init; }

    public class SecretsManagerPlanFeaturesResponseModel
    {
        public SecretsManagerPlanFeaturesResponseModel(Plan.SecretsManagerPlanFeatures plan)
        {
            MaxServiceAccounts = plan.MaxServiceAccounts;
            AllowServiceAccountsAutoscale = plan is { AllowServiceAccountsAutoscale: true };
            StripeServiceAccountPlanId = plan.StripeServiceAccountPlanId;
            AdditionalPricePerServiceAccount = plan.AdditionalPricePerServiceAccount;
            BaseServiceAccount = plan.BaseServiceAccount;
            MaxAdditionalServiceAccount = plan.MaxAdditionalServiceAccount;
            HasAdditionalServiceAccountOption = plan is { HasAdditionalServiceAccountOption: true };
            StripeSeatPlanId = plan.StripeSeatPlanId;
            HasAdditionalSeatsOption = plan is { HasAdditionalSeatsOption: true };
            BasePrice = plan.BasePrice;
            SeatPrice = plan.SeatPrice;
            BaseSeats = plan.BaseSeats;
            MaxSeats = plan.MaxSeats;
            MaxAdditionalSeats = plan.MaxAdditionalSeats;
            AllowSeatAutoscale = plan.AllowSeatAutoscale;
            MaxProjects = plan.MaxProjects;
        }
        // Service accounts
        public short? MaxServiceAccounts { get; init; }
        public bool AllowServiceAccountsAutoscale { get; init; }
        public string StripeServiceAccountPlanId { get; init; }
        public decimal? AdditionalPricePerServiceAccount { get; init; }
        public short? BaseServiceAccount { get; init; }
        public short? MaxAdditionalServiceAccount { get; init; }
        public bool HasAdditionalServiceAccountOption { get; init; }
        // Seats
        public string StripeSeatPlanId { get; init; }
        public bool HasAdditionalSeatsOption { get; init; }
        public decimal BasePrice { get; init; }
        public decimal SeatPrice { get; init; }
        public int BaseSeats { get; init; }
        public short? MaxSeats { get; init; }
        public int? MaxAdditionalSeats { get; init; }
        public bool AllowSeatAutoscale { get; init; }

        // Features
        public int MaxProjects { get; init; }
    }

    public record PasswordManagerPlanFeaturesResponseModel
    {
        public PasswordManagerPlanFeaturesResponseModel(Plan.PasswordManagerPlanFeatures plan)
        {
            StripePlanId = plan.StripePlanId;
            StripeSeatPlanId = plan.StripeSeatPlanId;
            StripeProviderPortalSeatPlanId = plan.StripeProviderPortalSeatPlanId;
            BasePrice = plan.BasePrice;
            SeatPrice = plan.SeatPrice;
            ProviderPortalSeatPrice = plan.ProviderPortalSeatPrice;
            AllowSeatAutoscale = plan.AllowSeatAutoscale;
            HasAdditionalSeatsOption = plan.HasAdditionalSeatsOption;
            MaxAdditionalSeats = plan.MaxAdditionalSeats;
            BaseSeats = plan.BaseSeats;
            HasPremiumAccessOption = plan.HasPremiumAccessOption;
            StripePremiumAccessPlanId = plan.StripePremiumAccessPlanId;
            PremiumAccessOptionPrice = plan.PremiumAccessOptionPrice;
            MaxSeats = plan.MaxSeats;
            BaseStorageGb = plan.BaseStorageGb;
            HasAdditionalStorageOption = plan.HasAdditionalStorageOption;
            AdditionalStoragePricePerGb = plan.AdditionalStoragePricePerGb;
            StripeStoragePlanId = plan.StripeStoragePlanId;
            MaxAdditionalStorage = plan.MaxAdditionalStorage;
            MaxCollections = plan.MaxCollections;
        }
        // Seats
        public string StripePlanId { get; init; }
        public string StripeSeatPlanId { get; init; }
        public string StripeProviderPortalSeatPlanId { get; init; }
        public decimal BasePrice { get; init; }
        public decimal SeatPrice { get; init; }
        public decimal ProviderPortalSeatPrice { get; init; }
        public bool AllowSeatAutoscale { get; init; }
        public bool HasAdditionalSeatsOption { get; init; }
        public int? MaxAdditionalSeats { get; init; }
        public int BaseSeats { get; init; }
        public bool HasPremiumAccessOption { get; init; }
        public string StripePremiumAccessPlanId { get; init; }
        public decimal PremiumAccessOptionPrice { get; init; }
        public short? MaxSeats { get; init; }
        // Storage
        public short? BaseStorageGb { get; init; }
        public bool HasAdditionalStorageOption { get; init; }
        public decimal AdditionalStoragePricePerGb { get; init; }
        public string StripeStoragePlanId { get; init; }
        public short? MaxAdditionalStorage { get; init; }
        // Feature
        public short? MaxCollections { get; init; }
    }
}
