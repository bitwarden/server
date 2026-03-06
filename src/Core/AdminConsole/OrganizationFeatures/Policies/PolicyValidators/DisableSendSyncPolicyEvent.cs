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
        var policyUpdate = policyRequest.PolicyUpdate;

        var sendControlsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            policyUpdate.OrganizationId, PolicyType.SendControls) ?? new Policy
            {
                Id = CoreHelpers.GenerateComb(),
                OrganizationId = policyUpdate.OrganizationId,
                Type = PolicyType.SendControls,
            };

        var sendControlsPolicyData =
            sendControlsPolicy.GetDataModel<SendControlsPolicyData>();

        sendControlsPolicyData.DisableSend = postUpsertedPolicyState.Enabled;

        // TODO: seek clarification on review comment: sendControlsPolicyData.DisableHideEmail not mapped during this event
        // DisableHideEmail mapping should be handled in
        // src/Core/AdminConsole/OrganizationFeatures/Policies/PolicyValidators/SendOptionsSyncPolicyEvent.cs
        sendControlsPolicy.Enabled = sendControlsPolicyData.DisableSend || sendControlsPolicyData.DisableHideEmail;
        sendControlsPolicy.SetDataModel(sendControlsPolicyData);

        await policyRepository.UpsertAsync(sendControlsPolicy);
    }
}
