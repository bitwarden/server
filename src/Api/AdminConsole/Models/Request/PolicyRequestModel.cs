// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Utilities;

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

        var metadata = MapMetadata();

        return new SavePolicyModel(updatedPolicy, performedBy, metadata);
    }

    private IPolicyMetadataModel MapMetadata()
    {
        if (Metadata == null)
        {
            return new EmptyMetadataModel();
        }

        // Use JSON serialization to convert dictionary to specific metadata model
        if (Policy.Type == PolicyType.OrganizationDataOwnership)
        {
            try
            {
                var json = JsonSerializer.Serialize(Metadata);
                // var deserialized = JsonSerializer.Deserialize<OrganizationModelOwnershipPolicyModel>(json);
                // return deserialized != null ? deserialized : new EmptyMetadataModel();
                //
                return CoreHelpers.LoadClassFromJsonData<OrganizationModelOwnershipPolicyModel>(json);
            }
            catch
            {
                return new EmptyMetadataModel();
            }
        }

        // Default to empty metadata for other policy types
        return new EmptyMetadataModel();
    }
}

