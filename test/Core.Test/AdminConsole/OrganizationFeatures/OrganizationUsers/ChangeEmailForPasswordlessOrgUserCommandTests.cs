using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Auth.UserFeatures.UserEmail.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class ChangeEmailForPasswordlessOrgUserCommandTests
{
    private static void SetupValidScenario(
        OrganizationUser organizationUser,
        User user,
        OrganizationDomain claimedDomain,
        SutProvider<ChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        user.MasterPassword = null;
        user.Key = null;
        claimedDomain.SetVerifiedDate();

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(organizationUser.OrganizationId, Arg.Any<string>())
            .Returns(claimedDomain);
    }

    [Theory, BitAutoData]
    public async Task ChangeOrganizationUserEmailAsync_Success_DelegatesToChangeEmailCommand(
        OrganizationUser organizationUser,
        User user,
        OrganizationDomain claimedDomain,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        SetupValidScenario(organizationUser, user, claimedDomain, sutProvider);

        await sutProvider.Sut.ChangeOrganizationUserEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail);

        await sutProvider.GetDependency<IChangeEmailCommand>()
            .Received(1)
            .ChangeEmailAsync(user, newEmail, logOutUser: false);
    }

    [Theory, BitAutoData]
    public async Task ChangeOrganizationUserEmailAsync_UserHasMasterPassword_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        SutProvider<ChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        user.MasterPassword = "some-hash";
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeOrganizationUserEmailAsync(organizationUser.OrganizationId, organizationUser, "new@example.com"));
    }

    [Theory, BitAutoData]
    public async Task ChangeOrganizationUserEmailAsync_DomainNotClaimedByOrg_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        user.MasterPassword = null;
        user.Key = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(organizationUser.OrganizationId, Arg.Any<string>())
            .Returns((OrganizationDomain)null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeOrganizationUserEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));
    }

    [Theory, BitAutoData]
    public async Task ChangeOrganizationUserEmailAsync_DomainNotVerified_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessOrgUserCommand> sutProvider)
    {
        user.MasterPassword = null;
        user.Key = null;
        // Construct an unverified domain — VerifiedDate defaults to null since SetVerifiedDate() was never called.
        var unverifiedDomain = new OrganizationDomain { OrganizationId = organizationUser.OrganizationId };
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(organizationUser.OrganizationId, Arg.Any<string>())
            .Returns(unverifiedDomain);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeOrganizationUserEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));
    }
}
