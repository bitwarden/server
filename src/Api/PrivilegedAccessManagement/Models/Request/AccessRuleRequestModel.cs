using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Api.PrivilegedAccessManagement.Models.Request;

public class AccessRuleRequestModel
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public object Rule { get; set; } = null!;

    public AccessRule ToAccessRule(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        Description = Description,
        Rule = SerializeRule(Rule),
    };

    private static string SerializeRule(object rule) => rule switch
    {
        JsonElement je when je.ValueKind == JsonValueKind.Null => string.Empty,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(rule),
    };
}
