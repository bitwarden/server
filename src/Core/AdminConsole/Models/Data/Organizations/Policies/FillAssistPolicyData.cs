using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class FillAssistPolicyData : IPolicyDataModel
{
    [Required]
    [Url]
    [RegularExpression("(?i)^https://.*", ErrorMessage = "RulesUrl must use HTTPS.")]
    [JsonPropertyName("rulesUrl")]
    public string? RulesUrl { get; set; }
}
