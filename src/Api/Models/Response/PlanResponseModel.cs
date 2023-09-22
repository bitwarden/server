using Bit.Core.Enums;
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
        Product = plan.Product;
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
            SecretsManager = new SecretsManagerPlanResponseModel
            {
                MaxServiceAccounts = plan.SecretsManager.MaxServiceAccounts,
                AllowServiceAccountsAutoscale = plan.SecretsManager is { AllowServiceAccountsAutoscale: true },
                StripeServiceAccountPlanId = plan.SecretsManager.StripeServiceAccountPlanId,
                AdditionalPricePerServiceAccount = plan.SecretsManager.AdditionalPricePerServiceAccount,
                BaseServiceAccount = plan.SecretsManager.BaseServiceAccount,
                MaxAdditionalServiceAccount = plan.SecretsManager.MaxAdditionalServiceAccount,
                HasAdditionalServiceAccountOption = plan.SecretsManager is { HasAdditionalServiceAccountOption: true },
                StripeSeatPlanId = plan.SecretsManager.StripeSeatPlanId,
                HasAdditionalSeatsOption = plan.SecretsManager is { HasAdditionalSeatsOption: true },
                BasePrice = plan.SecretsManager.BasePrice,
                SeatPrice = plan.SecretsManager.SeatPrice,
                BaseSeats = plan.SecretsManager.BaseSeats,
                MaxSeats = plan.SecretsManager.MaxSeats,
                MaxAdditionalSeats = plan.SecretsManager.MaxAdditionalSeats,
                AllowSeatAutoscale = plan.SecretsManager.AllowSeatAutoscale,
                MaxProjects = plan.SecretsManager.MaxProjects
            };
        }

        PasswordManager = new PasswordManagerPlanResponseModel
        {
            StripePlanId = plan.PasswordManager.StripePlanId,
            StripeSeatPlanId = plan.PasswordManager.StripeSeatPlanId,
            BasePrice = plan.PasswordManager.BasePrice,
            SeatPrice = plan.PasswordManager.SeatPrice,
            AllowSeatAutoscale = plan.PasswordManager.AllowSeatAutoscale,
            HasAdditionalSeatsOption = plan.PasswordManager.HasAdditionalSeatsOption,
            MaxAdditionalSeats = plan.PasswordManager.MaxAdditionalSeats,
            BaseSeats = plan.PasswordManager.BaseSeats,
            HasPremiumAccessOption = plan.PasswordManager.HasPremiumAccessOption,
            StripePremiumAccessPlanId = plan.PasswordManager.StripePremiumAccessPlanId,
            PremiumAccessOptionPrice = plan.PasswordManager.PremiumAccessOptionPrice,
            MaxSeats = plan.PasswordManager.MaxSeats,
            BaseStorageGb = plan.PasswordManager.BaseStorageGb,
            HasAdditionalStorageOption = plan.PasswordManager.HasAdditionalStorageOption,
            AdditionalStoragePricePerGb = plan.PasswordManager.AdditionalStoragePricePerGb,
            StripeStoragePlanId = plan.PasswordManager.StripeStoragePlanId,
            MaxAdditionalStorage = plan.PasswordManager.MaxAdditionalStorage,
            MaxCollections = plan.PasswordManager.MaxCollections
        };
    }

    public PlanType Type { get; set; }
    public ProductType Product { get; set; }
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
    public SecretsManagerPlanResponseModel SecretsManager { get; protected init; }
    public PasswordManagerPlanResponseModel PasswordManager { get; protected init; }

    public class SecretsManagerPlanResponseModel
    {
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

    public record PasswordManagerPlanResponseModel
    {
        // Seats
        public string StripePlanId { get; init; }
        public string StripeSeatPlanId { get; init; }
        public decimal BasePrice { get; init; }
        public decimal SeatPrice { get; init; }
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
