using System.Text.Json.Serialization;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Attributes;

namespace Bit.Core.Models.Business;

public class OrganizationLicense : BaseLicense
{
    [Obsolete("No longer used in the JWT based license format")]
    public const int CurrentLicenseFileVersion = 15;

    [LicenseVersion(1)]
    public Guid InstallationId { get; set; }

    [LicenseVersion(1)]
    public int? Seats { get; set; }

    [LicenseVersion(1)]
    public short? MaxCollections { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseVersion(1)]
    public bool Enabled { get; set; }

    [LicenseVersion(1)]
    public bool SelfHost { get; set; }

    [LicenseVersion(1)]
    public bool UseDirectory { get; set; }

    [LicenseVersion(1)]
    public bool UseGroups { get; set; }

    [LicenseVersion(1)]
    public bool UseTotp { get; set; }

    [LicenseVersion(1)]
    public string BillingEmail { get; set; }

    [LicenseVersion(1)]
    public string BusinessName { get; set; }

    [LicenseVersion(1)]
    public string Plan { get; set; }

    [LicenseVersion(1)]
    public PlanType PlanType { get; set; }

    [LicenseVersion(2)]
    public bool UsersGetPremium { get; set; }

    [LicenseVersion(3)]
    public bool UseEvents { get; set; }

    [LicenseVersion(4)]
    public bool Use2fa { get; set; }

    [LicenseVersion(5)]
    public bool UseApi { get; set; }

    [LicenseVersion(6)]
    public bool UsePolicies { get; set; }

    [LicenseVersion(7)]
    public bool UseSso { get; set; }

    [LicenseVersion(8)]
    public bool UseResetPassword { get; set; }

    [LicenseVersion(9)]
    public bool UseKeyConnector { get; set; }

    [LicenseVersion(10)]
    public bool UseScim { get; set; }

    [LicenseVersion(11)]
    public bool UseCustomPermissions { get; set; }

    [LicenseVersion(12)]
    public DateTime? ExpirationWithoutGracePeriod { get; set; }

    [LicenseVersion(13)]
    public bool UsePasswordManager { get; set; }

    [LicenseVersion(13)]
    public bool UseSecretsManager { get; set; }

    [LicenseVersion(13)]
    public int? SmSeats { get; set; }

    [LicenseVersion(13)]
    public int? SmServiceAccounts { get; set; }

    // Deprecated. Left for backwards compatibility with old license versions.
    [LicenseVersion(14)]
    public bool LimitCollectionCreationDeletion { get; set; } = true;

    [LicenseVersion(15)]
    public bool AllowAdminAccessToAllCollectionItems { get; set; } = true;
    //

    [LicenseVersion(16)]
    public bool UseOrganizationDomains { get; set; }

    [LicenseIgnore]
    public bool UseAdminSponsoredFamilies { get; set; }

    [LicenseIgnore]
    public bool UseRiskInsights { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public override bool ValidLicenseVersion
    {
        get => Version is >= 1 and <= CurrentLicenseFileVersion + 1;
    }
}
