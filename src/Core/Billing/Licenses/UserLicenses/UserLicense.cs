using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Attributes;
using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Licenses.UserLicenses;

public class UserLicense : ILicense
{
    /// <summary>
    /// Represents the current version of the license format. Previously, the engineer who adds a property to this class
    /// would need to increment this constant. Now, if you add a property, you must simply add a new claim
    /// </summary>
    /// <remarks>Intentionally set one version behind to allow self-hosted users some time to update before
    /// getting out of date license errors</remarks>
    public const int CurrentLicenseFileVersion = 0;

    public string LicenseKey { get; set; }

    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public bool Premium { get; set; }

    public short? MaxStorageGb { get; set; }

    public int Version { get; set; }

    public DateTime? Expires { get; set; }

    public bool Trial { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public DateTime? Refresh { get; set; }

    [LicenseIgnore(Condition = LicenseIgnoreCondition.OnHash)]
    public string Hash { get; set; }

    [LicenseIgnore]
    public LicenseType LicenseType { get; } = LicenseType.User;

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [JsonIgnore]
    [LicenseIgnore]
    public byte[] SignatureBytes => Convert.FromBase64String(Signature);

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedData => this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version));

    [LicenseIgnore]
    [JsonIgnore]
    public byte[] EncodedHash =>
        SHA256.HashData(
            this.EncodeLicense(p => p.ShouldIncludePropertyOnLicense(Version, LicenseIgnoreCondition.OnHash)));

    public bool ValidLicenseVersion => Version is >= 1 and <= CurrentLicenseFileVersion + 1;
}
