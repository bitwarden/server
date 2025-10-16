using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class SavePolicyRequest
{
    [Required]
    public PolicyRequestModel Policy { get; set; } = null!;

    public Dictionary<string, object>? Metadata { get; set; }

    public async Task<SavePolicyModel> ToSavePolicyModelAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var policyUpdate = await Policy.ToPolicyUpdateAsync(organizationId, currentContext);
        var metadata = ValidateAndDeserializeMetadata();
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        return new SavePolicyModel(policyUpdate, performedBy, metadata);
    }

    private IPolicyMetadataModel ValidateAndDeserializeMetadata()
    {
        if (Metadata == null)
        {
            return new EmptyMetadataModel();
        }

        try
        {
            var json = JsonSerializer.Serialize(Metadata);

            return Policy.Type!.Value switch
            {
                PolicyType.OrganizationDataOwnership =>
                    CoreHelpers.LoadClassFromJsonData<OrganizationModelOwnershipPolicyModel>(json),
                _ => new EmptyMetadataModel()
            };
        }
        catch
        {
            return new EmptyMetadataModel();
        }
    }
}
