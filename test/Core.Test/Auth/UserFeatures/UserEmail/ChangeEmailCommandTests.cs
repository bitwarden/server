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
    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_Success_UpdatesUserAndPushesLogout(
        User user,
        string newEmail,
        SutProvider<ChangeEmailCommand> sutProvider)
    {
        user.Gateway = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(user.Id);
        Assert.Equal(newEmail, user.Email);
        Assert.True(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_LogOutUserFalse_DoesNotPushLogout(
        User user,
        string newEmail,
        SutProvider<ChangeEmailCommand> sutProvider)
    {
        user.Gateway = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);

        await sutProvider.Sut.ChangeEmailAsync(user, newEmail, logOutUser: false);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_DuplicateEmail_ThrowsBadRequest(
        User user,
        User existingUser,
        string newEmail,
        SutProvider<ChangeEmailCommand> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns(existingUser);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ChangeEmailAsync(user, newEmail));
    }

    [Theory, BitAutoData]
    public async Task ChangeEmailAsync_StripeSyncFails_RollsBackEmail(
        User user,
        string newEmail,
        SutProvider<ChangeEmailCommand> sutProvider)
    {
        var originalEmail = user.Email;
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_test";
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(newEmail)
            .Returns((User)null);
        sutProvider.GetDependency<IStripeSyncService>()
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("Stripe error"));

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.ChangeEmailAsync(user, newEmail));

        await sutProvider.GetDependency<IUserRepository>().Received(2).ReplaceAsync(user);
        Assert.Equal(originalEmail, user.Email);
    }
}
