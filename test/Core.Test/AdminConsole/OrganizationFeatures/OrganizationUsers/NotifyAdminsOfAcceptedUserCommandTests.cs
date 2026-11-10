using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Quartz;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class NotifyAdminsOfAcceptedUserCommandTests
{
    private static IScheduler SetupPassingGuards(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser)
    {
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = true });

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(new PolicyStatus(organization.Id, PolicyType.AutomaticUserConfirmation,
                new Policy { Enabled = true }));

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutoConfirmDelayAcceptedEmail)
            .Returns(true);

        var scheduler = Substitute.For<IScheduler>();
        scheduler.IsStarted.Returns(true);
        sutProvider.GetDependency<ISchedulerFactory>()
            .GetScheduler(Arg.Any<CancellationToken>())
            .Returns(scheduler);

        orgUser.Type = OrganizationUserType.User;

        return scheduler;
    }

    private static async Task AssertAcceptedEmailSentAsync(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        User user,
        OrganizationUserUserDetails admin)
    {
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationAcceptedEmailAsync(
                organization,
                user.Email,
                Arg.Is<IEnumerable<string>>(emails => emails.Contains(admin.Email)));
    }

    private static async Task AssertNoPushOrScheduledEmailAsync(SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider)
    {
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<AutoConfirmPushNotification>>());
        await sutProvider.GetDependency<ISchedulerFactory>()
            .DidNotReceiveWithAnyArgs()
            .GetScheduler(Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_AutoConfirmEnabled_SchedulesAcceptedEmailInsteadOfSendingIt(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user)
    {
        // Arrange
        var scheduler = SetupPassingGuards(sutProvider, organization, orgUser);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j =>
                j.JobType == typeof(SendOrganizationAcceptedEmailJob) &&
                j.JobDataMap.GetString(SendOrganizationAcceptedEmailJob.OrganizationUserIdKey) == orgUser.Id.ToString()),
            Arg.Is<ITrigger>(t => t.StartTimeUtc > DateTimeOffset.UtcNow),
            Arg.Any<CancellationToken>());

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationAcceptedEmailAsync(default!, default!, default!);
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_AutoConfirmEnabled_FeatureFlagDisabled_SendsPushAndAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        var scheduler = SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AutoConfirmDelayAcceptedEmail)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Any<PushNotification<AutoConfirmPushNotification>>());
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_AutoConfirmEnabled_SchedulerNotStarted_SendsAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        var scheduler = SetupPassingGuards(sutProvider, organization, orgUser);
        scheduler.IsStarted.Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_AutoConfirmEnabled_SchedulerShutDown_SendsAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        var scheduler = SetupPassingGuards(sutProvider, organization, orgUser);
        scheduler.IsShutdown.Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_AutoConfirmEnabled_ApplicationStopping_SendsAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        var scheduler = SetupPassingGuards(sutProvider, organization, orgUser);

        using var stoppingTokenSource = new CancellationTokenSource();
        stoppingTokenSource.Cancel();
        sutProvider.GetDependency<IHostApplicationLifetime>()
            .ApplicationStopping
            .Returns(stoppingTokenSource.Token);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_SendsNotificationToAdminsAndOwners(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        List<OrganizationUserUserDetails> admins)
    {
        // Arrange
        foreach (var admin in admins)
        {
            admin.UserId = Guid.NewGuid();
        }

        SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails>());

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(admins.Count)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organization.Id &&
                pn.Payload.TargetUserId == user.Id &&
                pn.Payload.TargetOrganizationUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_SendsNotificationToCustomUsersWithManageUsersPermission(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        List<OrganizationUserUserDetails> customUsers)
    {
        // Arrange
        foreach (var customUser in customUsers)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":true}";
        }

        SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(customUsers);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(customUsers.Count)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organization.Id &&
                pn.Payload.TargetUserId == user.Id &&
                pn.Payload.TargetOrganizationUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_DoesNotSendToCustomUsersWithoutManageUsersPermission(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        List<OrganizationUserUserDetails> customUsers)
    {
        // Arrange
        foreach (var customUser in customUsers)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":false}";
        }

        SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails>());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(customUsers);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<AutoConfirmPushNotification>>());
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_SendsToAdminsAndCustomUsersWithManageUsers(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        List<OrganizationUserUserDetails> admins,
        List<OrganizationUserUserDetails> customUsersWithPermission,
        List<OrganizationUserUserDetails> customUsersWithoutPermission)
    {
        // Arrange
        foreach (var admin in admins)
        {
            admin.UserId = Guid.NewGuid();
        }

        foreach (var customUser in customUsersWithPermission)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":true}";
        }

        foreach (var customUser in customUsersWithoutPermission)
        {
            customUser.UserId = Guid.NewGuid();
            customUser.Permissions = "{\"manageUsers\":false}";
        }

        SetupPassingGuards(sutProvider, organization, orgUser);

        var allCustomUsers = customUsersWithPermission.Concat(customUsersWithoutPermission).ToList();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(allCustomUsers);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        var expectedNotificationCount = admins.Count + customUsersWithPermission.Count;
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(expectedNotificationCount)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm &&
                pn.Target == NotificationTarget.User &&
                pn.Payload.OrganizationId == organization.Id &&
                pn.Payload.TargetUserId == user.Id &&
                pn.Payload.TargetOrganizationUserId == orgUser.Id &&
                pn.ExcludeCurrentContext == false));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_SkipsUsersWithoutUserId(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        List<OrganizationUserUserDetails> admins)
    {
        // Arrange
        admins[0].UserId = Guid.NewGuid();
        admins[1].UserId = null;
        admins[2].UserId = Guid.NewGuid();

        SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(admins);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails>());

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(2)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.Type == PushType.AutoConfirm));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_DeduplicatesUserIds(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        Guid duplicateUserId)
    {
        // Arrange
        var admin1 = new OrganizationUserUserDetails { UserId = duplicateUserId };
        var admin2 = new OrganizationUserUserDetails { UserId = duplicateUserId };
        var customUser = new OrganizationUserUserDetails
        {
            UserId = duplicateUserId,
            Permissions = "{\"manageUsers\":true}"
        };

        SetupPassingGuards(sutProvider, organization, orgUser);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns(new List<OrganizationUserUserDetails> { admin1, admin2 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByRoleAsync(organization.Id, OrganizationUserType.Custom)
            .Returns(new List<OrganizationUserUserDetails> { customUser });

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<AutoConfirmPushNotification>>(pn =>
                pn.TargetId == duplicateUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_OrgAbilityIsNull_SendsAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        orgUser.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns((OrganizationAbility?)null);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await AssertNoPushOrScheduledEmailAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task NotifyAsync_UseAutomaticUserConfirmationDisabled_SendsAcceptedEmailImmediately(
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        orgUser.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = false });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await AssertNoPushOrScheduledEmailAsync(sutProvider);
    }

    [Theory, BitAutoData]
    public async Task NotifyAsync_PolicyDisabled_SendsAcceptedEmailImmediately(
        [Policy(PolicyType.AutomaticUserConfirmation, false)] PolicyStatus disabledPolicy,
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        orgUser.Type = OrganizationUserType.User;

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility { UseAutomaticUserConfirmation = true });

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.AutomaticUserConfirmation)
            .Returns(disabledPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await AssertNoPushOrScheduledEmailAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task NotifyAsync_TargetUserIsNotMember_SendsAcceptedEmailImmediately(
        OrganizationUserType privilegedType,
        SutProvider<NotifyAdminsOfAcceptedUserCommand> sutProvider,
        Organization organization,
        OrganizationUser orgUser,
        User user,
        OrganizationUserUserDetails admin)
    {
        // Arrange
        SetupPassingGuards(sutProvider, organization, orgUser);
        orgUser.Type = privilegedType;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)
            .Returns([admin]);

        // Act
        await sutProvider.Sut.NotifyAsync(organization, orgUser, user);

        // Assert
        await AssertAcceptedEmailSentAsync(sutProvider, organization, user, admin);
        await AssertNoPushOrScheduledEmailAsync(sutProvider);
    }
}
