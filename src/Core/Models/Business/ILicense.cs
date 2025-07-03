using Bit.Core.Enums;

namespace Bit.Core.Models.Business;

public interface ILicense
{
    LicenseType LicenseType { get; set; }
    string LicenseKey { get; set; }
    int Version { get; set; }
    DateTime Issued { get; set; }
    DateTime? Refresh { get; set; }
    DateTime? Expires { get; set; }
    bool Trial { get; set; }
    string Hash { get; set; }
    string Signature { get; set; }
    string Token { get; set; }
    bool ValidLicenseVersion { get; }
}
