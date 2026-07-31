using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class FillAssistPolicyData : IPolicyDataModel
{
    [Required]
    [Url]
    [JsonPropertyName("rulesUrl")]
    public string? RulesUrl { get; set; }
}
