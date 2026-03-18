using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyQuery(IPolicyRepository policyRepository) : IPolicyQuery
{
    public async Task<PolicyStatus> RunAsync(Guid organizationId, PolicyType policyType)
    {
        var dbPolicy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, policyType);

        if (dbPolicy == null && policyType == PolicyType.SendControls)
        {
            return await SynthesizeSendControlsStatusAsync(organizationId);
        }

        return new PolicyStatus(organizationId, policyType, dbPolicy);
    }

    /// <summary>
    /// When no SendControls policy row exists in the database, synthesizes a PolicyStatus
    /// from the legacy DisableSend and SendOptions policies. This supports lazy migration
    /// from the two legacy policies into the unified SendControls policy without requiring
    /// a database migration script.
    /// </summary>
    private async Task<PolicyStatus> SynthesizeSendControlsStatusAsync(Guid organizationId)
    {
        var disableSendPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.DisableSend);
        var sendOptionsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.SendOptions);

        var disableSend = disableSendPolicy?.Enabled ?? false;
        var disableHideEmail = sendOptionsPolicy?.Enabled == true
            && sendOptionsPolicy.GetDataModel<SendOptionsPolicyData>().DisableHideEmail;
        var enabled = disableSend || disableHideEmail;

        var data = new SendControlsPolicyData
        {
            DisableSend = disableSend,
            DisableHideEmail = disableHideEmail,
        };

        return new PolicyStatus(organizationId, PolicyType.SendControls)
        {
            Enabled = enabled,
            Data = CoreHelpers.ClassToJsonData(data),
        };
    }
}
