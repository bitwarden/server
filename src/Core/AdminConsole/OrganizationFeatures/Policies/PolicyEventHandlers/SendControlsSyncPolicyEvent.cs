using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;

/// <summary>
/// When the pm-31885-send-controls flag is active, syncs changes to the SendControls policy
/// back into the legacy DisableSend and SendOptions policy rows, enabling safe rollback.
/// </summary>
public class SendControlsSyncPolicyEvent(
    IPolicyRepository policyRepository,
    TimeProvider timeProvider,
    ISendRepository sendRepository,
    Bitwarden.Server.Sdk.Features.IFeatureService featureService,
    IOrganizationUserRepository orgUserRepository,
    IEventService eventService) : IOnPolicyPostUpdateEvent, IPolicyValidationEvent
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

        if (featureService.IsEnabled(FeatureFlagKeys.SendControlsExistingSends))
        {
            await UpdateSendsByPolicyAsync(postUpsertedPolicyState, sendControlsPolicyData);
        }
    }

    private async Task UpsertLegacyPolicyAsync<T>(
        Guid organizationId,
        PolicyType type,
        bool enabled,
        T? policyData) where T : IPolicyDataModel, new()
    {
        var existing = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, type);

        // Leave Id as default(Guid) for new policies so UpsertAsync routes to CreateAsync;
        // pre-assigning an Id causes UpsertAsync to attempt an UPDATE that silently affects 0 rows.
        var policy = existing ?? new Policy { OrganizationId = organizationId, Type = type, };

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

    // Enable or disable all Sends in an org based on whether they are compliant to org policy
    private async Task UpdateSendsByPolicyAsync(Policy postUpsertedPolicyState, SendControlsPolicyData sendControlsPolicyData)
    {
        var orgSendIds = await sendRepository.GetIdsByOrganizationIdAsync(postUpsertedPolicyState.OrganizationId);
        // We fetch all of the owners and admins in the org so we can ignore their Sends when enforcing policy compliance
        // This could be a heavy call in theory but in practice owners and admins should be a minority of org users
        var orgOwnerAndAdminUserIds = (await orgUserRepository.GetManyByMinimumRoleAsync(postUpsertedPolicyState.OrganizationId, Core.Enums.OrganizationUserType.Admin)).Select(oud => oud.GetUserId());
        foreach (var sendIdsChunk in orgSendIds.Chunk(50))
        {
            var sendsChunk = await sendRepository.GetManyByIdsAsync(sendIdsChunk);

            // If the Send was created by an Owner or an Admin in the organization we ignore it
            var toDisable = sendsChunk
                .Where(s => !s.Disabled && postUpsertedPolicyState.Enabled && !orgOwnerAndAdminUserIds.Contains(s.UserId) && SendIsNonCompliant(s, sendControlsPolicyData))
                .ToList();
            var toEnable = sendsChunk
                .Where(s => s.Disabled && (!postUpsertedPolicyState.Enabled || !SendIsNonCompliant(s, sendControlsPolicyData)))
                .ToList();

            await UpdateAndLogSendsAsync(toEnable, disabled: false, EventType.Send_PolicyEnabled, postUpsertedPolicyState.OrganizationId);
            await UpdateAndLogSendsAsync(toDisable, disabled: true, EventType.Send_PolicyDisabled, postUpsertedPolicyState.OrganizationId);
        }
    }

    private async Task UpdateAndLogSendsAsync(List<Send> sends, bool disabled, EventType eventType, Guid organizationId)
    {
        if (sends.Count == 0)
        {
            return;
        }

        await sendRepository.UpdateManyDisabledAsync(sends.Select(s => s.Id).ToList(), disabled);
        await eventService.LogSendEventsAsync(sends.Select(s => (s, eventType)), organizationId);
    }

    private static bool SendIsNonCompliant(Send send, SendControlsPolicyData policyData)
    {
        if (policyData.DisableSend)
        {
            return true;
        }
        if (policyData.DisableHideEmail && (send.HideEmail ?? false))
        {
            return true;
        }
        if (policyData.WhoCanAccess == SendWhoCanAccessType.PasswordProtected
            && send.AuthType != AuthType.Password)
        {
            return true;
        }
        if (policyData.WhoCanAccess == SendWhoCanAccessType.SpecificPeople)
        {
            if (send.AuthType != AuthType.Email)
            {
                return true;
            }
            try
            {
                if (policyData.AllowedDomains != null && !SendValidationService.SendAllEmailsHaveAllowedDomains(send.Emails, policyData.AllowedDomains))
                {
                    return true;
                }
            }
            catch (BadRequestException)
            {
                // Send data not sent from our clients may not have validation guaranteeing their
                // emails field contains valid email addresses. We can't verify such a Send against
                // the allowed-domains list, so treat it as non-compliant and disable it rather than
                // aborting the org-wide sweep.
                return true;
            }
        }
        if (policyData.AllowedSendTypes != null && !policyData.AllowedSendTypes.Contains(send.Type))
        {
            return true;
        }
        // We allow for up to a minute of skew in the difference between the deletion date and the creation date
        if (policyData.DeletionHours.HasValue && (send.DeletionDate.AddMinutes(-1) - send.CreationDate).TotalHours > policyData.DeletionHours.Value)
        {
            return true;
        }
        return false;
    }
}
