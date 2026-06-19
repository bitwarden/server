using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models.Business.Tokenables;
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

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerTests
{
    [Theory, BitAutoData]
    public async Task CheckAsync_UserNull_ThrowsUnauthorizedException(SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
    public async Task CheckAsync_BadSecret_ThrowsBadRequestException(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
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

    [Theory, BitAutoData]
    public async Task PutDuo_CannotAccessPremium_ThrowsBadRequestException(User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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

        Assert.True(result.Enabled);
        Assert.Equal("example.com", result.Host);
        Assert.Equal("clientId", result.ClientId);
        // ClientSecret is masked server-side per PM-9826; non-premium users get the same mask.
        Assert.StartsWith("secret", result.ClientSecret);
        Assert.Contains("*", result.ClientSecret);
        Assert.Equal("protected-duo-token", result.UserVerificationToken);
        // The read path no longer consults premium.
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremium(default);
    }

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

        Assert.True(result.Enabled);
        Assert.Equal("ccccccccccbe", result.Key1);
        Assert.True(result.Nfc);
        Assert.Equal("protected-yubikey-token", result.UserVerificationToken);
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremium(default);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_InvalidConfiguration_ThrowsBadRequestException(User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
    public async Task PutDuo_Success(User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
        Assert.IsType<TwoFactorDuoResponseModel>(result);
        Assert.Equal(user.TwoFactorProviders, request.ToUser(user).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_ExpiredToken_ThrowsBadRequest(
        User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
        User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
        User user, User otherUser, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider,
            ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task PutDuo_WrongProviderType_ThrowsBadRequest(
        User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider,
            ValidUserVerificationTokenableFor(user, TwoFactorProviderType.YubiKey));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PutDuo(request));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task CheckOrganizationAsync_ManagePolicies_ThrowsNotFoundException(
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
    public async Task CheckOrganizationAsync_GetByIdAsync_ThrowsNotFoundException(
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
    public async Task GetOrganizationDuo_Success(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);

        // Act
        var result = await sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_InvalidConfiguration_ThrowsBadRequestException(
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);

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
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result =
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
        Assert.Equal(organization.TwoFactorProviders, request.ToOrganization(organization).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
    public async Task DisableOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDisableRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.DisableOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DisableOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDisableRequestModel request, SutProvider<TwoFactorController> sutProvider)
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
        var result = () => sutProvider.Sut.DisableOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DisableOrganizationDuo_Success(
        User user, Organization organization, TwoFactorOrganizationDuoDisableRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();

        // Act
        var result = await sutProvider.Sut.DisableOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.IsType<TwoFactorProviderResponseModel>(result);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(organization, TwoFactorProviderType.OrganizationDuo);
    }


    [Theory, BitAutoData]
    public async Task PutAuthenticator_ExpiredToken_ThrowsBadRequest(
        User user,
        UpdateTwoFactorAuthenticatorRequestModel model,
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
        UpdateTwoFactorAuthenticatorRequestModel model,
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
        UpdateTwoFactorAuthenticatorRequestModel model,
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
        UpdateTwoFactorAuthenticatorRequestModel model,
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

        Assert.IsType<TwoFactorAuthenticatorResponseModel>(response);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
    }

    [Theory, BitAutoData]
    public async Task DisableAuthenticator_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorDisableRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key)
        {
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1)
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DisableAuthenticator_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorAuthenticatorDisableRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorAuthenticatorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DisableAuthenticator_InvalidTokenData_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorAuthenticatorDisableRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(sutProvider, new TwoFactorAuthenticatorUserVerificationTokenable(otherUser, "different-key"));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableAuthenticator(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
    }

    [Theory, BitAutoData]
    public async Task DisableAuthenticator_ValidToken_ReturnsResponse(
        User user,
        TwoFactorAuthenticatorDisableRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupAuthenticatorTokenFactoryToUnprotectInto(
            sutProvider,
            new TwoFactorAuthenticatorUserVerificationTokenable(user, model.Key));

        var response = await sutProvider.Sut.DisableAuthenticator(model);

        Assert.IsType<TwoFactorProviderResponseModel>(response);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Authenticator);
    }

    [Theory, BitAutoData]
    public async Task DisableWebAuthnAll_ValidToken_DisablesProvider(
        User user,
        TwoFactorWebAuthnDisableAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.WebAuthn));

        var response = await sutProvider.Sut.DisableWebAuthnAll(model);

        Assert.IsType<TwoFactorProviderResponseModel>(response);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
    }

    [Theory, BitAutoData]
    public async Task DisableWebAuthnAll_ExpiredToken_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDisableAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(sutProvider, new TwoFactorUserVerificationTokenable
        {
            UserId = user.Id,
            ProviderType = TwoFactorProviderType.WebAuthn,
            ExpirationDate = DateTime.UtcNow.AddMinutes(-1),
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DisableWebAuthnAll_TryUnprotectFails_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDisableAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(model.UserVerificationToken, out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DisableWebAuthnAll_TokenBoundToDifferentUser_ThrowsBadRequest(
        User user,
        User otherUser,
        TwoFactorWebAuthnDisableAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(otherUser, TwoFactorProviderType.WebAuthn));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DisableWebAuthnAll_TokenBoundToDifferentProvider_ThrowsBadRequest(
        User user,
        TwoFactorWebAuthnDisableAllRequestModel model,
        SutProvider<TwoFactorController> sutProvider)
    {
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.Duo));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DisableWebAuthnAll(model));
        AssertModelStateContains(exception, "UserVerificationToken", "User verification failed.");
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceiveWithAnyArgs()
            .DisableTwoFactorProviderAsync(default, default);
    }

    private static void SetupGetUserByPrincipalAsync(SutProvider<TwoFactorController> sutProvider, User user)
    {
        // PutAuthenticator calls model.ToUser(user) which reads user.TwoFactorProviders
        // as JSON. BitAutoData populates that with a random non-JSON string. Clear it so
        // the request handler doesn't fail before the token validation we want to test.
        user.TwoFactorProviders = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
    }

    private static void SetupAuthenticatorTokenFactoryToUnprotectInto(
        SutProvider<TwoFactorController> sutProvider,
        TwoFactorAuthenticatorUserVerificationTokenable tokenable)
    {
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<TwoFactorAuthenticatorUserVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tokenable;
                return true;
            });
    }

    private static void SetupUserVerificationTokenFactoryToUnprotectInto(
        SutProvider<TwoFactorController> sutProvider,
        TwoFactorUserVerificationTokenable tokenable)
    {
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tokenable;
                return true;
            });
    }

    private static TwoFactorUserVerificationTokenable ValidUserVerificationTokenableFor(
        User user, TwoFactorProviderType providerType) =>
        new()
        {
            UserId = user.Id,
            ProviderType = providerType,
            ExpirationDate = DateTime.UtcNow.AddMinutes(30),
        };

    private static void AssertModelStateContains(BadRequestException exception, string key, string expectedMessage)
    {
        Assert.NotNull(exception.ModelState);
        Assert.True(exception.ModelState.ContainsKey(key), $"Expected ModelState to contain key '{key}'.");
        Assert.Contains(exception.ModelState[key]!.Errors, e => e.ErrorMessage == expectedMessage);
    }

    private string GetUserTwoFactorDuoProvidersJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetUserTwoFactorYubiKeyProvidersJson()
    {
        return
            "{\"3\":{\"Enabled\":true,\"MetaData\":{\"Key1\":\"ccccccccccbe\",\"Key2\":null,\"Key3\":null,\"Key4\":null,\"Key5\":null,\"Nfc\":true}}}";
    }

    private string GetOrganizationTwoFactorDuoProvidersJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private static void SetupValidateUserBySecretToPass(SutProvider<TwoFactorController> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);
    }

    private void SetupCheckOrganizationAsyncToPass(SutProvider<TwoFactorController> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(organization);
    }
}
