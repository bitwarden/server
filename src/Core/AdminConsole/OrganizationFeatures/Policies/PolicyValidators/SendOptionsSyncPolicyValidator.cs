#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// Syncs changes to the SendOptions policy into the SendControls policy row.
/// Runs regardless of the pm-31885-send-controls feature flag to ensure SendControls
/// always stays current for when the flag is eventually enabled.
/// </summary>
public class SendOptionsSyncPolicyValidator(IPolicyRepository policyRepository) : IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.SendOptions;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        var policyUpdate = policyRequest.PolicyUpdate;

        var parsedSendOptions = postUpsertedPolicyState.Enabled
            ? CoreHelpers.LoadClassFromJsonData<SendOptionsPolicyData>(postUpsertedPolicyState.Data)
            : null;

        var sendControlsPolicy = await policyRepository.GetByOrganizationIdTypeAsync(
            policyUpdate.OrganizationId, PolicyType.SendControls);

        var data = sendControlsPolicy != null
            ? CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(sendControlsPolicy.Data) ?? new SendControlsPolicyData()
            : new SendControlsPolicyData();

        data.DisableHideEmail = parsedSendOptions?.DisableHideEmail ?? false;

        var policy = sendControlsPolicy ?? new Policy
        {
            OrganizationId = policyUpdate.OrganizationId,
            Type = PolicyType.SendControls,
        };

        if (sendControlsPolicy == null)
        {
            policy.SetNewId();
        }

        policy.Enabled = data.DisableSend || data.DisableHideEmail;
        policy.SetDataModel(data);

        await policyRepository.UpsertAsync(policy);
    }
}
