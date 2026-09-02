using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

[SutProviderCustomize]
public class SendOrganizationInvitesCommandTests
{
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory]
    [OrganizationInviteCustomize, OrganizationCustomize, BitAutoData]
    public async Task SendInvitesAsync_SsoOrgWithNeverEnabledRequireSsoPolicy_SendsEmailWithoutRequiringSso(
        Organization organization,
        SsoConfig ssoConfig,
        OrganizationUser invite,
        [Policy(PolicyType.RequireSso, false)] PolicyStatus policy,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Org must be able to use SSO and policies to trigger this test case
        organization.UseSso = true;
        organization.UsePolicies = true;

        ssoConfig.Enabled = true;
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        // Return null policy to mimic new org that's never turned on the require sso policy
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.RequireSso)
            .Returns(policy);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                });

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.OrgUserTokenPairs.FirstOrDefault(x => x.OrgUser.Email == invite.Email).OrgUser == invite &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name &&
                info.OrgSsoLoginRequiredPolicyEnabled == false));
    }

    [Theory]
    [OrganizationInviteCustomize, OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_SsoOrgWithNullSsoConfig_SendsInvite(
        Organization organization,
        OrganizationUser invite,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Org must be able to use SSO to trigger this proper test case as we currently only call to retrieve
        // an org's SSO config if the org can use SSO
        organization.UseSso = true;

        // Return null for sso config
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(organization.Id).ReturnsNull();

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                });

        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization));

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.OrgUserTokenPairs.FirstOrDefault(x => x.OrgUser.Email == invite.Email).OrgUser == invite &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.Free)]
    [BitAutoData(PlanType.Custom)]
    public async Task SendInvitesAsync_CallsMailServiceWithNewTemplates(
        PlanType planType,
        Organization organization,
        OrganizationUser invite,
        User invitingUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProvider(sutProvider);

        // Arrange
        organization.PlanType = planType;
        invite.OrganizationId = organization.Id;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(invitingUser.Id)
            .Returns(invitingUser);

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization, false, invitingUser.Id));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Email == invite.Email) &&
                info.InviterEmail == invitingUser.Email));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WithInvitingUserId_PopulatesInviterEmail(
        Organization organization,
        OrganizationUser invite,
        User invitingUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProvider(sutProvider);

        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(invitingUser.Id)
            .Returns(invitingUser);

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization, false, invitingUser.Id));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Email == invite.Email) &&
                info.InviterEmail == invitingUser.Email));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WithNullInvitingUserId_SendsEmailWithoutInviter(
        Organization organization,
        OrganizationUser invite,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProvider(sutProvider);

        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });

        // Act - pass null for InvitingUserId
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization, false, null));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Email == invite.Email) &&
                info.InviterEmail == null));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WithNonExistentInvitingUserId_SendsEmailWithoutInviter(
        Organization organization,
        OrganizationUser invite,
        Guid nonExistentUserId,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProvider(sutProvider);

        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        // Mock GetByIdAsync to return null for non-existent user
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(nonExistentUserId)
            .ReturnsNull();

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization, false, nonExistentUserId));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Email == invite.Email) &&
                info.InviterEmail == null));
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task SendInvitesAsync_WhenAnOrgUserHasNoEmail_DoesNotLookThatEmailUp(
        string blankEmail,
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - an invited org user with no email, as left behind by SSO just-in-time provisioning
        inviteWithoutEmail.Email = blankEmail;

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert - a blank email in this lookup fails the whole send against SQL Server
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .GetManyByEmailsAsync(Arg.Is<IEnumerable<string>>(emails => emails.Any(string.IsNullOrWhiteSpace)));
    }

    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task SendInvitesAsync_WhenAnOrgUserHasNoEmailButALinkedUser_ResolvesAndSendsBothInvites(
        string blankEmail,
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        User linkedUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - corrupt invited row (no email, linked to a user) has its email resolved from the linked user
        inviteWithoutEmail.Email = blankEmail;
        inviteWithoutEmail.UserId = linkedUser.Id;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([linkedUser]);

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert - both invites are sent, and the resolved user carries the recovered email
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 2 &&
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Id == invite.Id) &&
                info.OrgUserTokenPairs.Any(p =>
                    p.OrgUser.Id == inviteWithoutEmail.Id && p.OrgUser.Email == linkedUser.Email)));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WhenAnOrgUserHasNoEmail_MailInfoIsInternallyConsistent(
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange
        inviteWithoutEmail.Email = null;

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert - every mailed org user needs a recipient and an entry in the existing user dictionary
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.All(pair =>
                    !string.IsNullOrWhiteSpace(pair.OrgUser.Email) &&
                    info.OrgUserHasExistingUserDict.ContainsKey(pair.OrgUser.Id))));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WhenNoOrgUserHasAnEmail_SendsNothing(
        Organization organization,
        OrganizationUser inviteWithoutEmail,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange
        inviteWithoutEmail.Email = null;

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([inviteWithoutEmail], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Any<OrganizationInvitesInfo>());
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WhenAnOrgUserHasNoEmail_WarnsWithTheSkippedOrgUserId(
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange
        inviteWithoutEmail.Email = null;
        inviteWithoutEmail.Status = OrganizationUserStatusType.Invited;
        inviteWithoutEmail.OrganizationId = organization.Id;

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert - the log is the only signal an operator gets, and it must not carry an email address
        sutProvider.GetDependency<ILogger<SendOrganizationInvitesCommand>>().Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state =>
                state.ToString().Contains(inviteWithoutEmail.Id.ToString()) &&
                state.ToString().Contains(organization.Id.ToString()) &&
                !state.ToString().Contains(invite.Email)),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_WhenAccountEmailDiffersInCase_TreatsUserAsExisting(
        Organization organization,
        OrganizationUser invite,
        User existingUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - email lookups are case insensitive everywhere else in the invite flow
        invite.Email = "member@example.com";
        existingUser.Email = "Member@Example.com";

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([existingUser]);

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserHasExistingUserDict[invite.Id]));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_MixedBatch_ResolvesAndDropsUnresolvable(
        Organization organization,
        OrganizationUser canonicalInvite,
        OrganizationUser resolvableInvite,
        OrganizationUser unresolvableInvite,
        User linkedUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange
        resolvableInvite.Email = null;
        resolvableInvite.UserId = linkedUser.Id;

        unresolvableInvite.Email = null;
        unresolvableInvite.UserId = null;
        unresolvableInvite.Status = OrganizationUserStatusType.Invited;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([linkedUser]);

        // Act
        await sutProvider.Sut.SendInvitesAsync(
            new SendInvitesRequest([canonicalInvite, resolvableInvite, unresolvableInvite], organization));

        // Assert - the unresolvable row is logged
        sutProvider.GetDependency<ILogger<SendOrganizationInvitesCommand>>().Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString().Contains(unresolvableInvite.Id.ToString())),
            null,
            Arg.Any<Func<object, Exception, string>>());

        // Assert - correct invites are sent
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 2 &&
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Id == canonicalInvite.Id) &&
                info.OrgUserTokenPairs.Any(p => p.OrgUser.Id == resolvableInvite.Id) &&
                info.OrgUserTokenPairs.All(p => p.OrgUser.Id != unresolvableInvite.Id)));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_OrgUserWithNoEmailAndNoUserId_Drops(
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - no email and no linked user means the row cannot be resolved
        inviteWithoutEmail.Email = null;
        inviteWithoutEmail.UserId = null;

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.OrgUserTokenPairs.Single().OrgUser.Id == invite.Id));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_OrgUserWithNoEmailAndDanglingUserId_Drops(
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        Guid danglingUserId,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - the linked user no longer exists, so GetManyAsync returns nothing for it
        inviteWithoutEmail.Email = null;
        inviteWithoutEmail.UserId = danglingUserId;

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.OrgUserTokenPairs.Single().OrgUser.Id == invite.Id));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_LinkedUserHasBlankEmail_Drops(
        Organization organization,
        OrganizationUser invite,
        OrganizationUser inviteWithoutEmail,
        User linkedUser,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Arrange - the linked user resolves, but has no email to recover, so the row cannot be resolved
        inviteWithoutEmail.Email = null;
        inviteWithoutEmail.UserId = linkedUser.Id;
        linkedUser.Email = "";

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([linkedUser]);

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite, inviteWithoutEmail], organization));

        // Assert
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.OrgUserTokenPairs.Single().OrgUser.Id == invite.Id));
    }

    [Theory, BitAutoData]
    public async Task SendInvitesAsync_AllOrgUsersHaveEmail_SkipsEmailResolutionEntirely(
        Organization organization,
        OrganizationUser firstInvite,
        OrganizationUser secondInvite,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProviderWithNoExistingUsers(sutProvider);

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([firstInvite, secondInvite], organization));

        // Assert - the fast path takes no extra dependencies
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendUpdatedOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 2));
    }

    private void SetupSutProvider(SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();
    }

    private void SetupSutProviderWithNoExistingUsers(SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        SetupSutProvider(sutProvider);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyByEmailsAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });
    }
}
