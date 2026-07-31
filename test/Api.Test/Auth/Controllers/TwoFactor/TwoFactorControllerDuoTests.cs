using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.Auth.Controllers.TwoFactor.TwoFactorControllerTestHelpers;

namespace Bit.Api.Test.Auth.Controllers.TwoFactor;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerDuoTests
{
    [Theory, BitAutoData]
    public async Task GetDuo_Success(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        user.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);

        // Act
        var result = await sutProvider.Sut.GetDuo(request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
        Assert.NotNull(result.Duo);
    }

    [Theory, BitAutoData]
    public async Task GetDuo_NonPremiumUserWithExistingConfig_ReturnsConfigAndToken(
        User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // A lapsed-premium user (enrolled in Duo while premium, later lost premium) must be
        // able to read their own previously-configured provider and receive a UV token so the
        // standard GET → DELETE flow lets them disable it.
        SetupGetUserByPrincipalAsync(sutProvider, user);
        user.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .Protect(Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns("protected-duo-token");

        var result = await sutProvider.Sut.GetDuo(request);

        Assert.True(result.Duo.Enabled);
        Assert.Equal("example.com", result.Duo.Host);
        Assert.Equal("clientId", result.Duo.ClientId);
        // ClientSecret is masked server-side per PM-9826; non-premium users get the same mask.
        Assert.StartsWith("secret", result.Duo.ClientSecret);
        Assert.Contains("*", result.Duo.ClientSecret);
        Assert.Equal("protected-duo-token", result.UserVerificationToken);
        // The read path no longer consults premium.
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremium(default);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_CannotAccessPremium_ThrowsBadRequestException(User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(false);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));

        // Assert
        Assert.Equal("Premium status is required.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_InvalidConfiguration_ThrowsBadRequestException(User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .Returns(false);

        // Act
        try
        {
            await sutProvider.Sut.PutDuo(request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("Duo configuration settings are not valid. Please re-check the Duo Admin panel.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task PutDuo_Success(User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        user.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result = await sutProvider.Sut.PutDuo(request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoUpdateResponseModel>(result);
        Assert.NotNull(result.Duo);
        Assert.Equal(user.TwoFactorProviders, request.ToUser(user).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_ExpiredToken_ThrowsBadRequest(
        User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Duo,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutDuo_TryUnprotectFails_ThrowsBadRequest(
        User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(request.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutDuo_WrongUserId_ThrowsBadRequest(
        User user, User otherUser, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider,
            ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutDuo_WrongProviderType_ThrowsBadRequest(
        User user, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider,
            ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DeleteDuo_ValidToken_DisablesProvider(
        User user,
        TwoFactorDuoDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        await sutProvider.Sut.DeleteDuo(model);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Duo);
    }

    [Theory, BitAutoData]
    public async Task DeleteDuo_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorDuoDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Duo,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteDuo(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteDuo_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorDuoDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteDuo(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteDuo_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorDuoDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteDuo(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteDuo_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorDuoDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteDuo(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }
}
