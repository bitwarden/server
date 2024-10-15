using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Attributes;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Licenses.OrganizationLicenses;

public class OrganizationLicense : ILicense
{
    /// <summary>
    /// Represents the current version of the license format. Previously, the engineer who adds a property to this class
    /// would need to increment this constant. Now, if you add a property, you must simply add a new claim
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self-hosted users some time to update before
    /// getting out of date license errors</remarks>
    public const int CurrentLicenseFileVersion = 14;

    [LicenseVersion(1)]
    public string LicenseKey { get; set; }

    [LicenseVersion(1)]
    public Guid InstallationId { get; set; }

    [LicenseVersion(1)]
    public Guid Id { get; set; }

    [LicenseVersion(1)]
    public string Name { get; set; }

    [LicenseVersion(1)]
    public string BillingEmail { get; set; }

    [LicenseVersion(1)]
    public string BusinessName { get; set; }

    [LicenseVersion(1)]
    public bool Enabled { get; set; }

    [LicenseVersion(1)]
    public string Plan { get; set; }

    [LicenseVersion(1)]
    public PlanType PlanType { get; set; }

    [LicenseVersion(1)]
    public int? Seats { get; set; }

    [LicenseVersion(1)]
    public short? MaxCollections { get; set; }

    [LicenseVersion(1)]
    public bool UseGroups { get; set; }

    [LicenseVersion(1)]
    public bool UseDirectory { get; set; }

    [LicenseVersion(1)]
    public bool UseTotp { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseVersion(1)]
    public bool SelfHost { get; set; }

    [LicenseVersion(1)]
    public int Version { get; set; }

    [LicenseVersion(1)]
    public DateTime? Expires { get; set; }

    [LicenseVersion(1)]
    public bool Trial { get; set; }

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

    [LicenseVersion(14)]
    public bool LimitCollectionCreationDeletion { get; set; } = true;

    [LicenseVersion(15)]
    public bool AllowAdminAccessToAllCollectionItems { get; set; } = true;

    [LicenseIgnore]
    public LicenseType LicenseType { get; } = LicenseType.Organization;

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public string Hash { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime? Refresh { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedData => this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version));

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedHash =>
        SHA256.HashData(
            this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version, LicenseIgnoreCondition.OnHash)));

    [LicenseIgnore]
    [JsonIgnore]
    public bool ValidLicenseVersion => Version is >= 1 and <= CurrentLicenseFileVersion + 1;
}
