
namespace Bit.Core.KeyManagement.Models.Data;

public class SecurityStateData
{
    public required string SecurityState { get; set; }
    // The security version is included in the security state, but needs COSE parsing,
    // so this is a separate copy that can be used directly.
    public required int SecurityVersion { get; set; }
}
