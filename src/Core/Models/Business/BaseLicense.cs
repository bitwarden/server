using System.Text.Json.Serialization;
using Bit.Core.Billing.Licenses.Attributes;
using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public abstract class BaseLicense : ILicense
{
    [LicenseVersion(1)]
    public Guid Id { get; set; }

    [LicenseVersion(1)]
    public DateTime? Expires { get; set; }

    [LicenseVersion(1)]
    public int Version { get; set; }

    [LicenseVersion(1)]
    public bool Trial { get; set; }

    [LicenseVersion(1)]
    public string LicenseKey { get; set; }

    [LicenseVersion(1)]
    public string Name { get; set; }

    [LicenseIgnore]
    public string Signature { get; set; }

    [LicenseIgnore]
    public LicenseType LicenseType { get; set; }

    [LicenseIgnore]
    public string Token { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public abstract bool ValidLicenseVersion { get; }

    [LicenseIgnore(includeInSignature: true)]
    public DateTime? Refresh { get; set; }

    [LicenseIgnore(includeInSignature: true)]
    public DateTime Issued { get; set; }

    [LicenseIgnore(includeInSignature: true)]
    public string Hash { get; set; }
}
