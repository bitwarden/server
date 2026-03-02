#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

/// <summary>
/// When the pm-31885-send-controls flag is active, syncs changes to the SendControls policy
/// back into the legacy DisableSend and SendOptions policy rows, enabling safe rollback.
/// </summary>
public class SendControlsSyncPolicyValidator(
    IPolicyRepository policyRepository,
    IFeatureService featureService) : IOnPolicyPostUpdateEvent
{
    public PolicyType Type => PolicyType.SendControls;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.SendControls))
        {
            return;
        }

        var data = CoreHelpers.LoadClassFromJsonData<SendControlsPolicyData>(postUpsertedPolicyState.Data)
            ?? new SendControlsPolicyData();

        await UpsertLegacyPolicyAsync(
            policyRequest.PolicyUpdate.OrganizationId,
            PolicyType.DisableSend,
            enabled: postUpsertedPolicyState.Enabled && data.DisableSend,
            policyData: null);

        var sendOptionsData = new SendOptionsPolicyData { DisableHideEmail = data.DisableHideEmail };
        await UpsertLegacyPolicyAsync(
            policyRequest.PolicyUpdate.OrganizationId,
            PolicyType.SendOptions,
            enabled: postUpsertedPolicyState.Enabled && data.DisableHideEmail,
            policyData: CoreHelpers.ClassToJsonData(sendOptionsData));
    }

    private async Task UpsertLegacyPolicyAsync(
        Guid organizationId,
        PolicyType type,
        bool enabled,
        string? policyData)
    {
        var existing = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, type);

        var policy = existing ?? new Policy
        {
            OrganizationId = organizationId,
            Type = type,
        };

        if (existing == null)
        {
            policy.SetNewId();
        }

        policy.Enabled = enabled;
        policy.Data = policyData;

        await policyRepository.UpsertAsync(policy);
    }
}
