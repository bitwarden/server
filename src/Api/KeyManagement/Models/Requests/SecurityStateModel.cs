#nullable enable
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.KeyManagement.Models.Requests;

public class SecurityStateModel
{
    public required string SecurityState { get; set; }
    public required int SecurityVersion { get; set; }

    public SecurityStateData ToSecurityState()
    {
        return new SecurityStateData
        {
            SecurityState = SecurityState,
            SecurityVersion = SecurityVersion
        };
    }

    public static SecurityStateModel FromSecurityStateData(SecurityStateData data)
    {
        return new SecurityStateModel
        {
            SecurityState = data.SecurityState,
            SecurityVersion = data.SecurityVersion
        };
    }
}
