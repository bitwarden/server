using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
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
    IOrganizationUserRepository organizationUserRepository,
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

        await SetDisabledForSendsByPolicyAsync(postUpsertedPolicyState, sendControlsPolicyData);
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

    private async Task SetDisabledForSendsByPolicyAsync(Policy postUpsertedPolicyState, SendControlsPolicyData sendControlsPolicyData)
    {
        var orgUsers = await organizationUserRepository.GetManyByOrganizationAsync(postUpsertedPolicyState.OrganizationId, null);
        var orgUserIds = orgUsers.Where(w => w.UserId != null).Select(s => s.UserId!.Value).ToList();
        var domains = (sendControlsPolicyData.AllowedDomains ?? "").Split(",").Select(d => d.Trim()).Where(d => d != "");
        var enabled = new List<Guid>();
        var enabledSendUserIds = new List<Guid>();
        var disabled = new List<Guid>();
        var disabledSendUserIds = new List<Guid>();
        foreach (var userId in orgUserIds)
        {
            var userSends = await sendRepository.GetManyByUserIdAsync(userId);
            var userHadSendsEnabled = false;
            var userHadSendsDisabled = false;
            foreach (var userSend in userSends)
            {
                // If the policy is no longer in effect then re-enable all Sends
                if (!postUpsertedPolicyState.Enabled)
                {
                    enabled.Add(userSend.Id);
                    userHadSendsEnabled = true;

                } else if (
                    sendControlsPolicyData.DisableSend ||
                    (sendControlsPolicyData.DisableHideEmail && (userSend.HideEmail ?? false)) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.PasswordProtected && userSend.AuthType != AuthType.Password) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.SpecificPeople && userSend.AuthType != AuthType.Email) ||
                    (sendControlsPolicyData.WhoCanAccess == SendWhoCanAccessType.SpecificPeople && domains.Any() && (userSend.Emails ?? "").Split(",").Select(e => e.Trim()).Any(e => !domains.Any(d => SendValidationService.SendEmailMatchesDomain(e, d)))))
                {
                    disabled.Add(userSend.Id);
                    userHadSendsDisabled = true;
                } else
                {
                    enabled.Add(userSend.Id);
                    userHadSendsEnabled = true;
                }
            }
            if (userHadSendsEnabled)
            {
                enabledSendUserIds.Add(userId);
            }
            if (userHadSendsDisabled)
            {
                disabledSendUserIds.Add(userId);
            }
        }
        if (enabled.Count > 0) {
            await sendRepository.UpdateManyDisabledAsync(enabled, false, enabledSendUserIds);
        }
        if (disabled.Count > 0)
        {
            await sendRepository.UpdateManyDisabledAsync(disabled, true, disabledSendUserIds);
        }
        return;
    }
}
