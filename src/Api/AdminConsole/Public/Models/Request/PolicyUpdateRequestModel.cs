using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class PolicyUpdateRequestModel : PolicyBaseModel
{
    public Policy ToPolicy(Guid orgId, PolicyType type)
    {
        return ToPolicy(new Policy
        {
            OrganizationId = orgId,
            Enabled = Enabled.GetValueOrDefault(),
            Data = Data != null ? JsonSerializer.Serialize(Data) : null,
            Type = type
        });
    }

    public virtual Policy ToPolicy(Policy existingPolicy)
    {
        existingPolicy.Enabled = Enabled.GetValueOrDefault();
        existingPolicy.Data = Data != null ? JsonSerializer.Serialize(Data) : null;
        return existingPolicy;
    }
}
