using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class NotifyAdminsOfAcceptedUserCommand(
    IOrganizationUserRepository organizationUserRepository,
    IPushNotificationService pushNotificationService,
    IPolicyQuery policyQuery,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    ISendOrganizationAcceptedEmailCommand sendOrganizationAcceptedEmailCommand,
    IFeatureService featureService,
    ISchedulerFactory schedulerFactory,
    IHostApplicationLifetime hostApplicationLifetime)
    : INotifyAdminsOfAcceptedUserCommand
{
    public static readonly TimeSpan AcceptedEmailDelay = TimeSpan.FromMinutes(1);

    public async Task NotifyAsync(Organization organization, OrganizationUser organizationUser, User user)
    {
        if (!await IsAutoConfirmEnabledAsync(organization.Id, organizationUser))
        {
            await sendOrganizationAcceptedEmailCommand.SendAsync(organization, user.Email);
            return;
        }

        await PushAutoConfirmNotificationsAsync(organization.Id, organizationUser, user.Id);

        if (!featureService.IsEnabled(FeatureFlagKeys.AutoConfirmDelayAcceptedEmail))
        {
            await sendOrganizationAcceptedEmailCommand.SendAsync(organization, user.Email);
            return;
        }

        if (await TryScheduleAcceptedEmailAsync(organizationUser.Id))
        {
            return;
        }

        await sendOrganizationAcceptedEmailCommand.SendAsync(organization, user.Email);
    }

    private async Task<bool> IsAutoConfirmEnabledAsync(Guid organizationId, OrganizationUser organizationUser)
    {
        if (organizationUser.Type != OrganizationUserType.User)
        {
            return false;
        }

        var orgAbility = await organizationAbilityCacheService.GetOrganizationAbilityAsync(organizationId);
        if (orgAbility is not { UseAutomaticUserConfirmation: true })
        {
            return false;
        }

        var policy = await policyQuery.RunAsync(organizationId, PolicyType.AutomaticUserConfirmation);
        return policy.Enabled;
    }

    private async Task PushAutoConfirmNotificationsAsync(Guid organizationId, OrganizationUser organizationUser, Guid userId)
    {
        var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(
            organizationId,
            OrganizationUserType.Admin);

        var customUsersWithManagePermission = (await organizationUserRepository.GetManyDetailsByRoleAsync(
                organizationId,
                OrganizationUserType.Custom))
            .Where(c => c.GetPermissions()?.ManageUsers == true)
            .Select(c => c.UserId);

        var userIds = admins
            .Select(a => a.UserId)
            .Concat(customUsersWithManagePermission)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct();

        foreach (var adminUserId in userIds)
        {
            await pushNotificationService.PushAsync(
                new PushNotification<AutoConfirmPushNotification>
                {
                    Target = NotificationTarget.User,
                    TargetId = adminUserId,
                    Type = PushType.AutoConfirm,
                    Payload = new AutoConfirmPushNotification
                    {
                        UserId = adminUserId,
                        OrganizationId = organizationId,
                        TargetUserId = userId,
                        TargetOrganizationUserId = organizationUser.Id
                    },
                    ExcludeCurrentContext = false,
                });
        }
    }

    /// <remarks>
    /// Returns false when this host does not run the scheduler or is shutting down, so the email is not silently dropped.
    /// </remarks>
    private async Task<bool> TryScheduleAcceptedEmailAsync(Guid organizationUserId)
    {
        if (hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            return false;
        }

        var scheduler = await schedulerFactory.GetScheduler();
        if (!scheduler.IsStarted || scheduler.IsShutdown)
        {
            return false;
        }

        var job = JobBuilder.Create<SendOrganizationAcceptedEmailJob>()
            .UsingJobData(SendOrganizationAcceptedEmailJob.OrganizationUserIdKey, organizationUserId.ToString())
            .Build();

        var trigger = TriggerBuilder.Create()
            .StartAt(DateTimeOffset.UtcNow.Add(AcceptedEmailDelay))
            .Build();

        await scheduler.ScheduleJob(job, trigger);
        return true;
    }
}
