using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.Auth.Controllers.TwoFactor.TwoFactorControllerTestHelpers;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerTests
{
    // ---------------------------------------------------------------------
    // Entry-secret verification (ValidateUserBySecretAsync helper)
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task ValidateUserBySecretAsync_UserNull_ThrowsUnauthorizedException(SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(null as User);

        // Act
        var result = () => sutProvider.Sut.GetDuo(request);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserBySecretAsync_BadSecret_ThrowsBadRequestException(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(false);

        // Act
        try
        {
            await sutProvider.Sut.GetDuo(request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("The model state is invalid.", e.Message);
        }
    }

    // ---------------------------------------------------------------------
    // YubiKey
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task GetYubiKey_NonPremiumUserWithExistingConfig_ReturnsConfigAndToken(
        User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Mirror of GetDuo_NonPremiumUserWithExistingConfig_ReturnsConfigAndToken for YubiKey.
        SetupGetUserByPrincipalAsync(sutProvider, user);
        user.TwoFactorProviders = GetUserTwoFactorYubiKeyProvidersJson();
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(false);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .Protect(Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns("protected-yubikey-token");

        var result = await sutProvider.Sut.GetYubiKey(request);

        Assert.True(result.YubiKey.Enabled);
        Assert.Equal("ccccccccccbe", result.YubiKey.Key1);
        Assert.True(result.YubiKey.Nfc);
        Assert.Equal("protected-yubikey-token", result.UserVerificationToken);
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremium(default);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_ValidToken_ReturnsResponse(
        User user,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        // Null TwoFactorProviders so the response constructor doesn't choke on AutoFixture junk.
        user.TwoFactorProviders = null;
        // Null all keys so ValidateYubiKeyAsync skips its UserManager round-trips.
        model.Key1 = model.Key2 = model.Key3 = model.Key4 = model.Key5 = null;
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));
        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);

        var response = await sutProvider.Sut.PutYubiKey(model);

        Assert.IsType<TwoFactorYubiKeyUpdateResponseModel>(response);
        Assert.NotNull(response.YubiKey);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.YubiKey);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_CannotAccessPremium_ThrowsBadRequestException(
        User user,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutYubiKey(model));

        Assert.Equal("Premium status is required.", exception.Message);
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutYubiKey_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteYubiKey_ValidToken_DisablesProvider(
        User user,
        TwoFactorYubiKeyDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        await sutProvider.Sut.DeleteYubiKey(model);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.YubiKey);
    }

    [Theory, BitAutoData]
    public async Task DeleteYubiKey_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.YubiKey,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteYubiKey_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteYubiKey_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorYubiKeyDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteYubiKey_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorYubiKeyDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteYubiKey(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    // ---------------------------------------------------------------------
    // Duo (personal)
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Organization Duo
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_Success(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);
        SetupOrganizationAccessToPass(sutProvider, organization);

        // Act
        var result = await sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorOrganizationDuoResponseModel>(result);
        Assert.NotNull(result.Duo);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_InvalidConfiguration_ThrowsBadRequestException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(false);

        // Act
        try
        {
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("Duo configuration settings are not valid. Please re-check the Duo Admin panel.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_Success(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result =
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorOrganizationDuoUpdateResponseModel>(result);
        Assert.NotNull(result.Duo);
        Assert.Equal(organization.TwoFactorProviders, request.ToOrganization(organization).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_Success(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();

        // Act
        await sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(organization, TwoFactorProviderType.OrganizationDuo);
    }

    // ---------------------------------------------------------------------
    // WebAuthn
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task PutWebAuthn_ValidToken_ReturnsResponse(
        User user,
        TwoFactorWebAuthnUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        user.TwoFactorProviders = null;
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.WebAuthn));
        sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!)
            .ReturnsForAnyArgs(true);

        var response = await sutProvider.Sut.PutWebAuthn(model);

        Assert.IsType<TwoFactorWebAuthnUpdateResponseModel>(response);
        Assert.NotNull(response.WebAuthn);
        await sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .Received(1)
            .CompleteTwoFactorWebAuthnRegistrationAsync(user, model.Id!.Value, model.Name, model.DeviceResponse);
    }

    [Theory, BitAutoData]
    public async Task PutWebAuthn_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .DidNotReceiveWithAnyArgs()
            .CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task PutWebAuthn_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .DidNotReceiveWithAnyArgs()
            .CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task PutWebAuthn_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorWebAuthnUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.WebAuthn));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .DidNotReceiveWithAnyArgs()
            .CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task PutWebAuthn_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ICompleteTwoFactorWebAuthnRegistrationCommand>()
            .DidNotReceiveWithAnyArgs()
            .CompleteTwoFactorWebAuthnRegistrationAsync(default!, default, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthn_ValidToken_ReturnsResponse(
        User user,
        TwoFactorWebAuthnDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        user.TwoFactorProviders = null;
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.WebAuthn));
        sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .DeleteTwoFactorWebAuthnCredentialAsync(default!, default)
            .ReturnsForAnyArgs(true);

        var response = await sutProvider.Sut.DeleteWebAuthn(model);

        Assert.IsType<TwoFactorWebAuthnDeleteResponseModel>(response);
        Assert.NotNull(response.WebAuthn);
        await sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .Received(1)
            .DeleteTwoFactorWebAuthnCredentialAsync(user, model.Id!.Value);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthn_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteTwoFactorWebAuthnCredentialAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthn_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteTwoFactorWebAuthnCredentialAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthn_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorWebAuthnDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.WebAuthn));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteTwoFactorWebAuthnCredentialAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthn_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthn(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IDeleteTwoFactorWebAuthnCredentialCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteTwoFactorWebAuthnCredentialAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthnAll_ValidToken_DisablesProvider(
        User user,
        TwoFactorWebAuthnDeleteAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.WebAuthn));

        await sutProvider.Sut.DeleteWebAuthnAll(model);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthnAll_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthnAll_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthnAll_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorWebAuthnDeleteAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.WebAuthn));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteWebAuthnAll_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDeleteAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    // ---------------------------------------------------------------------
    // Email
    // ---------------------------------------------------------------------

    [Theory, BitAutoData]
    public async Task GetEmail_Success(
        User user,
        SecretVerificationRequestModel request,
        SutProvider<TwoFactorController> sutProvider)
    {
        // AutoFixture seeds TwoFactorProviders with random junk; the response constructor
        // would try to deserialize it. Null it so the constructor takes the no-providers path.
        user.TwoFactorProviders = null;
        SetupValidateUserBySecretToPass(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .Protect(Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns("protected-email-token");

        var response = await sutProvider.Sut.GetEmail(request);

        Assert.IsType<TwoFactorEmailResponseModel>(response);
        Assert.NotNull(response.Email);
        Assert.Equal("protected-email-token", response.UserVerificationToken);
    }

    [Theory, BitAutoData]
    public async Task SendEmailSetup_ValidToken_InvokesEmailService(
        User user,
        TwoFactorEmailSetupRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Email));

        await sutProvider.Sut.SendEmailSetup(model);

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorSetupEmailAsync(user);
    }

    [Theory, BitAutoData]
    public async Task SendEmailSetup_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorEmailSetupRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Email,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SendEmailSetup(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorSetupEmailAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SendEmailSetup_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorEmailSetupRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SendEmailSetup(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorSetupEmailAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SendEmailSetup_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorEmailSetupRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Email));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SendEmailSetup(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorSetupEmailAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SendEmailSetup_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorEmailSetupRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SendEmailSetup(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorSetupEmailAsync(default);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_ValidTokenAndOtp_ReturnsResponse(
        User user,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Email));

        var emailProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        emailProvider
            .ValidateAsync("TwoFactor", model.Token, Arg.Any<UserManager<User>>(), Arg.Any<User>())
            .Returns(true);
        sutProvider.GetDependency<UserManager<User>>()
            .RegisterTokenProvider(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Email),
                emailProvider);

        var response = await sutProvider.Sut.PutEmail(model);

        Assert.IsType<TwoFactorEmailUpdateResponseModel>(response);
        Assert.NotNull(response.Email);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_InvalidOtp_ThrowsBadRequest(
        User user,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Email));

        var emailProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();
        emailProvider
            .ValidateAsync("TwoFactor", model.Token, Arg.Any<UserManager<User>>(), Arg.Any<User>())
            .Returns(false);
        sutProvider.GetDependency<UserManager<User>>()
            .RegisterTokenProvider(
                CoreHelpers.CustomProviderName(TwoFactorProviderType.Email),
                emailProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutEmail(model));
        AssertModelStateContains(exception, "Token", "Invalid token.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Email,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Email));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PutEmail_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorEmailUpdateRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .UpdateTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteEmail_ValidToken_DisablesProvider(
        User user,
        TwoFactorEmailDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Email));

        await sutProvider.Sut.DeleteEmail(model);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
    }

    [Theory, BitAutoData]
    public async Task DeleteEmail_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorEmailDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.Email,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteEmail_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorEmailDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteEmail_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorEmailDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Email));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteEmail_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorEmailDeleteRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteEmail(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

}
