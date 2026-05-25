using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.PrivilegedAccessManagement.Entities;

namespace Bit.Api.PrivilegedAccessManagement.Models.Request;

public class LeasingPolicyRequestModel
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public object Policy { get; set; } = null!;

    public LeasingPolicy ToLeasingPolicy(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        Name = Name,
        Description = Description,
        Policy = SerializePolicy(Policy),
    };

    private static string SerializePolicy(object policy) => policy switch
    {
        JsonElement je when je.ValueKind == JsonValueKind.Null => string.Empty,
        JsonElement je => je.GetRawText(),
        _ => JsonSerializer.Serialize(policy),
    };
}
