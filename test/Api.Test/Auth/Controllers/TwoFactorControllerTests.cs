using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
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
