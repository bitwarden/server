using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Pam.Entities;

namespace Bit.Api.Pam.Models.Request;

public class AccessRuleRequestModel
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public object Conditions { get; set; } = null!;

    /// <summary>
    /// The complete set of collections this rule governs. The rule's associations are replaced to match
    /// exactly this set; an empty array clears all associations.
    /// </summary>
    [Required]
    public IEnumerable<Guid> Collections { get; set; } = null!;

    public AccessRule ToAccessRule(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        Description = Description,
        Conditions = SerializeConditions(Conditions),
    };

    private static string SerializeConditions(object conditions) => conditions switch
    {
        JsonElement je when je.ValueKind == JsonValueKind.Null => string.Empty,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(conditions),
    };
}
