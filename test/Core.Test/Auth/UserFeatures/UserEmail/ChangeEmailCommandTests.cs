using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
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
    // Same-domain pair: lets tests bypass the policy gate via the domain-equality short-circuit
    // in ChangeEmailCommand.EnsureNewEmailDomainAllowedByPolicyAsync. Tests that specifically
    // exercise the policy gate use their own different-domain emails.
    private const string _currentEmail = "old@example.com";
    private const string _newEmail = "new@example.com";

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_AnotherUserOwnsEmail_ThrowsBadRequestAndDoesNotPersist(
        SutProvider<ChangeEmailCommand> sutProvider, User user, User otherUser)
    {
        user.Email = _currentEmail;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns(otherUser);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _newEmail));
        Assert.Equal("Email already in use.", ex.Message);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_SameUserHoldsEmail_Succeeds(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns(user);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        Assert.Equal(_newEmail, user.Email);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NonStripeUserWithMasterPassword_UpdatesFieldsAndPushesLogout(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = null;
        user.MasterPassword = "hash";
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        var before = DateTime.UtcNow;
        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);
        var after = DateTime.UtcNow;

        Assert.Equal(_newEmail, user.Email);
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
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = null;
        user.MasterPassword = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncSettingsAsync(user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeUser_SyncsCustomerEmailWithBillingAddress(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_123";
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        await sutProvider.GetDependency<IStripeSyncService>().Received(1)
            .UpdateCustomerEmailAddressAsync("cus_123", user.BillingEmailAddress()!);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeUserWithoutGatewayCustomerId_SkipsSyncAndCompletes(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        Assert.Equal(_newEmail, user.Email);
        await sutProvider.GetDependency<IStripeSyncService>().DidNotReceive()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>());
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeSyncThrows_RestoresPreviousEmailAndRevisionDatesThenRethrows(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_123";

        var originalEmail = user.Email;
        var originalRevisionDate = user.RevisionDate;
        var originalAccountRevisionDate = user.AccountRevisionDate;
        var originalLastEmailChangeDate = user.LastEmailChangeDate;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        var stripeFailure = new InvalidOperationException("stripe boom");
        sutProvider.GetDependency<IStripeSyncService>()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(stripeFailure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _newEmail));

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

    [Theory]
    [BitAutoData("no-at-sign")]
    [BitAutoData("too@many@signs.com")]
    [BitAutoData("@no-local-part.com")]
    [BitAutoData("")]
    public async Task ChangeEmailAsync_NewEmailDomainIsNull_ThrowsBadRequestAndDoesNotPersist(
        string invalidEmail, SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, invalidEmail));
        Assert.Equal("Invalid email address.", ex.Message);

        await sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>().DidNotReceive()
            .IsAllowedAsync(Arg.Any<User>(), Arg.Any<string>());
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NewEmailDomainBlockedByPolicy_ThrowsAndDoesNotPersist(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        const string blockedEmail = "user@blocked-domain.com";
        sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>()
            .IsAllowedAsync(user, "blocked-domain.com")
            .Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, blockedEmail));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", ex.Message);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive().ReplaceAsync(Arg.Any<User>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_NewEmailDomainNotBlockedByPolicy_Succeeds(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = null;
        const string unblockedEmail = "user@unblocked-domain.com";
        sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>()
            .IsAllowedAsync(user, "unblocked-domain.com")
            .Returns(true);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(unblockedEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, unblockedEmail);

        Assert.Equal(unblockedEmail, user.Email);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }
}
