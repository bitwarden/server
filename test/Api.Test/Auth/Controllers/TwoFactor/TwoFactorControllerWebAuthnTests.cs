using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
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
public class TwoFactorControllerWebAuthnTests
{
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
}
