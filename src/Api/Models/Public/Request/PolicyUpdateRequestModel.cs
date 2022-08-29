using System.Text.Json;
using Bit.Core.Entities;

namespace Bit.Api.Models.Public.Request;

public class PolicyUpdateRequestModel : PolicyBaseModel
{
    public Policy ToPolicy(Guid orgId)
    {
        return ToPolicy(new Policy
        {
            OrganizationId = orgId
        });
    }

    public virtual Policy ToPolicy(Policy existingPolicy)
    {
        existingPolicy.Enabled = Enabled.GetValueOrDefault();
        existingPolicy.Data = Data != null ? JsonSerializer.Serialize(Data) : null;
        return existingPolicy;
    }
}
