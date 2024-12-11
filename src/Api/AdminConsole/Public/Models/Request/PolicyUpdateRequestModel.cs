using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class PolicyUpdateRequestModel : PolicyBaseModel
{
    public PolicyUpdate ToPolicyUpdate(Guid organizationId, PolicyType type) =>
        new()
        {
            Type = type,
            OrganizationId = organizationId,
            Data = Data != null ? JsonSerializer.Serialize(Data) : null,
            Enabled = Enabled.GetValueOrDefault(),
            PerformedBy = new SystemUser(EventSystemUser.PublicApi),
        };
}
