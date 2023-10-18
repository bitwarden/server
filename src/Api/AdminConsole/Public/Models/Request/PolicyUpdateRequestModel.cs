using System.Text.Json;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Public.Models.Request;

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
