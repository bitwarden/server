using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
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
public class TwoFactorControllerYubiKeyTests
{
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
}
