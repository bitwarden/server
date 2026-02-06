using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class PolicyUpdateRequestModel : PolicyBaseModel
{
    public Dictionary<string, object>? Metadata { get; set; }

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

    public SavePolicyModel ToSavePolicyModel(Guid organizationId, PolicyType type)
    {
        var serializedData = PolicyDataValidator.ValidateAndSerialize(Data, type);

        var policyUpdate = new PolicyUpdate
        {
            Type = type,
            OrganizationId = organizationId,
            Data = serializedData,
            Enabled = Enabled.GetValueOrDefault()
        };

        var performedBy = new SystemUser(EventSystemUser.PublicApi);
        var metadata = PolicyDataValidator.ValidateAndDeserializeMetadata(Metadata, type);

        return new SavePolicyModel(policyUpdate, performedBy, metadata);
    }
}
