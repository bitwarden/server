using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

public class SendInviteRemindersCommand(
    IFeatureService featureService,
    IGlobalSettings globalSettings,
    TimeProvider timeProvider,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository,
    IOrgUserInviteTokenableFactory orgUserInviteTokenableFactory,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenFactory,
    IMailService mailService,
    IEventService eventService,
    ILogger<SendInviteRemindersCommand> logger) : ISendInviteRemindersCommand
{
    public async Task<SendInviteRemindersResult> SendInviteRemindersAsync()
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.AutoReinviteUsers))
        {
            logger.LogInformation("Invite reminder run skipped: feature flag {FeatureFlag} is disabled",
                FeatureFlagKeys.AutoReinviteUsers);
            return SendInviteRemindersResult.Empty;
        }

        var batchSize = GetBatchSize();
        if (batchSize == 0)
        {
            logger.LogInformation("Invite reminder run skipped: the batch size is 0");
            return SendInviteRemindersResult.Empty;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claimed = await organizationUserRepository.ClaimInviteReminderSequencesAsync(
            now,
            now.AddDays(-InviteReminderConstants.MaxLatenessDays),
            now.AddHours(InviteReminderSchedule.GetIntervalHours(globalSettings, logger)),
            batchSize);
        if (claimed.Count == 0)
        {
            return SendInviteRemindersResult.Empty;
        }

        if (claimed.Count == batchSize)
        {
            logger.LogInformation(
                "Invite reminder run filled the batch size of {BatchSize}. More reminders may be due.",
                batchSize);
        }

        var invitesInfoContext = await GetInvitesInfoContextAsync(claimed);

        var sentMembers = new List<OrganizationUserInviteReminder>();
        foreach (var group in claimed.GroupBy(m => (m.OrganizationId, ReminderNumber: (int)m.InviteRemindersSentCount)))
        {
            var members = group.ToList();
            if (!invitesInfoContext.Organizations.TryGetValue(group.Key.OrganizationId, out var organization))
            {
                logger.LogWarning(
                    "Invite reminder skipped for organization {OrganizationId}: the organization was not found",
                    group.Key.OrganizationId);
                continue;
            }

            try
            {
                var orgInvitesInfo = BuildOrganizationInvitesInfo(organization, members, invitesInfoContext);
                await mailService.SendOrganizationInviteReminderEmailsAsync(orgInvitesInfo, group.Key.ReminderNumber);
                sentMembers.AddRange(members);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Invite reminder mail call failed for organization {OrganizationId}: {MemberCount} members did not get reminder {ReminderNumber}",
                    group.Key.OrganizationId, members.Count, group.Key.ReminderNumber);
            }
        }

        await LogReminderEventsAsync(sentMembers, now);

        return new SendInviteRemindersResult(claimed.Count, claimed.Count - sentMembers.Count, sentMembers);
    }

    /// <summary>
    /// Writes 1 reminder event for each member who got their email, in 1 bulk call. The emails are already
    /// sent, so a failure here is logged instead of thrown.
    /// </summary>
    private async Task LogReminderEventsAsync(List<OrganizationUserInviteReminder> sentMembers, DateTime now)
    {
        if (sentMembers.Count == 0)
        {
            return;
        }

        try
        {
            // Invited members are not linked to a user yet, so the ID and organization ID are enough.
            await eventService.LogOrganizationUserEventsAsync(sentMembers.Select(m => (
                new OrganizationUser { Id = m.Id, OrganizationId = m.OrganizationId },
                EventType.OrganizationUser_InviteReminderSent,
                EventSystemUser.ScheduledJob,
                (DateTime?)now)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invite reminder events could not be written for {MemberCount} members",
                sentMembers.Count);
        }
    }

    private int GetBatchSize()
    {
        var batchSize = globalSettings.OrganizationInviteReminderBatchSize;
        if (batchSize >= 0)
        {
            return batchSize;
        }

        logger.LogWarning(
            "Invite reminder batch size {ConfiguredBatchSize} is below 0. Using the default of {DefaultBatchSize}.",
            batchSize, GlobalSettings.DefaultOrganizationInviteReminderBatchSize);
        return GlobalSettings.DefaultOrganizationInviteReminderBatchSize;
    }

    /// <summary>
    /// Loads everything the reminder emails need in a few bulk reads, before sending any mail.
    /// <see cref="InviteUsers.SendOrganizationInvitesCommand"/> does the same thing for a single organization.
    /// </summary>
    private async Task<InvitesInfoContext> GetInvitesInfoContextAsync(ICollection<OrganizationUserInviteReminder> claimed)
    {
        var organizations = (await organizationRepository.GetManyByIdsAsync(claimed.Select(m => m.OrganizationId).Distinct()))
            .ToDictionary(o => o.Id);

        var existingUsers = await userRepository.GetManyByEmailsAsync(claimed.Select(m => m.Email).Distinct());
        var existingUserEmails = new HashSet<string>(existingUsers.Select(u => u.Email),
            StringComparer.OrdinalIgnoreCase);

        var ssoEnabledOrganizationIds = await GetSsoEnabledOrganizationIdsAsync(organizations.Values);
        var requireSsoOrganizationIds = await GetRequireSsoOrganizationIdsAsync(organizations.Values, ssoEnabledOrganizationIds);

        return new InvitesInfoContext(organizations, existingUserEmails, ssoEnabledOrganizationIds, requireSsoOrganizationIds);
    }

    private async Task<HashSet<Guid>> GetSsoEnabledOrganizationIdsAsync(IEnumerable<Organization> organizations)
    {
        var useSsoOrganizationIds = organizations.Where(o => o.UseSso).Select(o => o.Id).ToList();
        if (useSsoOrganizationIds.Count == 0)
        {
            return [];
        }

        var ssoConfigs = await ssoConfigRepository.GetManyByOrganizationIdsAsync(useSsoOrganizationIds);
        return ssoConfigs.Where(c => c.Enabled).Select(c => c.OrganizationId).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetRequireSsoOrganizationIdsAsync(IEnumerable<Organization> organizations,
        HashSet<Guid> ssoEnabledOrganizationIds)
    {
        // Policy rows stay in place when an organization downgrades its plan, so only count the policy if the
        // organization can still use policies.
        var usePoliciesOrganizationIds = organizations
            .Where(o => o.UsePolicies && ssoEnabledOrganizationIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToList();
        if (usePoliciesOrganizationIds.Count == 0)
        {
            return [];
        }

        var policies = await policyRepository.GetManyByOrganizationIdsTypeAsync(usePoliciesOrganizationIds,
            PolicyType.RequireSso);
        return policies.Where(p => p.Enabled).Select(p => p.OrganizationId).ToHashSet();
    }

    private OrganizationInvitesInfo BuildOrganizationInvitesInfo(Organization organization,
        List<OrganizationUserInviteReminder> members, InvitesInfoContext context)
    {
        // The invite token and link only use the member's ID, organization ID, and email, so a partial
        // OrganizationUser is enough.
        var orgUsers = members.Select(m => new OrganizationUser
        {
            Id = m.Id,
            OrganizationId = m.OrganizationId,
            Email = m.Email,
            Status = OrganizationUserStatusType.Invited
        }).ToList();

        var orgUserHasExistingUserDict = orgUsers.ToDictionary(
            ou => ou.Id,
            ou => context.ExistingUserEmails.Contains(ou.Email!));

        var orgUserTokenPairs = orgUsers.Select(orgUser =>
        {
            var orgUserInviteTokenable = orgUserInviteTokenableFactory.CreateToken(orgUser);
            var protectedToken = dataProtectorTokenFactory.Protect(orgUserInviteTokenable);
            return (orgUser, new ExpiringToken(protectedToken, orgUserInviteTokenable.ExpirationDate));
        }).ToList();

        return new OrganizationInvitesInfo(
            organization,
            context.SsoEnabledOrganizationIds.Contains(organization.Id),
            context.RequireSsoOrganizationIds.Contains(organization.Id),
            orgUserTokenPairs,
            orgUserHasExistingUserDict);
    }

    private record InvitesInfoContext(
        Dictionary<Guid, Organization> Organizations,
        HashSet<string> ExistingUserEmails,
        HashSet<Guid> SsoEnabledOrganizationIds,
        HashSet<Guid> RequireSsoOrganizationIds);
}
