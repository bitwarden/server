using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// When the pm-31885-send-controls flag is active, syncs changes to the SendControls policy
/// back into the legacy DisableSend and SendOptions policy rows, enabling safe rollback.
/// </summary>
public class SendControlsSyncPolicyEvent(
    IPolicyRepository policyRepository,
    TimeProvider timeProvider) : IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.SendControls;

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

        await UpsertLegacyPolicyAsync(
            policyRequest.PolicyUpdate.OrganizationId,
            PolicyType.DisableSend,
            enabled: postUpsertedPolicyState.Enabled && sendControlsPolicyData.DisableSend,
            policyData: null);

        var sendOptionsData = new SendOptionsPolicyData { DisableHideEmail = sendControlsPolicyData.DisableHideEmail };
        await UpsertLegacyPolicyAsync(
            policyRequest.PolicyUpdate.OrganizationId,
            PolicyType.SendOptions,
            enabled: postUpsertedPolicyState.Enabled && sendControlsPolicyData.DisableHideEmail,
            policyData: CoreHelpers.ClassToJsonData(sendOptionsData));
    }

    private async Task UpsertLegacyPolicyAsync(
        Guid organizationId,
        PolicyType type,
        bool enabled,
        string? policyData)
    {
        var existing = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, type);

        var policy = existing ?? new Policy { OrganizationId = organizationId, Type = type, };

        if (existing == null)
        {
            policy.SetNewId();
        }

        policy.Enabled = enabled;
        policy.Data = policyData;
        policy.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;

        await policyRepository.UpsertAsync(policy);
    }
}
