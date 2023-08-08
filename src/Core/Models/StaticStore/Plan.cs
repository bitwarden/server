using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore;

public abstract record Plan
{
    public PlanType Type { get; set; }
    public ProductType Product { get; set; }
    public string Name { get; set; }
    public bool IsAnnual { get; set; }
    public string NameLocalizationKey { get; set; }
    public string DescriptionLocalizationKey { get; set; }
    public bool CanBeUsedByBusiness { get; set; }
    public short? MaxUsers { get; set; }
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
    public bool HasCustomPermissions { get; set; }
    public int UpgradeSortOrder { get; set; }
    public int DisplaySortOrder { get; set; }
    public int? LegacyYear { get; set; }
    public bool Disabled { get; set; }
    public string StripePlanId { get; set; }
    public short? MaxProjects { get; set; }
    public PasswordManagerPlanFeatures PasswordManager { get; set; }
    public SecretsManagerPlanFeatures SecretsManager { get; set; }

}
