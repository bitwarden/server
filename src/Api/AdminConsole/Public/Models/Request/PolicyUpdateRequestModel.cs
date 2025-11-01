using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class PolicyUpdateRequestModel : PolicyBaseModel
{
    public PolicyUpdate ToPolicyUpdate(Guid organizationId, PolicyType type)
    {
        var serializedData = PolicyDataValidator.ValidateAndSerialize(Data, type);

        return new()
        {
            Type = type,
            OrganizationId = organizationId,
            Data = serializedData,
            Enabled = Enabled.GetValueOrDefault(),
            PerformedBy = new SystemUser(EventSystemUser.PublicApi)
        };
    }
}
