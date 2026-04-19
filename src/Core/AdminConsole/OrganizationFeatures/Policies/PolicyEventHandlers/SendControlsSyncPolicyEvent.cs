using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

/// <summary>
/// When the pm-31885-send-controls flag is active, syncs changes to the SendControls policy
/// back into the legacy DisableSend and SendOptions policy rows, enabling safe rollback.
/// </summary>
public class SendControlsSyncPolicyEvent(
    IPolicyRepository policyRepository,
    TimeProvider timeProvider,
    ISendRepository sendRepository) : IOnPolicyPostUpdateEvent, IPolicyValidationEvent
{
    public PolicyType Type => PolicyType.SendControls;

    public async Task ExecutePostUpsertSideEffectAsync(
        SavePolicyModel policyRequest,
        Policy postUpsertedPolicyState,
        Policy? previousPolicyState)
    {
        var sendControlsPolicyData =
            postUpsertedPolicyState.GetDataModel<SendControlsPolicyData>();

        await UpsertLegacyPolicyAsync<SendControlsPolicyData>(
            postUpsertedPolicyState.OrganizationId,
            PolicyType.DisableSend,
            enabled: postUpsertedPolicyState.Enabled && sendControlsPolicyData.DisableSend,
            policyData: null);

        var sendOptionsData = new SendOptionsPolicyData { DisableHideEmail = sendControlsPolicyData.DisableHideEmail };
        await UpsertLegacyPolicyAsync(
            postUpsertedPolicyState.OrganizationId,
            PolicyType.SendOptions,
            enabled: postUpsertedPolicyState.Enabled && sendControlsPolicyData.DisableHideEmail,
            policyData: sendOptionsData);

        await UpdateSendsByPolicyAsync(postUpsertedPolicyState, sendControlsPolicyData);
    }

    private async Task UpsertLegacyPolicyAsync<T>(
        Guid organizationId,
        PolicyType type,
        bool enabled,
        T? policyData) where T : IPolicyDataModel, new()
    {
        var existing = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, type);

        var policy = existing ?? new Policy { OrganizationId = organizationId, Type = type, };

        if (existing == null)
        {
            policy.SetNewId();
        }

        policy.Enabled = enabled;
        if (policyData != null)
        {
            policy.SetDataModel(policyData);
        }
        policy.RevisionDate = timeProvider.GetUtcNow().UtcDateTime;

        await policyRepository.UpsertAsync(policy);
    }

    public Task<string> ValidateAsync(SavePolicyModel policyRequest, Policy? currentPolicy)
    {
        var dataModel = policyRequest.PolicyUpdate.GetDataModel<SendControlsPolicyData>();
        if (dataModel.AllowedDomains is not null && dataModel.WhoCanAccess != SendWhoCanAccessType.SpecificPeople)
        {
            return Task.FromResult("Allowed domains can only be set when the required access type is set to specific people");
        }
        return Task.FromResult(string.Empty);
    }

    private async Task UpdateSendsByPolicyAsync(Policy postUpsertedPolicyState, SendControlsPolicyData sendControlsPolicyData)
    {
        var orgSendIds = await sendRepository.GetIdsByOrganizationIdAsync(postUpsertedPolicyState.OrganizationId);
        foreach (var sendIdsChunk in orgSendIds.Chunk(50))
        {
            var enabled = new List<Guid>();
            var disabled = new List<Guid>();
            var sendsChunk = await sendRepository.GetManyByIdsAsync(sendIdsChunk);
            foreach (var send in sendsChunk)
            {
                if (
                    // If the policy is disabled then we want to re-enable any Sends that were previously disabled
                    postUpsertedPolicyState.Enabled && 
                    (sendControlsPolicyData.DisableSend ||
                    (sendControlsPolicyData.DisableHideEmail && (send.HideEmail ?? false)) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.PasswordProtected && send.AuthType != AuthType.Password) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.SpecificPeople && send.AuthType != AuthType.Email) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.SpecificPeople && !SendValidationService.SendAllEmailsHaveAllowedDomains(send.Emails, sendControlsPolicyData.AllowedDomains))))
                {
                    disabled.Add(send.Id);
                } else
                {
                    enabled.Add(send.Id);
                }
            }
            if (enabled.Count > 0) {
                await sendRepository.UpdateManyDisabledAsync(enabled, false);
            }
            if (disabled.Count > 0)
            {
                await sendRepository.UpdateManyDisabledAsync(disabled, true);
            }
        }
    }
}
