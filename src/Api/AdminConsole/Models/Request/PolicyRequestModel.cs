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
// Jimmy todo: Make sure to move the classes into their own files.


public class SavePolicyRequest
{
    [Required]
    public PolicyRequestModel Policy { get; set; }

    // public IPolicyMetadataModel Metadata { get; set; }

    public Dictionary<string, object> Metadata { get; set; }

    public async Task<SavePolicyModel> ToSavePolicyModelAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        var updatedPolicy = new PolicyUpdate()
        {
            Type = Policy.Type!.Value,
            OrganizationId = organizationId,
            Data = Policy.Data != null ? JsonSerializer.Serialize(Policy.Data) : null,
            Enabled = Policy.Enabled.GetValueOrDefault(),
        };

        var metadata = Metadata != null ? JsonSerializer.Serialize(Metadata) : new EmptyMetadataModel();

        return new SavePolicyModel(updatedPolicy, performedBy, metadata);
    }
}


public class SavePolicyRequestTest
{
    [Required]
    public string test { get; set; }

    public async Task<SavePolicyModel> ToSavePolicyModelAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));
        // Data.OrganizationId = organizationId;
        return new SavePolicyModel(new PolicyUpdate(), performedBy, new EmptyMetadataModel());
    }

}
