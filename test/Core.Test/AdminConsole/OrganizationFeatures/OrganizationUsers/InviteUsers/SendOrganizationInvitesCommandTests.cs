using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Services;
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
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(organization.Id).ReturnsNull();

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
}
