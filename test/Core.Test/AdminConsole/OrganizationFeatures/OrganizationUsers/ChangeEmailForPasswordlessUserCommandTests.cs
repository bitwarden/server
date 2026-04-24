using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class ChangeEmailForPasswordlessUserCommandTests
{
    private static void SetupValidScenario(
        OrganizationUser organizationUser,
        User user,
        OrganizationDomain claimedDomain,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
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
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_Success_UpdatesUserAndPushesLogout(
        OrganizationUser organizationUser,
        User user,
        OrganizationDomain claimedDomain,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
    {
        newEmail = $"new@{newEmail.Split('@').Last()}";
        SetupValidScenario(organizationUser, user, claimedDomain, newEmail, sutProvider);
        user.Gateway = null;

        await sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(user.Id);
        Assert.Equal(newEmail, user.Email);
        Assert.True(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_UserHasMasterPassword_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
    {
        user.MasterPassword = "some-hash";
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, "new@example.com"));
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_DomainNotClaimedByOrg_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
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
            () => sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_DomainNotVerified_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
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
            () => sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_DuplicateEmail_ThrowsBadRequest(
        OrganizationUser organizationUser,
        User user,
        User existingUser,
        OrganizationDomain claimedDomain,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
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
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns(existingUser);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeSyncFails_RollsBackEmail(
        OrganizationUser organizationUser,
        User user,
        OrganizationDomain claimedDomain,
        string newEmail,
        SutProvider<ChangeEmailForPasswordlessUserCommand> sutProvider)
    {
        newEmail = $"new@{newEmail.Split('@').Last()}";
        var originalEmail = user.Email;
        SetupValidScenario(organizationUser, user, claimedDomain, newEmail, sutProvider);
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_test";

        sutProvider.GetDependency<IStripeSyncService>()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("Stripe error"));

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.ChangeEmailAsync(organizationUser.OrganizationId, organizationUser, newEmail));

        await sutProvider.GetDependency<IUserRepository>().Received(2).ReplaceAsync(user);
        Assert.Equal(originalEmail, user.Email);
    }
}
