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
        SupportsSecretsManager = plan.SupportsSecretsManager;
        SecretsManager = plan.SecretsManager;
        PasswordManager = plan.PasswordManager;
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
    public bool SupportsSecretsManager { get; set; }
    public Plan.PasswordManagerPlanFeatures PasswordManager { get; protected init; }
    public Plan.SecretsManagerPlanFeatures SecretsManager { get; protected init; }
}
