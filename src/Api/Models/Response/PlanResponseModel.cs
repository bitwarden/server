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
        BaseSeats = plan.BaseSeats;
        BaseStorageGb = plan.BaseStorageGb;
        MaxCollections = plan.MaxCollections;
        MaxUsers = plan.MaxUsers;
        HasAdditionalSeatsOption = plan.HasAdditionalSeatsOption;
        HasAdditionalStorageOption = plan.HasAdditionalStorageOption;
        MaxAdditionalSeats = plan.MaxAdditionalSeats;
        MaxAdditionalStorage = plan.MaxAdditionalStorage;
        HasPremiumAccessOption = plan.HasPremiumAccessOption;
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
        StripePlanId = plan.StripePlanId;
        StripeSeatPlanId = plan.StripeSeatPlanId;
        StripeStoragePlanId = plan.StripeStoragePlanId;
        BasePrice = plan.BasePrice;
        SeatPrice = plan.SeatPrice;
        AdditionalStoragePricePerGb = plan.AdditionalStoragePricePerGb;
        PremiumAccessOptionPrice = plan.PremiumAccessOptionPrice;
    }

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
