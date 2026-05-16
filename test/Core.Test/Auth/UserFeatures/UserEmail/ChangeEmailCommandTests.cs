using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.UserFeatures.UserEmail;
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

namespace Bit.Core.Test.Auth.UserFeatures.UserEmail;

[SutProviderCustomize]
public class ChangeEmailCommandTests
{
    private static void StubNoClaimingOrganizations(SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization>());
    }

    private static void StubClaimingOrganization(SutProvider<ChangeEmailCommand> sutProvider, User user,
        string verifiedDomain)
    {
        var claimingOrg = new Organization { Id = Guid.NewGuid(), Enabled = true, UseOrganizationDomains = true };
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { claimingOrg });
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationDomain> { new() { DomainName = verifiedDomain } });
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NewEmailDomainDoesNotMatchClaimedDomain_ThrowsAndDoesNotPersist(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        const string newEmail = "user@other.com";
        StubClaimingOrganization(sutProvider, user, verifiedDomain: "example.com");

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, newEmail));
        Assert.Equal("Your new email must match your organization domain.", ex.Message);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NewEmailDomainMatchesClaimedDomain_Succeeds(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Gateway = null;
        const string newEmail = "user@example.com";
        StubClaimingOrganization(sutProvider, user, verifiedDomain: "example.com");
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        Assert.Equal(newEmail, user.Email);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_ClaimingOrgIsDisabled_IsIgnoredAndChangeProceeds(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = null;
        var disabledOrg = new Organization { Id = Guid.NewGuid(), Enabled = false, UseOrganizationDomains = true };
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(user.Id)
            .Returns(new List<Organization> { disabledOrg });
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        Assert.Equal(newEmail, user.Email);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().DidNotReceive()
            .GetVerifiedDomainsByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_AnotherUserOwnsEmail_ThrowsBadRequestAndDoesNotPersist(
        SutProvider<ChangeEmailCommand> sutProvider, User user, User otherUser, string newEmail)
    {
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns(otherUser);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, newEmail));
        Assert.Equal("Email already in use.", ex.Message);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_SameUserHoldsEmail_Succeeds(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = null;
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns(user);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        Assert.Equal(newEmail, user.Email);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NonStripeUserWithMasterPassword_UpdatesFieldsAndPushesLogout(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = null;
        user.MasterPassword = "hash";
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        var before = DateTime.UtcNow;
        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);
        var after = DateTime.UtcNow;

        Assert.Equal(newEmail, user.Email);
        Assert.True(user.EmailVerified);
        Assert.NotNull(user.LastEmailChangeDate);
        Assert.InRange(user.LastEmailChangeDate!.Value, before, after);
        Assert.InRange(user.RevisionDate, before, after);
        Assert.InRange(user.AccountRevisionDate, before, after);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IStripeSyncService>().DidNotReceive()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NonStripeUserWithoutMasterPassword_PushesSyncSettings(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = null;
        user.MasterPassword = null;
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncSettingsAsync(user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeUser_SyncsCustomerEmailWithBillingAddress(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_123";
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        await sutProvider.GetDependency<IStripeSyncService>().Received(1)
            .UpdateCustomerEmailAddressAsync("cus_123", user.BillingEmailAddress()!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeUserWithoutGatewayCustomerId_SkipsSyncAndCompletes(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = null;
        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        Assert.Equal(newEmail, user.Email);
        await sutProvider.GetDependency<IStripeSyncService>().DidNotReceive()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>());
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeSyncThrows_RestoresPreviousEmailAndRevisionDatesThenRethrows(
        SutProvider<ChangeEmailCommand> sutProvider, User user, string newEmail)
    {
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_123";

        var originalEmail = user.Email;
        var originalRevisionDate = user.RevisionDate;
        var originalAccountRevisionDate = user.AccountRevisionDate;
        var originalLastEmailChangeDate = user.LastEmailChangeDate;

        StubNoClaimingOrganizations(sutProvider, user);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        var stripeFailure = new InvalidOperationException("stripe boom");
        sutProvider.GetDependency<IStripeSyncService>()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(stripeFailure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, newEmail));

        Assert.Same(stripeFailure, thrown);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalRevisionDate, user.RevisionDate);
        Assert.Equal(originalAccountRevisionDate, user.AccountRevisionDate);
        Assert.Equal(originalLastEmailChangeDate, user.LastEmailChangeDate);
        // Two persists: initial write, then the rollback write.
        await sutProvider.GetDependency<IUserRepository>().Received(2).ReplaceAsync(user);
        // Session push must not fire if Stripe sync failed.
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }
}
