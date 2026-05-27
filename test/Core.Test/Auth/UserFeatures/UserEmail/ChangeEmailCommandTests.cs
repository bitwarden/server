using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Enums;
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
using Microsoft.Extensions.Time.Testing;
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

    [Theory]
    [BitAutoData("hash")]
    [BitAutoData((string)null)]
    public async Task ChangeEmailAsync_NonStripeUser_UpdatesFieldsAndPushesSyncSettings(
        string masterPassword, User user)
    {
        var sutProvider = new SutProvider<ChangeEmailCommand>()
            .WithFakeTimeProvider()
            .Create();
        var now = new DateTime(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(now);

        user.Email = _currentEmail;
        user.Gateway = null;
        user.MasterPassword = masterPassword;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        Assert.Equal(_newEmail, user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal(now, user.LastEmailChangeDate);
        Assert.Equal(now, user.RevisionDate);
        Assert.Equal(now, user.AccountRevisionDate);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        // Sessions are not invalidated regardless of master-password presence: this command
        // assumes the master-password salt has been decoupled from User.Email.
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushSyncSettingsAsync(user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IStripeSyncService>().DidNotReceive()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>());
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

        Assert.True(user.EmailVerified);
        await sutProvider.GetDependency<IStripeSyncService>().Received(1)
            .UpdateCustomerEmailAddressAsync("cus_123", user.BillingEmailAddress()!);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushSyncSettingsAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeUserWithoutGatewayCustomerId_ThrowsAndRollsBack(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = null;

        var originalEmail = user.Email;
        var originalRevisionDate = user.RevisionDate;
        var originalAccountRevisionDate = user.AccountRevisionDate;
        var originalLastEmailChangeDate = user.LastEmailChangeDate;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _newEmail));
        Assert.Equal("Missing gateway customer ID or billing email address for Stripe sync.", ex.Message);

        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalRevisionDate, user.RevisionDate);
        Assert.Equal(originalAccountRevisionDate, user.AccountRevisionDate);
        Assert.Equal(originalLastEmailChangeDate, user.LastEmailChangeDate);
        await sutProvider.GetDependency<IStripeSyncService>().DidNotReceive()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>());
        // Two persists: initial write, then the rollback write.
        await sutProvider.GetDependency<IUserRepository>().Received(2).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
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
        // No push notification fires if Stripe sync failed.
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
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
        Assert.Equal("Invalid email address format.", ex.Message);

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
            .Returns(OrganizationDomainAllowEmailChangeDenialReason.DomainIsBlockedByPolicy);

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
    public async Task ChangeEmailAsync_UserIsClaimedAndDomainNotVerified_ThrowsAndDoesNotPersist(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        const string unverifiedEmail = "user@unverified-domain.com";
        sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>()
            .IsAllowedAsync(user, "unverified-domain.com")
            .Returns(OrganizationDomainAllowEmailChangeDenialReason.UserIsClaimedAndDomainNotVerified);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, unverifiedEmail));
        Assert.Equal(
            "Your account is managed by an organization, and this email address isn't on one of the organization's verified domains.",
            ex.Message);

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
            .Returns(OrganizationDomainAllowEmailChangeDenialReason.Allowed);
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(unblockedEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, unblockedEmail);

        Assert.Equal(unblockedEmail, user.Email);
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_SameDomain_SkipsOrganizationDomainQuery(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, _newEmail);

        await sutProvider.GetDependency<IOrganizationDomainAllowEmailChangeQuery>().DidNotReceive()
            .IsAllowedAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeRollbackWriteThrows_StillSurfacesOriginalStripeException(
        SutProvider<ChangeEmailCommand> sutProvider, User user)
    {
        user.Email = _currentEmail;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_123";

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(_newEmail)
            .Returns((User)null);

        var stripeFailure = new InvalidOperationException("stripe boom");
        sutProvider.GetDependency<IStripeSyncService>()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(stripeFailure);

        var rollbackFailure = new InvalidOperationException("db boom");
        var replaceCallCount = 0;
        sutProvider.GetDependency<IUserRepository>()
            .When(x => x.ReplaceAsync(user))
            .Do(_ =>
            {
                replaceCallCount++;
                if (replaceCallCount == 2)
                {
                    throw rollbackFailure;
                }
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, _newEmail));

        // Original Stripe cause propagates even when the rollback write also fails.
        Assert.Same(stripeFailure, thrown);
        // Both ReplaceAsync calls were attempted (initial write + rollback write).
        await sutProvider.GetDependency<IUserRepository>().Received(2).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushSyncSettingsAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }
}
