using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Syncs changes to the DisableSend policy into the SendControls policy row.
/// Runs regardless of the pm-31885-send-controls feature flag to ensure SendControls
/// always stays current for when the flag is eventually enabled.
/// </summary>
public class DisableSendSyncPolicyEvent(IPolicyRepository policyRepository) : IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.DisableSend;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        var organizationId = policyRequest.PolicyUpdate.OrganizationId;

        // Step 1: sync DisableSend.Enabled -> SendControlsPolicy.Data.DisableSend
        var sendControlsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.SendControls) ?? new Policy
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = organizationId,
                Type = PolicyType.SendControls,
            };

        var sendOptionsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            organizationId, PolicyType.SendOptions);

        var sendControlsPolicyData = sendControlsPolicy.GetDataModel<SendControlsPolicyData>();
        sendControlsPolicyData.DisableSend = postUpsertedPolicyState.Enabled;
        if (sendOptionsPolicy?.Enabled == true)
        {
            sendControlsPolicyData.DisableHideEmail =
                sendOptionsPolicy.GetDataModel<SendOptionsPolicyData>().DisableHideEmail;
        }

        sendControlsPolicy.SetDataModel(sendControlsPolicyData);

        // Step 2: sync Enabled status. SendControlsPolicy is enabled if either legacy policy is enabled
        sendControlsPolicy.Enabled = postUpsertedPolicyState.Enabled || (sendOptionsPolicy?.Enabled ?? false);

        await policyRepository.UpsertAsync(sendControlsPolicy);
    }
}
