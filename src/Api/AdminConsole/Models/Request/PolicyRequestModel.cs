// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Models.Request;

public class PolicyRequestModel
{
    [Required]
    public PolicyType? Type { get; set; }
    [Required]
    public bool? Enabled { get; set; }
    public Dictionary<string, object> Data { get; set; }

    public async Task<PolicyUpdate> ToPolicyUpdateAsync(Guid organizationId, ICurrentContext currentContext) => new()
    {
        Type = Type!.Value,
        OrganizationId = organizationId,
        Data = Data != null ? JsonSerializer.Serialize(Data) : null,
        Enabled = Enabled.GetValueOrDefault(),
        PerformedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId))
    };
}

public class PolicyVNextRequest
{

    [Required]
    public PolicyUpdate Data { get; set; }

    public Dictionary<string, object> Metadata { get; set; }

    public async Task<PolicyUpdate> ToPolicyUpdateAsync(Guid organizationId, ICurrentContext currentContext) => new()
    {
        // Type = Type!.Value,
        OrganizationId = organizationId,
        Data = Data != null ? JsonSerializer.Serialize(Data) : null,
        // Enabled = Enabled.GetValueOrDefault(),
        PerformedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId))
    };
}
