using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class SecurityStateModel
{
    [StringLength(1000)]
    [JsonPropertyName("securityState")]
    public required string SecurityState { get; set; }
    [JsonPropertyName("securityVersion")]
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
