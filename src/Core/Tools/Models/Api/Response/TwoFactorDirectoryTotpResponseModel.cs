using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Core.Tools.Models.Api.Response;

public class TwoFactorDirectoryTotpResponseModel
{
    [Required]
    [JsonPropertyName("domain")]
    public string Domain { get; set; }
    [JsonPropertyName("documentation")]
    public string Documentation { get; set; }
    [JsonPropertyName("additional-domains")]
    public IEnumerable<string> AdditionalDomains { get; set; }
}
