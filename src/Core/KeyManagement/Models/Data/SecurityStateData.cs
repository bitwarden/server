
namespace Bit.Core.KeyManagement.Models.Data;

public class SecurityStateData
{
    public required string SecurityState { get; set; }
    // The security version is included in the security state, but needs COSE parsing,
    // so this is a separate copy that can be used directly.
    public required int SecurityVersion { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not SecurityStateData other)
        {
            return false;
        }

        return SecurityState == other.SecurityState &&
               SecurityVersion == other.SecurityVersion;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SecurityState, SecurityVersion);
    }
}
