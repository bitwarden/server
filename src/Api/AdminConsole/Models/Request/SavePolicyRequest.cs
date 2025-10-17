using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Models.Request;

public class SavePolicyRequest
{
    [Required]
    public PolicyRequestModel Policy { get; set; } = null!;

    public Dictionary<string, object> Metadata { get; set; } = new();

    public async Task<SavePolicyModel> ToSavePolicyModelAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var policyUpdate = await Policy.ToPolicyUpdateAsync(organizationId, currentContext);
        var metadata = PolicyDataValidator.ValidateAndDeserializeMetadata(Metadata, Policy.Type!.Value);
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        return new SavePolicyModel(policyUpdate, performedBy, metadata);
    }
}
