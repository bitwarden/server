using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.Auth.Controllers.TwoFactor.TwoFactorControllerTestHelpers;

namespace Bit.Api.Test.Auth.Controllers.TwoFactor;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerAuthenticatorTests
{
    [Theory, BitAutoData]
    public async Task PutAuthenticator_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutAuthenticator_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorAuthenticatorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutAuthenticator_InvalidTokenData_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorAuthenticatorUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(otherUser, "different-key"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutAuthenticator_ValidToken_ReturnsResponse(
        User user,
        TwoFactorAuthenticatorUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(
            sutProvider,
            new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key));

        // UserManager.VerifyTwoFactorTokenAsync delegates to a registered
        // token provider; register a substitute that accepts model.Token.
        var authenticatorProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        authenticatorProvider
            .ValidateAsync("TwoFactor", model.Token, Arg.Any<UserManager<User>>(), Arg.Any<User>())
            .Returns(true);
        sutProvider.GetDependency<UserManager<User>>()
            .RegisterTokenProvider(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Authenticator),
                authenticatorProvider);

        var response = await sutProvider.Sut.PutAuthenticator(model);

        Assert.IsType<TwoFactorAuthenticatorUpdateResponseModel>(response);
        Assert.NotNull(response.Authenticator);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
    }

    [Theory, BitAutoData]
    public async Task DeleteAuthenticator_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DeleteAuthenticator_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorAuthenticatorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DeleteAuthenticator_InvalidTokenData_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorAuthenticatorDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(otherUser, "different-key"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DeleteAuthenticator_ValidToken_DisablesProvider(
        User user,
        TwoFactorAuthenticatorDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(
            sutProvider,
            new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key));

        await sutProvider.Sut.DeleteAuthenticator(model);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
    }
}
