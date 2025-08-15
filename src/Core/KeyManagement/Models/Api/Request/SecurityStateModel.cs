using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class SecurityStateModel
{
    [StringLength(1000)]
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
