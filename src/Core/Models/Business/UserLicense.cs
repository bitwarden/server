using System.Text.Json.Serialization;
using Bit.Core.Billing.Licenses.Attributes;

namespace Bit.Core.Models.Business;

public class UserLicense : BaseLicense
{
    [LicenseVersion(1)]
    public string Email { get; set; }

    [LicenseVersion(1)]
    public bool Premium { get; set; }

    [LicenseVersion(1)]
    public short? MaxStorageGb { get; set; }

    [LicenseIgnore]
    [JsonIgnore]
    public override bool ValidLicenseVersion
    {
        get => Version == 1;
    }
}
