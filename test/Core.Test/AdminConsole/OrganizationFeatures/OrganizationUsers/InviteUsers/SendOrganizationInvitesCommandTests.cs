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
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
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
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
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
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
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
    public async Task SendInvitesAsync_WithFeatureFlagEnabled_CallsMailServiceWithNewTemplates(
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

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate)
            .Returns(true);

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
    public async Task SendInvitesAsync_WithFeatureFlagDisabled_UsesLegacyMailService(
        Organization organization,
        OrganizationUser invite,
        SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate)
            .Returns(false);

        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
            {
                ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
            });

        // Act
        await sutProvider.Sut.SendInvitesAsync(new SendInvitesRequest([invite], organization));

        // Assert - verify legacy mail service is called, not new mailer
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Any<OrganizationInvitesInfo>());
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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate)
            .Returns(true);

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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate)
            .Returns(true);

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
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate)
            .Returns(true);

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

    private void SetupSutProvider(SutProvider<SendOrganizationInvitesCommand> sutProvider)
    {
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();
    }
}
