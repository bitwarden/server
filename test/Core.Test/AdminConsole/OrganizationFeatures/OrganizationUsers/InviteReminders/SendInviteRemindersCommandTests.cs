using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

[SutProviderCustomize]
public class SendInviteRemindersCommandTests
{
    private const int _batchSize = 100;

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenFlagIsOff_SkipsTheRunAndLogsInformation(Organization organization)
    {
        var sutProvider = GetSutProvider(flagEnabled: false);
        ArrangeClaim(sutProvider, [Reminder(organization, 1)], organization);

        var result = await sutProvider.Sut.SendInviteRemindersAsync();

        Assert.Equal(0, result.Claimed);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .ClaimInviteReminderSequencesAsync(default, default, default, default);
        sutProvider.GetDependency<ILogger<SendInviteRemindersCommand>>().Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(FeatureFlagKeys.AutoReinviteUsers)),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SendInviteRemindersAsync_WhenBatchSizeIsZero_SkipsTheRunAndLogsInformation()
    {
        var sutProvider = GetSutProvider(batchSize: 0);

        var result = await sutProvider.Sut.SendInviteRemindersAsync();

        Assert.Equal(0, result.Claimed);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .ClaimInviteReminderSequencesAsync(default, default, default, default);
        sutProvider.GetDependency<ILogger<SendInviteRemindersCommand>>().Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("batch size is 0")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SendInviteRemindersAsync_WhenBatchSizeIsBelowZero_ClaimsWithDefaultAndLogsWarning()
    {
        var sutProvider = GetSutProvider(batchSize: -1);
        ArrangeClaim(sutProvider, []);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ClaimInviteReminderSequencesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Core.Settings.GlobalSettings.DefaultOrganizationInviteReminderBatchSize);
        sutProvider.GetDependency<ILogger<SendInviteRemindersCommand>>().Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("batch size -1 is below 0")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SendInviteRemindersAsync_WhenFlagIsOn_ClaimsWithScheduleFromConstants()
    {
        var sutProvider = GetSutProvider();
        var now = new DateTimeOffset(2026, 10, 22, 14, 0, 0, TimeSpan.Zero);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);
        ArrangeClaim(sutProvider, []);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ClaimInviteReminderSequencesAsync(
                now.UtcDateTime,
                now.UtcDateTime.AddDays(-InviteReminderConstants.MaxLatenessDays),
                now.UtcDateTime.AddDays(InviteReminderConstants.ReminderIntervalDays),
                _batchSize);
    }

    [Fact]
    public async Task SendInviteRemindersAsync_WhenNothingIsClaimed_MakesNoReadsOrMailCalls()
    {
        var sutProvider = GetSutProvider();
        ArrangeClaim(sutProvider, []);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByIdsAsync(default!);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByEmailsAsync(default!);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendOrganizationInviteReminderEmailsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenClaimFillsBatch_LogsInformation(Organization organization)
    {
        var sutProvider = GetSutProvider(batchSize: 2);
        ArrangeClaim(sutProvider, [Reminder(organization, 1), Reminder(organization, 1)], organization);

        await sutProvider.Sut.SendInviteRemindersAsync();

        sutProvider.GetDependency<ILogger<SendInviteRemindersCommand>>().Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("filled the batch size of 2")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_MakesOneMailCallForEachOrgAndReminderNumber(
        Organization organization1, Organization organization2)
    {
        var sutProvider = GetSutProvider();
        var first = Reminder(organization1, 1);
        var third = Reminder(organization1, 3);
        ArrangeClaim(sutProvider,
            [first, third, Reminder(organization2, 1), Reminder(organization2, 1)],
            organization1, organization2);

        await sutProvider.Sut.SendInviteRemindersAsync();

        var mailService = sutProvider.GetDependency<IMailService>();
        await mailService.ReceivedWithAnyArgs(3).SendOrganizationInviteReminderEmailsAsync(default!, default);
        await mailService.Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => info.OrgUserTokenPairs.Single().OrgUser.Id == first.Id), 1);
        await mailService.Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => info.OrgUserTokenPairs.Single().OrgUser.Id == third.Id), 3);
        await mailService.Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrganizationName == organization2.DisplayName() && info.OrgUserTokenPairs.Count() == 2), 1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_BuildsMemberFromClaimedRow(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var reminder = Reminder(organization, 1);
        ArrangeClaim(sutProvider, [reminder], organization);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Single().OrgUser.Id == reminder.Id &&
                info.OrgUserTokenPairs.Single().OrgUser.OrganizationId == organization.Id &&
                info.OrgUserTokenPairs.Single().OrgUser.Email == reminder.Email &&
                info.OrgUserTokenPairs.Single().Token.ExpirationDate > DateTime.UtcNow &&
                info.InviterEmail == null),
            1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenNoOrgUsesSso_MakesNoSsoReads(Organization organization)
    {
        var sutProvider = GetSutProvider();
        organization.UseSso = false;
        organization.UsePolicies = true;
        ArrangeClaim(sutProvider, [Reminder(organization, 1)], organization);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdsAsync(default!);
        await sutProvider.GetDependency<IPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdsTypeAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_SsoOrgWithRequireSsoPolicy_SendsWithSsoRequired(
        Organization organization, SsoConfig ssoConfig, Policy policy)
    {
        var sutProvider = GetSutProvider();
        organization.UseSso = true;
        organization.UsePolicies = true;
        ssoConfig.OrganizationId = organization.Id;
        ssoConfig.Enabled = true;
        policy.OrganizationId = organization.Id;
        policy.Type = PolicyType.RequireSso;
        policy.Enabled = true;
        ArrangeClaim(sutProvider, [Reminder(organization, 1)], organization);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetManyByOrganizationIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == organization.Id))
            .Returns([ssoConfig]);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdsTypeAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == organization.Id),
                PolicyType.RequireSso)
            .Returns([policy]);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgSsoEnabled && info.OrgSsoLoginRequiredPolicyEnabled &&
                info.OrgSsoIdentifier == organization.Identifier),
            1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_SsoOrgWithoutUsePolicies_SendsWithoutSsoRequiredOrPolicyRead(
        Organization organization, SsoConfig ssoConfig)
    {
        var sutProvider = GetSutProvider();
        organization.UseSso = true;
        organization.UsePolicies = false;
        ssoConfig.OrganizationId = organization.Id;
        ssoConfig.Enabled = true;
        ArrangeClaim(sutProvider, [Reminder(organization, 1)], organization);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetManyByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ssoConfig]);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IPolicyRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdsTypeAsync(default!, default);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => info.OrgSsoEnabled && !info.OrgSsoLoginRequiredPolicyEnabled),
            1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_SsoOrgWithDisabledSsoConfig_SendsWithoutSso(
        Organization organization, SsoConfig ssoConfig)
    {
        var sutProvider = GetSutProvider();
        organization.UseSso = true;
        organization.UsePolicies = true;
        ssoConfig.OrganizationId = organization.Id;
        ssoConfig.Enabled = false;
        ArrangeClaim(sutProvider, [Reminder(organization, 1)], organization);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetManyByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([ssoConfig]);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => !info.OrgSsoEnabled && !info.OrgSsoLoginRequiredPolicyEnabled),
            1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenAccountEmailDiffersInCase_TreatsUserAsExisting(
        Organization organization, User user)
    {
        var sutProvider = GetSutProvider();
        var withAccount = Reminder(organization, 1);
        var withoutAccount = Reminder(organization, 1);
        user.Email = withAccount.Email.ToUpperInvariant();
        ArrangeClaim(sutProvider, [withAccount, withoutAccount], organization);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([user]);

        await sutProvider.Sut.SendInviteRemindersAsync();

        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserHasExistingUserDict[withAccount.Id] &&
                !info.OrgUserHasExistingUserDict[withoutAccount.Id]),
            1);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenOneMailCallFails_StillSendsTheOthersAndCountsTheFailure(
        Organization failingOrganization, Organization organization)
    {
        var sutProvider = GetSutProvider();
        var failing = Reminder(failingOrganization, 1);
        var first = Reminder(organization, 1);
        var second = Reminder(organization, 2);
        ArrangeClaim(sutProvider, [failing, first, second], failingOrganization, organization);
        FailMailCallsFor(sutProvider, failingOrganization);

        var result = await sutProvider.Sut.SendInviteRemindersAsync();

        var mailService = sutProvider.GetDependency<IMailService>();
        await mailService.Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => info.OrganizationName == organization.DisplayName()), 1);
        await mailService.Received(1).SendOrganizationInviteReminderEmailsAsync(
            Arg.Is<OrganizationInvitesInfo>(info => info.OrganizationName == organization.DisplayName()), 2);
        Assert.Equal(3, result.Claimed);
        Assert.Equal(2, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.Equal([first.Id, second.Id], result.SentMembers.Select(m => m.Id));
        Assert.Equal(new Dictionary<int, int> { [1] = 1, [2] = 1 }, result.SentByReminderNumber);
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenMailCallFails_LogsErrorWithoutEmails(Organization organization)
    {
        var sutProvider = GetSutProvider();
        var reminder = Reminder(organization, 3);
        ArrangeClaim(sutProvider, [reminder], organization);
        sutProvider.GetDependency<IMailService>()
            .SendOrganizationInviteReminderEmailsAsync(Arg.Any<OrganizationInvitesInfo>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down."));

        await sutProvider.Sut.SendInviteRemindersAsync();

        sutProvider.GetDependency<ILogger<SendInviteRemindersCommand>>().Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString()!.Contains(organization.Id.ToString()) &&
                state.ToString()!.Contains("1 members did not get reminder 3") &&
                !state.ToString()!.Contains(reminder.Email)),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendInviteRemindersAsync_WhenOrganizationIsMissing_CountsMembersAsFailed(
        Organization organization, Organization deletedOrganization)
    {
        var sutProvider = GetSutProvider();
        var reminder = Reminder(organization, 1);
        ArrangeClaim(sutProvider, [reminder, Reminder(deletedOrganization, 1)], organization);

        var result = await sutProvider.Sut.SendInviteRemindersAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal([reminder.Id], result.SentMembers.Select(m => m.Id));
    }

    private static SutProvider<SendInviteRemindersCommand> GetSutProvider(bool flagEnabled = true,
        int batchSize = _batchSize)
    {
        var sutProvider = new SutProvider<SendInviteRemindersCommand>()
            .WithFakeTimeProvider()
            .Create();

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.AutoReinviteUsers).Returns(flagEnabled);
        sutProvider.GetDependency<IGlobalSettings>().OrganizationInviteReminderBatchSize.Returns(batchSize);
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.AddDays(7)
            });

        return sutProvider;
    }

    /// <summary>
    /// Makes the claim return <paramref name="claimed"/> and the organization lookup return
    /// <paramref name="organizations"/>.
    /// </summary>
    private static void ArrangeClaim(SutProvider<SendInviteRemindersCommand> sutProvider,
        ICollection<OrganizationUserInviteReminder> claimed, params Organization[] organizations)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ClaimInviteReminderSequencesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<int>())
            .Returns(claimed);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(organizations.ToList());
    }

    private static void FailMailCallsFor(SutProvider<SendInviteRemindersCommand> sutProvider,
        Organization organization) =>
        sutProvider.GetDependency<IMailService>()
            .SendOrganizationInviteReminderEmailsAsync(
                Arg.Is<OrganizationInvitesInfo>(info => info.OrganizationName == organization.DisplayName()),
                Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("SMTP is down."));

    private static OrganizationUserInviteReminder Reminder(Organization organization, byte reminderNumber) =>
        new()
        {
            Id = CombGuid.Generate(),
            OrganizationId = organization.Id,
            Email = $"{Guid.NewGuid()}@example.com",
            InviteRemindersSentCount = reminderNumber
        };
}
