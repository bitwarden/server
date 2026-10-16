using AutoFixture;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.Auth.Controllers.TwoFactor.TwoFactorControllerTestHelpers;

namespace Bit.Api.Test.Auth.Controllers.TwoFactor;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerEmailTests
{
    private const string DeviceIdentifier = "device-identifier";
    private const string MasterPasswordHash = "master-password-hash";

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
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;

        await sutProvider.Sut.SendEmailSetup(model);

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorSetupEmailAsync(user, DeviceIdentifier);
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
            .SendTwoFactorSetupEmailAsync(default, default);
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
            .SendTwoFactorSetupEmailAsync(default, default);
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
            .SendTwoFactorSetupEmailAsync(default, default);
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
            .SendTwoFactorSetupEmailAsync(default, default);
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

        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;
        sutProvider.GetDependency<ITwoFactorEmailService>()
            .VerifyTwoFactorSetupTokenAsync(user, DeviceIdentifier, model.Token)
            .Returns(true);

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

        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;
        sutProvider.GetDependency<ITwoFactorEmailService>()
            .VerifyTwoFactorSetupTokenAsync(user, DeviceIdentifier, model.Token)
            .Returns(false);

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

    /// <summary>
    /// The emailed login code is bound to the device the current context reports from the Device-Identifier header.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmailLogin_DeviceHeader_SendsCodeForHeaderDevice(
        User user)
    {
        var sutProvider = CreateSutProviderFindingUser(user);
        SetupMasterPasswordToPass(sutProvider, user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;

        await sutProvider.Sut.SendEmailLoginAsync(MasterPasswordModel(user));

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);
    }

    /// <summary>
    /// When the header (via the current context) and the body name different devices, the header wins.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmailLogin_DeviceInHeaderAndBody_SendsCodeForHeaderDevice(
        User user)
    {
        var sutProvider = CreateSutProviderFindingUser(user);
        SetupMasterPasswordToPass(sutProvider, user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;
        var model = MasterPasswordModel(user);
        model.DeviceIdentifier = "body-device-identifier";

        await sutProvider.Sut.SendEmailLoginAsync(model);

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);
    }

    // TODO: PM-44555 - Delete this test once every supported mobile client version sends the Device-Identifier
    // header on send-email-login and the body fallback is removed.
    /// <summary>
    /// Without a header, the code is bound to the device named in the body.
    /// </summary>
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task SendEmailLogin_NoDeviceHeader_SendsCodeForBodyDevice(
        string headerDeviceIdentifier, User user)
    {
        var sutProvider = CreateSutProviderFindingUser(user);
        SetupMasterPasswordToPass(sutProvider, user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = headerDeviceIdentifier;
        var model = MasterPasswordModel(user);
        model.DeviceIdentifier = DeviceIdentifier;

        await sutProvider.Sut.SendEmailLoginAsync(model);

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);
    }

    /// <summary>
    /// A missing or over-long device identifier is rejected before the user is looked up, so the response says
    /// nothing about whether the email or credential is valid, and no code is sent.
    /// </summary>
    [Theory]
    [BitAutoData((string)null, (string)null)]
    [BitAutoData("", "")]
    [BitAutoData(" ", " ")]
    [BitAutoData("123456789012345678901234567890123456789012345678901", (string)null)]
    [BitAutoData((string)null, "123456789012345678901234567890123456789012345678901")]
    public async Task SendEmailLogin_MissingOrOverLongDevice_ThrowsBeforeUserLookup(
        string headerDeviceIdentifier,
        string bodyDeviceIdentifier,
        User user)
    {
        var sutProvider = CreateSutProviderFindingUser(user);
        SetupMasterPasswordToPass(sutProvider, user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = headerDeviceIdentifier;
        var model = MasterPasswordModel(user);
        model.DeviceIdentifier = bodyDeviceIdentifier;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SendEmailLoginAsync(model));

        AssertModelStateContains(exception, "Device-Identifier", "A valid device identifier is required.");
        await sutProvider.GetDependency<UserManager<User>>()
            .DidNotReceiveWithAnyArgs()
            .FindByEmailAsync(default);
        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .DidNotReceiveWithAnyArgs()
            .SendTwoFactorLoginEmailAsync(default, default);
    }

    /// <summary>
    /// The auth request branch binds the code to the requesting device.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmailLogin_ApprovedAuthRequest_SendsCodeForRequestingDevice(
        User user)
    {
        const string accessCode = "access-code";
        var authRequest = new AuthRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = AuthRequestType.AuthenticateAndUnlock,
            AccessCode = accessCode,
            Approved = true,
            ResponseDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
        };
        var sutProvider = CreateSutProviderFindingUser(user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;
        sutProvider.GetDependency<IAuthRequestRepository>().GetByIdAsync(authRequest.Id).Returns(authRequest);

        await sutProvider.Sut.SendEmailLoginAsync(
            new TwoFactorEmailLoginRequestModel
            {
                Email = user.Email,
                AuthRequestId = authRequest.Id.ToString(),
                AuthRequestAccessCode = accessCode,
            });

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);
    }

    /// <summary>
    /// The SSO session token branch binds the code to the requesting device.
    /// </summary>
    [Theory, BitAutoData]
    public async Task SendEmailLogin_ValidSsoSessionToken_SendsCodeForRequestingDevice(
        User user)
    {
        const string ssoSessionToken = "sso-session-token";
        var sutProvider = CreateSutProviderFindingUser(user);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier = DeviceIdentifier;
        sutProvider.GetDependency<IDataProtectorTokenFactory<SsoEmail2faSessionTokenable>>()
            .TryUnprotect(ssoSessionToken, out Arg.Any<SsoEmail2faSessionTokenable>())
            .Returns(call =>
            {
                call[1] = new SsoEmail2faSessionTokenable(user);
                return true;
            });

        await sutProvider.Sut.SendEmailLoginAsync(
            new TwoFactorEmailLoginRequestModel { Email = user.Email, SsoEmail2FaSessionToken = ssoSessionToken });

        await sutProvider.GetDependency<ITwoFactorEmailService>()
            .Received(1)
            .SendTwoFactorLoginEmailAsync(user, DeviceIdentifier);
    }

    private static TwoFactorEmailLoginRequestModel MasterPasswordModel(User user) =>
        new() { Email = user.Email, MasterPasswordHash = MasterPasswordHash };

    private static void SetupMasterPasswordToPass(SutProvider<TwoFactorController> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, MasterPasswordHash).Returns(true);
    }

    /// <summary>
    /// Builds the controller with a substitute UserManager that finds the given user by email. The UserManager the
    /// fixture builds is a real instance over a store that cannot look users up by email.
    /// </summary>
    private static SutProvider<TwoFactorController> CreateSutProviderFindingUser(User user)
    {
        var userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
        userManager.FindByEmailAsync(user.Email.ToLowerInvariant()).Returns(user);

        return new SutProvider<TwoFactorController>(
                new Fixture().Customize(new ControllerCustomization<TwoFactorController>()))
            .SetDependency(userManager)
            .Create();
    }
}
